using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using FF4Tas.Core;
using Last.Management;
using UnityEngine;
using Il2CppSystem.Input;
using System.Reflection;
using System.Text.Json;

namespace FF4Tas;

[BepInPlugin("ff4jobs.scriptedrun", "FF4 Scripted Run Prototype", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal static string DirectoryPath="";
    internal static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public override void Load()
    {
        // Separate opt-in, separate command directory. Normal Jobs launches never patch input.
        if(Environment.GetEnvironmentVariable("FF4JOBS_TAS")!="1")return;
        var directory=Environment.GetEnvironmentVariable("FF4JOBS_TAS_DIR");
        if(string.IsNullOrWhiteSpace(directory))throw new InvalidOperationException("FF4JOBS_TAS_DIR must identify this test session");
        DirectoryPath=Path.GetFullPath(directory);Directory.CreateDirectory(DirectoryPath);
        // This prototype is specific to the same executable inspected for Jobs+.
        const string expected="bb2f4c9db44c8ee9b065696aeafb77561a1709492130f045433e6d215abc42ed";
        string actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Paths.GameRootPath,"GameAssembly.dll")))).ToLowerInvariant();
        if(actual!=expected)throw new InvalidOperationException("Unverified FF4 executable; scripted input disabled");
        // Never replay a stale request from an earlier process.
        File.Delete(Path.Combine(DirectoryPath,"request.json"));
        var harmony=new Harmony("ff4jobs.scriptedrun");
        try { harmony.PatchAll();AddComponent<Controller>(); }
        catch { harmony.UnpatchSelf();throw; }
        Log.LogInfo("Scripted-run prototype armed; waiting for a route. F10 stops. No route starts automatically.");
    }
}
public sealed class Controller : MonoBehaviour
{
    public Controller(IntPtr p):base(p) { }
    internal static RouteEngine? Engine;
    internal static readonly string Session=Guid.NewGuid().ToString("N");
    private static Snapshot snapshot=new();
    private static int frame=-1;
    private static float nextVision;
    private static Vision? vision;
    private sealed class Vision
    {
        public DateTime Utc {get;set;}
        public string[] Matches {get;set;}=Array.Empty<string>();
        public string? Error {get;set;}
    }
    private float nextPoll,nextReport;
    private string error="",routeName="";
    private static Dictionary<string,string>? names;
    internal static string? Logical(string nativeName)
    {
        names??=new Dictionary<string,string>{
            [Key.Action.Name]="Confirm",[Key.Cancel.Name]="Back",[Key.Menu.Name]="Menu",[Key.Shortcut.Name]="Shortcut",
            [Key.Up.Name]="Up",[Key.Down.Name]="Down",[Key.Left.Name]="Left",[Key.Right.Name]="Right",
            [Key.SwitchLeft.Name]="SwitchLeft",[Key.SwitchRight.Name]="SwitchRight",[Key.PageUp.Name]="PageUp",
            [Key.PageDown.Name]="PageDown",[Key.Start.Name]="Start",[Key.Select.Name]="Select"};
        return names.TryGetValue(nativeName,out var value)?value:null;
    }
    internal static void Sample()
    {
        if(frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            var phase=SceneManager.Instance?.GetCurrentSubSceneManager()?.TryCast<SubSceneManagerMainGame>()?.GetCurrentState();
            snapshot=new Snapshot{Phase=phase?.ToString()??"None",MapId=UserDataManager.Instance()?.CurrentMapId??0,Focused=Application.isFocused};
            if(Time.unscaledTime>=nextVision)
            {
                nextVision=Time.unscaledTime+.2f;
                var path=Path.Combine(Plugin.DirectoryPath,"vision.json");
                try {vision=File.Exists(path)?JsonSerializer.Deserialize<Vision>(File.ReadAllText(path),Plugin.Json):null;}
                catch {vision=null;}
            }
            double age=vision==null?double.MaxValue:(DateTime.UtcNow-vision.Utc.ToUniversalTime()).TotalSeconds;
            if(vision?.Error==null && age>=0 && age<=1 && vision?.Matches!=null)snapshot.VisualMatches=vision.Matches;
            if(Input.GetKey(KeyCode.F10))Engine?.Stop("Emergency stop");
            Engine?.Tick(frame,snapshot);
        }
        catch(Exception e) { Engine?.Stop("State read failed: "+e.Message); }
    }
    private void Update()
    {
        if(Environment.GetEnvironmentVariable("FF4JOBS_TAS_SMOKE")=="1") {Smoke();return;}
        Sample();
        try
        {
            if(Time.unscaledTime>=nextPoll)
            {
                nextPoll=Time.unscaledTime+.05f;
                string request=Path.Combine(Plugin.DirectoryPath,"request.json");
                if(File.Exists(request))
                {
                    var command=JsonSerializer.Deserialize<Request>(File.ReadAllText(request),Plugin.Json);
                    File.Delete(request);
                    if(command==null || command.Session!=Session)throw new InvalidOperationException("Request belongs to another game session");
                    if(command.Command=="stop")Engine?.Stop("Stop requested");
                    else if(command.Command=="start")
                    {
                        if(Engine?.Running==true)throw new InvalidOperationException("Stop the current route first");
                        var route=command.Route??throw new InvalidOperationException("Missing route");
                        var next=new RouteEngine(route);Engine=next;routeName=route.Name;error="";
                        // First synthetic input begins next frame, never partway through this one.
                    }
                    else throw new InvalidOperationException("Unknown command");
                }
            }
            if(Time.unscaledTime>=nextReport) {nextReport=Time.unscaledTime+.1f;Report();}
        }
        catch(Exception e) { error=e.Message;Engine?.Stop(error);Report(); }
    }
    private bool smokeDone;
    private void Smoke()
    {
        if(smokeDone || InputListener.Instance==null || InputSystemManager.Instance==null)return;
        smokeDone=true;bool passed=false;string? failure=null;int checks=0;
        try
        {
            // Exercise native query hooks synchronously, releasing input before
            // another game Update can consume it. No route/save is loaded.
            frame=Time.frameCount;
            Engine=new RouteEngine(new Route{Name="native input probe",Steps=new[]{new Step{
                Label="confirm",Kind="hold",Keys=new[]{"Confirm"},Phases=new[]{"None"},Frames=1}}});
            Engine.Tick(frame,new Snapshot{Phase="None",Focused=true});
            var listener=InputListener.Instance;
            var device=Il2CppSystem.Input.KeyConfig.KeyValue.InputDeviceType.None;
            if(!listener.GetKeyDown(Key.Action,device,false,true))throw new Exception("Native Confirm down failed");checks++;
            if(!listener.GetKey(Key.Action,device,true))throw new Exception("Native Confirm held failed");checks++;
            if(listener.GetKeyDown(Key.Cancel,device,false,true))throw new Exception("Confirm leaked into Back");checks++;
            if(!listener.KeyConfig.GetKeyDown(Il2CppSystem.Input.KeyConfig.KeyValue.InputDeviceType.GamePad,Key.Action.Name,false))throw new Exception("Direct configured controller query failed");checks++;
            passed=true;
        }
        catch(Exception e) {failure=e.ToString();}
        finally
        {
            Engine?.Stop("Native input check complete");Engine=null;
            File.WriteAllText(Path.Combine(Plugin.DirectoryPath,"smoke.json"),JsonSerializer.Serialize(new {
                passed,nativeInputChecks=checks,error=failure,
                scope="Native logical-input hooks only; no full route, movement or campaign tested"
            },Plugin.Json));
            Application.Quit();
        }
    }
    private void Report()
    {
        string target=Path.Combine(Plugin.DirectoryPath,"status.json");
        File.WriteAllText(target+".tmp",JsonSerializer.Serialize(new{
            session=Session,utc=DateTime.UtcNow,frame,route=routeName,status=Engine?.Status??"idle",
            step=Engine?.Index,label=Engine?.Label,reason=Engine?.Reason,error,
            phase=snapshot.Phase,mapId=snapshot.MapId,focused=snapshot.Focused,
            scope="Observed Unity frames; deterministic TAS/savestates and complete route not implemented"
        },Plugin.Json));
        File.Move(target+".tmp",target,true);
    }
    private void OnApplicationFocus(bool focused)
    {
        if(!focused)Engine?.Stop("Game lost focus");
    }
    private void OnDisable()=>Engine?.Stop("Runner disabled");
    private void OnDestroy()=>Engine?.Stop("Runner destroyed");
    private sealed class Request
    {
        public string Session {get;set;}="";
        public string Command {get;set;}="";
        public Route? Route {get;set;}
    }
}

// Unique native methods, not shared IL2CPP getters. InputListener handles normal
// gameplay/Jobs; KeyConfig covers UI code that queries logical keys directly.
[HarmonyPatch]
internal static class ScriptInput
{
    static IEnumerable<MethodBase> TargetMethods()=>new[]{typeof(InputListener),typeof(Il2CppSystem.Input.KeyConfig.KeyConfig)}
        .SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
        .Where(m=>m.Name is "GetKey" or "GetKeyDown" or "GetKeyUp");
    static bool Prefix(MethodBase __originalMethod,object[] __args,ref bool __result)
    {
        Controller.Sample();
        var engine=Controller.Engine;
        if(engine?.Running!=true)return true;
        string? name=__args[0] is Key key?key.Name:__args.Length>1?__args[1] as string:null;
        string? logical=name==null?null:Controller.Logical(name);
        if(logical==null)return true;
        __result=__originalMethod.Name switch {"GetKeyDown"=>engine.Down(logical),"GetKeyUp"=>engine.Up(logical),_=>engine.Held(logical)};
        return false;
    }
}
