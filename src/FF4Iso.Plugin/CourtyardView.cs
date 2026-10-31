using BepInEx;
using Last.Entity.Field;
using UnityEngine;
using UnityEngine.UI;
using System.Text.Json;
using Object=UnityEngine.Object;

namespace FF4Iso;

// A separate presentation scene. Native entities, collisions, camera and events are untouched.
internal sealed class CourtyardView : IDisposable
{
    private const int Layer=31;
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly GameObject hud;
    private readonly List<Object> owned=new();
    private readonly List<(FieldSpriteEntity source,SpriteRenderer copy)> sprites=new();
    private CameraFollowing? following;
    private float scanAt;
    private readonly Settings settings;
    private float left=-320,top=288;
    private readonly Vector3 origin=new(10000,10000,10000);
    private readonly Material spriteMaterial;
    private static string SettingsPath=>Path.Combine(Paths.ConfigPath,"ff4iso-view.json");
    internal sealed class Band
    {
        public int X0 {get;set;} public int X1 {get;set;} public int Y0 {get;set;} public int Y1 {get;set;}
        public float Height {get;set;}=64;public float Depth {get;set;}=32;
    }
    internal sealed class Settings
    {
        public float Pitch {get;set;}=48;public float Yaw {get;set;}=35;public float Size {get;set;}=132;
        public bool Follow {get;set;}=true;public float SpriteScale {get;set;}=1;
        public string Shader {get;set;}="Unlit/Transparent Cutout";
        public Band[] Bands {get;set;}=new[]{
            new Band{X0=5,X1=17,Y0=25,Y1=31,Height=72,Depth=32},
            new Band{X0=23,X1=35,Y0=25,Y1=31,Height=72,Depth=32},
            new Band{X0=17,X1=23,Y0=25,Y1=31,Height=72,Depth=24},
            new Band{X0=7,X1=15,Y0=17,Y1=21,Height=48,Depth=24},
            new Band{X0=25,X1=33,Y0=17,Y1=21,Height=48,Depth=24},
            new Band{X0=17,X1=23,Y0=15,Y1=20,Height=64,Depth=32},
            new Band{X0=5,X1=35,Y0=6,Y1=12,Height=80,Depth=32}
        };
    }
    internal CourtyardView()
    {
        settings=File.Exists(SettingsPath)?JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath))??new():new();
        if(!File.Exists(SettingsPath))File.WriteAllText(SettingsPath,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
        root=new GameObject("FF4 Iso presentation");root.layer=Layer;root.transform.position=origin;Object.DontDestroyOnLoad(root);
        root.SetActive(false);
        try
        {
            var go=Child("Isometric camera");camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=Math.Clamp(settings.Size,80,400);camera.depth=100;
            camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.075f,1);
            camera.nearClipPlane=.3f;camera.farClipPlane=2500;camera.allowHDR=false;camera.allowMSAA=false;
            camera.transform.rotation=Quaternion.Euler(settings.Pitch,settings.Yaw,0);
            var shader=UnityEngine.Shader.Find(settings.Shader)??UnityEngine.Shader.Find("Sprites/Default");
            if(shader==null)throw new InvalidOperationException("No usable texture shader found.");
            Plugin.Logger.LogInfo("Courtyard terrain shader: "+shader.name);
            spriteMaterial=new Material(UnityEngine.Shader.Find("Sprites/Default")??shader);owned.Add(spriteMaterial);
            BuildTerrain(shader);
            hud=BuildHud();
            ScanSprites();
            Plugin.Logger.LogInfo("Courtyard 3D geometry ready; RB + Start / F9 toggles.");
        }
        catch {Dispose();throw;}
    }
    private GameObject Child(string name)
    {
        var go=new GameObject(name);go.layer=Layer;go.transform.SetParent(root.transform,false);return go;
    }
    private void BuildTerrain(Shader shader)
    {
        var sources=Object.FindObjectsOfType<MapLayerMesh>().Where(m=>m.name.EndsWith("_opaque") && m.mapTileGroupData!=null).ToArray();
        var seen=new HashSet<string>();int total=0;
        foreach(var src in sources)
        {
            if(!seen.Add(src.LayerData.LayerName))continue;
            var group=src.mapTileGroupData;var cells=src.LayerData.MapTileUvDatas;var verts=group.Vertices;var table=group.TexUVTable;
            if(cells.Length!=40*36 || verts.Length!=cells.Length*4) {Plugin.Logger.LogInfo($"Skip {src.name}: cells={cells.Length}, vertices={verts.Length}");continue;}
            left=verts[0].x;top=verts[0].y;
            var material=new Material(shader);owned.Add(material);material.mainTexture=group.TileTexture;
            if(material.HasProperty("_Color")) material.SetColor("_Color",Color.white);
            if(material.HasProperty("_Cutoff"))material.SetFloat("_Cutoff",.4f);
            var geometry=new Geometry();
            bool ground=src.LayerData.LayerName=="BottomLayer";
            float lift=ground?0:.08f;
            for(int cell=0;cell<cells.Length;cell++)
            {
                var data=cells[cell];if(data==null)continue;int id=data.GetCurrentTileId();
                if(id<0 || id>=table.Length || table[id]==null || table[id].Length<4)continue;
                int x=cell%40,y=cell/40;var uv=table[id].ToArray();
                var band=settings.Bands.FirstOrDefault(b=>x>=b.X0&&x<b.X1&&y>=b.Y0&&y<b.Y1);
                Vector3 a,b,c,d;
                if(band!=null)
                {
                    float z=top-band.Y1*16;float h0=(band.Y1-y)*band.Height/(band.Y1-band.Y0),h1=(band.Y1-y-1)*band.Height/(band.Y1-band.Y0);
                    a=new(left+x*16,h0,z-lift);b=new(left+(x+1)*16,h0,z-lift);c=new(left+x*16,h1,z-lift);d=new(left+(x+1)*16,h1,z-lift);
                }
                else {a=new(verts[cell*4].x,lift,verts[cell*4].y);b=new(verts[cell*4+1].x,lift,verts[cell*4+1].y);c=new(verts[cell*4+2].x,lift,verts[cell*4+2].y);d=new(verts[cell*4+3].x,lift,verts[cell*4+3].y);}
                geometry.Quad(a,b,c,d,uv,Color.white);total++;
                // Extend exposed left/right faces with the closest original wall tile.
                if(ground && band!=null)
                {
                    float back=top-band.Y1*16+band.Depth;
                    if(x==band.X0)geometry.Quad(new(a.x,a.y,back),a,new(c.x,c.y,back),c,uv,new Color(.62f,.65f,.75f,1));
                    if(x==band.X1-1)geometry.Quad(b,new(b.x,b.y,back),d,new(d.x,d.y,back),uv,new Color(.78f,.81f,.9f,1));
                    if(y==band.Y0)geometry.Quad(new(a.x,a.y,back),new(b.x,b.y,back),a,b,uv,new Color(.88f,.9f,.95f,1));
                }
            }
            var mesh=geometry.Mesh();owned.Add(mesh);var go=Child("Raised "+src.LayerData.LayerName);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        if(total==0)throw new InvalidOperationException("Courtyard full tile geometry not found.");
        Plugin.Logger.LogInfo($"Courtyard copied {total} tile faces; origin={left},{top}.");
    }
    private void ScanSprites()
    {
        foreach(var item in sprites)if(item.copy!=null)Object.Destroy(item.copy.gameObject);
        sprites.Clear();
        foreach(var source in Object.FindObjectsOfType<FieldSpriteEntity>())
        {
            if(source.SpriteRenderer==null)continue;
            var copy=Child("Original sprite "+source.name).AddComponent<SpriteRenderer>();
            copy.sharedMaterial=spriteMaterial;sprites.Add((source,copy));
        }
        following=Object.FindObjectsOfType<CameraFollowing>().FirstOrDefault(c=>c.TargetEntity!=null);
    }
    internal void SetActive(bool active)
    {
        if(root==null)return;
        if(root.activeSelf!=active)root.SetActive(active);
        camera.enabled=active;if(hud!=null)hud.SetActive(active);
    }
    internal void Tick()
    {
        if(Time.unscaledTime>=scanAt) {scanAt=Time.unscaledTime+2;ScanSprites();}
        var target=following?.TargetEntity;
        Vector3 focus=target!=null&&settings.Follow?new(target.transform.position.x,14,target.transform.position.y):new(0,24,0);
        camera.transform.position=origin+focus-camera.transform.forward*900;
        foreach(var (source,copy) in sprites)
        {
            if(source==null || copy==null)continue;
            var native=source.SpriteRenderer;copy.enabled=native!=null && native.enabled && native.gameObject.activeInHierarchy && native.sprite!=null;
            if(!copy.enabled)continue;
            copy.sprite=native!.sprite;copy.flipX=native.flipX;copy.flipY=native.flipY;copy.color=native.color;
            copy.transform.rotation=camera.transform.rotation;
            var scale=native.transform.lossyScale;copy.transform.localScale=new Vector3(Math.Abs(scale.x),Math.Abs(scale.y),1)*settings.SpriteScale;
            var p=source.transform.position;
            var bounds=copy.sprite.bounds;
            float feet=-bounds.min.y*Math.Abs(scale.y)*settings.SpriteScale;
            copy.transform.position=origin+new Vector3(p.x,1,p.y)+camera.transform.up*feet;
            copy.sortingOrder=Mathf.RoundToInt(-p.y);
        }
    }
    private GameObject BuildHud()
    {
        var go=new GameObject("Courtyard prototype controls");Object.DontDestroyOnLoad(go);
        var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var panel=new GameObject("Controls");var r=panel.AddComponent<RectTransform>();r.SetParent(go.transform,false);r.anchorMin=new Vector2(.02f,.93f);r.anchorMax=new Vector2(.98f,.985f);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
        var bg=panel.AddComponent<Image>();bg.color=new Color(.02f,.04f,.18f,.94f);bg.raycastTarget=false;
        var label=new GameObject("Hint");var lr=label.AddComponent<RectTransform>();lr.SetParent(r,false);lr.anchorMin=new Vector2(.015f,0);lr.anchorMax=new Vector2(.99f,1);lr.offsetMin=Vector2.zero;lr.offsetMax=Vector2.zero;
        var text=label.AddComponent<Text>();text.font=Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f=>f.name=="SE-ALPSTN__")??Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();text.fontSize=27;text.color=Color.white;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
        text.text="BARON COURTYARD PROTOTYPE     RB + Start / F9: normal view     F10: courtyard";
        go.SetActive(false);return go;
    }
    public void Dispose()
    {
        if(camera!=null)camera.enabled=false;
        if(root!=null) {root.SetActive(false);Object.Destroy(root);}
        if(hud!=null)Object.Destroy(hud);
        foreach(var item in owned)if(item!=null)Object.Destroy(item);
        owned.Clear();sprites.Clear();
    }
    private sealed class Geometry
    {
        private readonly List<Vector3> v=new();private readonly List<Vector2> uv=new();private readonly List<int> t=new();private readonly List<Color> c=new();
        public void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector2[] tex,Color color)
        {
            int n=v.Count;v.AddRange(new[]{a,b,d,e});uv.AddRange(tex.Take(4));c.AddRange(new[]{color,color,color,color});
            t.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3,n+2,n+1,n,n+3,n+1,n+2});
        }
        public Mesh Mesh()
        {
            var m=new Mesh();m.vertices=v.ToArray();m.uv=uv.ToArray();m.colors=c.ToArray();m.triangles=t.ToArray();m.RecalculateBounds();return m;
        }
    }
}
