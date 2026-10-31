using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using Last.Entity.Field;
using System.Text.Json;
using Last.Management;
using Last.Map;
using HarmonyLib;

namespace FF4Iso;

[BepInPlugin("ff4jobs.isometric.prototype","FF4 Isometric Courtyard Prototype","0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger=null!;
    public override void Load()
    {
        Logger=Log;
        string expected=File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!,"gameassembly.sha256")).Trim();
        string actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Paths.GameRootPath,"GameAssembly.dll")))).ToLowerInvariant();
        if(expected!=actual) {Log.LogError("Isometric prototype disabled: game build differs.");return;}
        new Harmony("ff4jobs.isometric.prototype").PatchAll();
        AddComponent<IsoScene>();
        Log.LogInfo("Isometric courtyard prototype loaded.");
    }
}

public sealed class IsoScene : MonoBehaviour
{
    public IsoScene(IntPtr pointer):base(pointer) {}
    private float nextPoll;
    private bool warpPending;
    private static SubSceneManagerMainGame.State? Phase => SceneManager.Instance?.GetCurrentSubSceneManager()?.TryCast<SubSceneManagerMainGame>()?.GetCurrentState();
    internal static bool Chord => Input.GetKey(KeyCode.JoystickButton5) && Input.GetKey(KeyCode.JoystickButton7);
    private bool chordHeld;
    private bool desired=true,failed;
    private CourtyardView? view;
    private FieldMap? field;
    private int mapId;
    private void Update()
    {
        try
        {
            bool chord=Chord;
            if(Input.GetKeyDown(KeyCode.F9) || (chord && !chordHeld)) {desired=!desired;Plugin.Logger.LogInfo("Isometric view: "+desired);}
            chordHeld=chord;
            if(Input.GetKeyDown(KeyCode.F10)) warpPending=true;
            if(Time.unscaledTime>=nextPoll)
            {
                nextPoll=Time.unscaledTime+.3f;
                field=UnityEngine.Object.FindObjectOfType<FieldMap>();
                mapId=field?.mapManager?.CurrentMapModel?.GetId()??0;
                if(Take("inspect")) Inspect();
                if(Take("warp")) warpPending=true;
                if(Take("toggle")) desired=!desired;
                if(Take("rebuild")) {view?.Dispose();view=null;failed=false;}
                if(warpPending && Phase==SubSceneManagerMainGame.State.Player && field?.fieldController?.eventHandle!=null)
                {
                    warpPending=false; view?.SetActive(false);
                    var load=new LoadData();load.NextMapData(4,1);
                    field.fieldController.eventHandle.EventGotoMap(load,false,false);
                    Plugin.Logger.LogInfo("Courtyard test warp requested via native map transition.");
                    return;
                }
                if(mapId!=4) {view?.Dispose();view=null;failed=false;}
                else if(view==null && !failed && Phase==SubSceneManagerMainGame.State.Player)
                {
                    try {view=new CourtyardView();} catch(Exception e) {failed=true;Plugin.Logger.LogError("Courtyard build failed; normal view retained: "+e);}
                }
            }
        }
        catch(Exception e) {view?.SetActive(false);failed=true;Plugin.Logger.LogError("Isometric update recovered: "+e);enabled=false;}
    }
    private void LateUpdate()
    {
        try
        {
            bool active=desired && !failed && mapId==4 && Phase==SubSceneManagerMainGame.State.Player;
            view?.SetActive(active);
            if(active) view?.Tick();
        }
        catch(Exception e) {view?.SetActive(false);failed=true;Plugin.Logger.LogError("Isometric rendering stopped; normal view restored: "+e);}
    }
    private static bool Take(string name)
    {
        var path=Path.Combine(Paths.ConfigPath,"ff4iso-"+name+".request");
        if(!File.Exists(path))return false;
        File.Delete(path);return true;
    }
    private void OnDestroy() {view?.Dispose();view=null;}
    private static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    private static float[] U(Vector2 v)=>new[]{v.x,v.y};
    private static string Hierarchy(Transform t)
    {
        string s=t.name;
        for(int n=0;t.parent!=null && n<8;n++) {t=t.parent;s=t.name+"/"+s;}
        return s;
    }
    private static void Inspect()
    {
        var cameras=UnityEngine.Object.FindObjectsOfType<Camera>().Select(c=>new {name=Hierarchy(c.transform),position=V(c.transform.position),rotation=V(c.transform.eulerAngles),size=c.orthographicSize,c.orthographic,c.depth,c.cullingMask,target=c.targetTexture?.name,enabled=c.enabled}).ToArray();
        var meshes=UnityEngine.Object.FindObjectsOfType<MapLayerMesh>().Select(m=>new {name=Hierarchy(m.transform),position=V(m.transform.position),scale=V(m.transform.lossyScale),layer=m.LayerData?.LayerName,order=m.LayerData?.ZOrder,
            fullVertices=m.mapTileGroupData.Vertices.Select(V).ToArray(),tileIds=m.LayerData.MapTileUvDatas.Select(t=>t==null?-999:t.TileId).ToArray(),uvTable=m.mapTileGroupData.TexUVTable.Select(a=>a==null?null:a.Select(U).ToArray()).ToArray(),cells=m.cellList?.ToArray(),
            vertices=m.GetComponent<MeshFilter>().sharedMesh.vertices.Select(V).ToArray(),uv=m.GetComponent<MeshFilter>().sharedMesh.uv.Select(U).ToArray(),triangles=m.GetComponent<MeshFilter>().sharedMesh.triangles.ToArray(),
            shader=m.GetComponent<MeshRenderer>().sharedMaterial.shader.name,texture=m.GetComponent<MeshRenderer>().sharedMaterial.mainTexture?.name,textureScale=U(m.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale),textureOffset=U(m.GetComponent<MeshRenderer>().sharedMaterial.mainTextureOffset)}).ToArray();
        var sprites=UnityEngine.Object.FindObjectsOfType<FieldSpriteEntity>().Select(s=>new {name=Hierarchy(s.transform),position=V(s.transform.position),visual=V(s.VisualParent.position),sprite=s.SpriteRenderer?.sprite?.name,scale=V(s.transform.lossyScale),bounds=s.SpriteRenderer==null?null:V(s.SpriteRenderer.bounds.size)}).ToArray();
        File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4iso-inspect.json"),JsonSerializer.Serialize(new {phase=Phase?.ToString(),map=UnityEngine.Object.FindObjectOfType<FieldMap>()?.mapManager?.CurrentMapModel?.GetId(),cameras,meshes,sprites},new JsonSerializerOptions{WriteIndented=true}));
        Plugin.Logger.LogInfo($"Isometric inspection: {meshes.Length} map layers, {sprites.Length} entities.");
    }
}

[HarmonyPatch(typeof(Il2CppSystem.Input.InputSystemManager),"GetKeyDown",new[]{typeof(Il2CppSystem.Input.InputSystemManager.InputActionType)})]
internal static class ToggleChordInput
{
    private static void Postfix(ref bool __result)
    {
        if(__result && IsoScene.Chord) __result=false;
    }
}
