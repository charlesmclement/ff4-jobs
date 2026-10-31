using UnityEngine;
using UnityEngine.UI;
using Last.UI.KeyInput;

namespace FF4Jobs;

// Native Unity UI only: this FF4 build strips IMGUI texture drawing methods.
internal static class MenuSkin
{
    internal static readonly Color Blue=new(.075f,.075f,.55f,1);
    internal static Font? Font;
    private static FontStyle fontStyle;
    private static Sprite? frame;
    private static float frameScale=1;
    internal static void Capture(MainMenuController menu)
    {
        var commands=menu.commandMenuController;
        var contents=commands.contents;
        if(contents!=null && contents.Count>0) { Font=contents[0].NameText.font; fontStyle=contents[0].NameText.fontStyle; }
        float largest=0;
        foreach(var image in commands.GetComponentsInChildren<Image>(true))
        {
            if(image.sprite==null || image.type!=Image.Type.Sliced || !image.fillCenter || image.sprite.border==Vector4.zero) continue;
            var rect=image.rectTransform.rect; float area=rect.width*rect.height;
            if(area<=largest) continue;
            largest=area; frame=image.sprite; frameScale=image.pixelsPerUnitMultiplier;
        }
        Plugin.Logger.LogInfo($"Native menu skin: font={Font?.name}, frame={frame?.name}");
    }
    internal static GameObject Root(MainMenuController menu)
    {
        var canvas=menu.GetComponentInParent<Canvas>();
        if(canvas==null || Font==null) throw new InvalidOperationException("FF4 menu assets not ready.");
        var root=new GameObject("FF4 Jobs screen");
        var rect=root.AddComponent<RectTransform>(); rect.SetParent(canvas.transform,false);
        rect.anchorMin=new Vector2(.04f,.04f); rect.anchorMax=new Vector2(.96f,.96f);
        rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
        var background=root.AddComponent<Image>(); background.color=Blue; background.raycastTarget=true;
        if(frame!=null)
        {
            var edge=Element(root.transform,"Native border",0,0,1,1).gameObject.AddComponent<Image>();
            edge.sprite=frame; edge.type=Image.Type.Sliced; edge.fillCenter=false;
            edge.pixelsPerUnitMultiplier=frameScale; edge.raycastTarget=false;
        }
        rect.SetAsLastSibling(); return root;
    }
    // Fractions of the parent rect, measured from the top-left.
    internal static RectTransform Element(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name); var rect=go.AddComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin=new Vector2(x,1-y-h); rect.anchorMax=new Vector2(x+w,1-y);
        rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; return rect;
    }
    internal static Text Label(Transform parent,string value,float x,float y,float w,float h,int size=40,Color? color=null)
    {
        var rect=Element(parent,"Label",x,y,w,h); var text=rect.gameObject.AddComponent<Text>();
        text.font=Font; text.fontStyle=fontStyle; text.fontSize=size; text.color=color??Color.white;
        text.alignment=TextAnchor.MiddleLeft; text.horizontalOverflow=HorizontalWrapMode.Wrap;
        text.verticalOverflow=VerticalWrapMode.Overflow; text.raycastTarget=false; text.supportRichText=false;
        text.text=value;
        var outline=rect.gameObject.AddComponent<Outline>(); outline.effectColor=Color.black; outline.effectDistance=new Vector2(1,-1);
        return text;
    }
    internal static void Choice(Transform parent,string value,float x,float y,float w,float h,bool selected,Action action,int size=44)
    {
        var rect=Element(parent,"Choice",x,y,w,h); var img=rect.gameObject.AddComponent<Image>();
        img.color=selected?new Color(.2f,.2f,.75f,1):Blue;
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=img;
        button.transition=Selectable.Transition.None;
        var nav=button.navigation; nav.mode=Navigation.Mode.None; button.navigation=nav;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)action);
        Label(rect,selected?">  "+value:"    "+value,.025f,0,.95f,1,size,selected?Color.yellow:Color.white);
    }
    internal static void Portrait(Transform parent,Sprite? sprite,float x,float y,float w,float h)
    {
        if(sprite==null) return;
        var img=Element(parent,"Portrait",x,y,w,h).gameObject.AddComponent<Image>();
        img.sprite=sprite; img.preserveAspect=true; img.raycastTarget=false;
    }
    internal static void ProgressBar(Transform parent,float x,float y,float w,float h,float progress)
    {
        var track=Element(parent,"ABP track",x,y,w,h).gameObject.AddComponent<Image>();
        track.color=new Color(.025f,.025f,.2f,1); track.raycastTarget=false;
        if(progress<=0) return;
        var fill=Element(track.transform,"ABP earned",0,0,Math.Clamp(progress,0,1),1).gameObject.AddComponent<Image>();
        fill.color=new Color(.45f,.85f,1,1); fill.raycastTarget=false;
    }
}
