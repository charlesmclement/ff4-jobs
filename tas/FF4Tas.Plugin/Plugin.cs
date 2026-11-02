using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using FF4Tas.Core;
using Last.Management;
using Last.Interpreter;
using UnityEngine;
using Il2CppSystem.Input;
using System.Reflection;
using System.Text.Json;

namespace FF4Tas;

[BepInPlugin("ff4jobs.scriptedrun", "FF4 Scripted Run Prototype", "0.2.0")]
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
    private static FieldMap? field;
    private static readonly Navigation navigation=new();
    private static readonly BattlePilot battle=new();
    private static string lastPhase="";
    private string lastRecord="";
    private float nextRecord;
    private static float nextFieldFind;
    private static float nextVision;
    private static Vision? vision;
    private const int StoryFlagScan=40;
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
            var active=Engine?.ActiveStep;
            // Early story flags are reported even while idle so the launcher can
            // tell which step a loaded save is at (e.g. arriving vs leaving Kaipo).
            var storage=DataStorage.Instance;
            if(storage!=null && UserDataManager.Instance()!=null)
            {
                var flags=new List<int>();
                for(int i=0;i<=StoryFlagScan;i++)if(storage.Get(DataStorage.Category.kScenarioFlag1,i)!=0)flags.Add(i);
                int? requested=active?.Until?.StoryFlag;
                if(requested>StoryFlagScan && storage.Get(DataStorage.Category.kScenarioFlag1,requested.Value)!=0)flags.Add(requested.Value);
                snapshot.StoryFlags=flags.ToArray();
                if(active?.Until?.TreasureFlag is int chest && storage.Get(DataStorage.Category.kTreasureFlag1,chest)!=0)
                    snapshot.TreasureFlags=new[]{chest};
            }
            if(active!=null)
            {
                // PopupManager's filter is up whenever a modal popup owns input
                // (same test Jobs fast-forward uses); movement is frozen meanwhile.
                snapshot.Modal=Last.UI.PopupManager.Instance?.Filter?.activeInHierarchy==true;
                if(active.Choice!=null)
                {
                    var select=Last.Message.MessageSelectWindowManager.Instance;
                    if(select!=null && select.IsOpen())snapshot.ChoiceIndex=select.controller?.selectCursor?.Index;
                }
                if(snapshot.Phase!=lastPhase)
                {
                    navigation.Reset();
                    if(snapshot.Phase!="Battle")battle.Reset();
                    lastPhase=snapshot.Phase;
                }
                // While paused for focus, skip movement/battle reads so stall
                // detection does not count frames the route is not driving.
                if(!snapshot.Focused || snapshot.Modal)navigation.Reset();
                else if(snapshot.Phase=="Player")
                {
                    if((field==null || !field) && Time.unscaledTime>=nextFieldFind)
                    {field=UnityEngine.Object.FindObjectOfType<FieldMap>();nextFieldFind=Time.unscaledTime+.5f;}
                    if(field!=null && field)
                    {
                        snapshot.MapId=field.mapManager?.CurrentMapModel?.GetId()??snapshot.MapId;
                        navigation.Read(field,active,snapshot,frame);
                    }
                }
                else if(snapshot.Phase=="Battle" && active.HandleBattles)battle.Read(snapshot,frame,active.BattlePlan);
            }
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
                        var next=new RouteEngine(route);navigation.Reset();battle.Reset();battle.Weaknesses=route.Weaknesses;battle.FleeFrom=new HashSet<int>(route.FleeFrom);battle.Db=BattleDb.Load(Path.Combine(Plugin.DirectoryPath,"battle.json"));lastPhase="";Engine=next;routeName=route.Name;error="";RunClock.Reset();
                        // First synthetic input begins next frame, never partway through this one.
                    }
                    else throw new InvalidOperationException("Unknown command");
                }
            }
            RunClock.Update(Engine?.Running==true && !Engine.Paused);
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
            session=Session,utc=DateTime.UtcNow,frame,route=routeName,status=Engine?.Paused==true?"paused":Engine?.Status??"idle",
            step=Engine?.Index,label=Engine?.Label,reason=Engine?.Reason,error,elapsedSeconds=Math.Round(RunClock.RealSeconds,2),gameSeconds=Math.Round(RunClock.GameSeconds,2),speedBoost=RunClock.Boost,speedMultiplier=RunClock.Multiplier,
            phase=snapshot.Phase,mapId=snapshot.MapId,focused=snapshot.Focused,
            x=snapshot.X,y=snapshot.Y,layer=snapshot.Layer,storyFlags=snapshot.StoryFlags,detail=snapshot.Detail,battle=snapshot.Battle,
            scope="Untested scripted Baron/Mist Dragon route; observed Unity frames, no deterministic savestates"
        },Plugin.Json));
        File.Move(target+".tmp",target,true);
        string record=$"{Engine?.Status}|{Engine?.Index}|{snapshot.Phase}|{snapshot.MapId}|{snapshot.Detail}|{snapshot.Battle}|{Engine?.Reason}";
        if(record!=lastRecord || Time.unscaledTime>=nextRecord)
        {
            File.AppendAllText(Path.Combine(Plugin.DirectoryPath,"trace-"+Session+".jsonl"),File.ReadAllText(target).Replace("\r","").Replace("\n","")+"\n");
            lastRecord=record;nextRecord=Time.unscaledTime+1;
        }
    }
    private void OnDisable() {Engine?.Stop("Runner disabled");RunClock.Restore();}
    private void OnDestroy() {Engine?.Stop("Runner destroyed");RunClock.Restore();}
    private static GUIStyle? overlayStyle,shadowStyle;
    // Run timer overlay. The TAS time is game time: it speeds up with the boost,
    // so a boosted run is timed exactly as if it ran at normal speed. Real
    // wall-clock time is shown for reference only.
    private void OnGUI()
    {
        if(Engine==null && !RunClock.Boost)return;
        if(overlayStyle==null)
        {
            overlayStyle=new GUIStyle();overlayStyle.fontSize=18;overlayStyle.normal.textColor=Color.white;
            shadowStyle=new GUIStyle();shadowStyle.fontSize=18;shadowStyle.normal.textColor=Color.black;
        }
        string state=Engine==null?"idle":Engine.Paused?"paused":Engine.Status;
        string speed=RunClock.Boost?$"  {RunClock.Multiplier:0.#}x ON (F8)":"  1x (F8 for speed)";
        string text=$"TAS {RunClock.Format(RunClock.GameSeconds)}  (wall clock {RunClock.Format(RunClock.RealSeconds)}){speed}\n{state}: {Engine?.Label}";
        // In battle, a third line shows the enemies and the latest decision.
        if(snapshot.Phase=="Battle" && snapshot.Battle.Length>0)text+="\n"+snapshot.Battle;
        GUI.Label(new Rect(11,9,1400,90),text,shadowStyle);
        GUI.Label(new Rect(10,8,1400,90),text,overlayStyle);
    }
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

// This is the nontrivial field-axis query (RVA 0x394750 in the verified build),
// not a shared IL2CPP backing-field getter. Movement still passes through the
// native player's input, collision, encounters and foot-trigger handling.
[HarmonyPatch(typeof(InputDevice),"get_InputAxis")]
internal static class ScriptFieldAxis
{
    static bool Prefix(ref Vector2 __result)
    {
        Controller.Sample();var engine=Controller.Engine;
        if(engine?.Running!=true)return true;
        __result=new Vector2((engine.Held("Right")?1:0)-(engine.Held("Left")?1:0),
            (engine.Held("Up")?1:0)-(engine.Held("Down")?1:0));
        return false;
    }
}
