using HarmonyLib;
using Last.Data.User;
using Last.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FF4Jobs;
internal static class PartyClassLabels
{
    private sealed class Entry { internal Text Source=null!,Replacement=null!; internal OwnedCharacterData Character=null!; }
    private static readonly Dictionary<IntPtr,Entry> labels=new();
    private static readonly Dictionary<int,MenuCharacterController> portraits=new();
    internal static void TrackPortrait(MenuCharacterController view,OwnedCharacterData data,bool isFace)
    { if(isFace && data!=null) portraits[data.characterStatusId]=view; }
    internal static Sprite? Portrait(int status) => portraits.TryGetValue(status,out var view) && view!=null?view.CharacterImageView?.sprite:null;
    internal static string MainName(OwnedCharacterData c)
    {
        var p=State.Campaign.Character(c.characterStatusId);
        return p.Main=="original"?c.characterStatusId switch {
            1=>"Dark Knight",2=>"Dragoon",3=>"White Mage",4=>"Summoner",5=>"Engineer",6=>"Sage",7=>"Bard",8=>"Monk",
            9=>"Black Mage",10=>"White Mage",11=>"Ninja",12=>"Sage",13=>"Paladin",_=>"Original"
        }:Core.Jobs.Get(p.Main).Name;
    }
    internal static string Description(OwnedCharacterData c)
    { var p=State.Campaign.Character(c.characterStatusId); return MainName(c)+" / "+(p.Sub==null?"None":Core.Jobs.Get(p.Sub).Name); }
    internal static void Track(Text source,OwnedCharacterData data)
    {
        if(!Plugin.Enabled || source==null || data==null) return;
        // Only the main party screen; don't alter save-slot or equipment class text.
        if(source.GetComponentInParent<Last.UI.KeyInput.MainMenuController>()==null) return;
        if(!labels.TryGetValue(source.Pointer,out var entry) || entry.Source==null || entry.Replacement==null)
        {
            var rect=MenuSkin.Element(source.transform,"Job class blue mask",0,0,1,1);
            var image=rect.gameObject.AddComponent<Image>(); image.color=MenuSkin.Blue; image.raycastTarget=false;
            var text=MenuSkin.Element(rect,"Job and subjob",0,0,1,1).gameObject.AddComponent<Text>();
            text.font=source.font; text.fontStyle=source.fontStyle; text.fontSize=source.fontSize;
            text.alignment=source.alignment; text.color=source.color; text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow;
            var outline=text.gameObject.AddComponent<Outline>(); outline.effectColor=Color.black; outline.effectDistance=new Vector2(1,-1);
            entry=new Entry{Source=source,Replacement=text}; labels[source.Pointer]=entry;
        }
        entry.Character=data; entry.Replacement.text=Description(data);
    }
    internal static void Refresh()
    {
        foreach(var pair in labels.ToArray())
        {
            var e=pair.Value;
            if(e.Source==null || e.Replacement==null) { labels.Remove(pair.Key); continue; }
            if(e.Character!=null) e.Replacement.text=Description(e.Character);
        }
    }
}
[HarmonyPatch(typeof(CharacterStatusProvider),nameof(CharacterStatusProvider.SettingStatusText))]
internal static class PartyClassTextPatch
{
    static void Postfix(OwnedCharacterData data,Text jobName) => PartyClassLabels.Track(jobName,data);
}
[HarmonyPatch(typeof(MenuCharacterController),nameof(MenuCharacterController.SetData))]
internal static class CapturePartyPortrait
{
    static void Postfix(MenuCharacterController __instance,OwnedCharacterData data,bool isFace) => PartyClassLabels.TrackPortrait(__instance,data,isFace);
}
