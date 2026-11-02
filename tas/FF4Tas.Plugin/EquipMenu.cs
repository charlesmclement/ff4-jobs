using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// "menu" steps with equip: Menu -> Equipment -> for each listed character
// the native "Strongest" command (Last.UI.EquipmentCommandId 2), then Back
// to the field. Keys only. The equipment window's targetIndex is the party
// slot it shows; SwitchRight steps to the next member.
internal sealed class EquipMenu
{
    private const int MenuEquipment=3,Strongest=2,WindowCommand=1,AfterEquip=30,MaxSwitches=12;
    private int[] wanted=Array.Empty<int>();
    private readonly HashSet<int> done=new();
    private bool finished,sideways;
    private int wait,switches,stuck,lastIndex=-1;
    private string? lastKey;
    private MainMenuController? main;
    private EquipmentWindowController? window;
    internal void Reset(int[] characters)
    {
        wanted=characters;done.Clear();finished=sideways=false;wait=switches=stuck=0;lastIndex=-1;lastKey=null;main=null;window=null;
    }
    internal void Read(Snapshot s,int frame)
    {
        if(finished && s.Phase=="Player") {s.MenuDone=true;s.Detail="Equip: done";return;}
        if(wait>0) {wait--;s.Detail="Equip: equipping";return;}
        int next=Array.Find(wanted,id=>!done.Contains(id) && Slot(id)>=0);
        if(next==0 || switches>MaxSwitches)finished=true;
        if(s.Phase=="Player")
        {
            if(finished) {s.MenuDone=true;return;}
            Pulse(s,frame,"Menu","Open the menu to equip");return;
        }
        if(s.Phase!="Menu")return;
        if(finished) {Pulse(s,frame,"Back","Close the menu");return;}
        if(main==null || !main)main=UnityEngine.Object.FindObjectOfType<MainMenuController>();
        if(main?.stateMachine==null)return;
        int menuState=(int)main.stateMachine.Current;
        if(menuState==0)
        {
            var commands=main.commandMenuController;
            if(commands?.contents==null || commands.selectCursor==null)return;
            int want=-1;
            for(int i=0;i<commands.contents.Count;i++)if(commands.contents[i]?.Data!=null && (int)commands.contents[i].Data.Id==MenuEquipment) {want=i;break;}
            if(want<0) {finished=true;return;}
            int at=commands.selectCursor.Index;
            Pulse(s,frame,at==want?"Confirm":want<at?"Up":"Down","Equipment");return;
        }
        if(menuState!=MenuEquipment) {Pulse(s,frame,"Back","Leave this submenu");return;}
        if(window==null || !window)window=UnityEngine.Object.FindObjectOfType<EquipmentWindowController>();
        // No equipment window yet: the party list picks who to equip; the
        // window then switches members itself.
        if(window==null || !window.gameObject.activeInHierarchy || window.stateMachine==null) {Pulse(s,frame,"Confirm","Pick a member");return;}
        int slot=Slot(next),shown=window.targetIndex;
        if(shown!=slot)
        {
            if(frame%12==0)switches++;
            Pulse(s,frame,"SwitchRight","Next member (#"+next+")");return;
        }
        if((int)window.stateMachine.Current!=WindowCommand) {Pulse(s,frame,"Back","Back to the equip commands");return;}
        var c=window.commandController;
        if(c?.contents==null || c.selectCursor==null)return;
        int target=-1;
        for(int i=0;i<c.contents.Count;i++)if(c.contents[i]?.Data!=null && (int)c.contents[i].Data.Id==Strongest) {target=i;break;}
        if(target<0) {finished=true;return;}
        int cursor=c.selectCursor.Index;
        if(cursor==target)
        {
            if(frame%12==0) {done.Add(next);wait=AfterEquip;}
            Pulse(s,frame,"Confirm","Strongest for #"+next);return;
        }
        // The command row may be horizontal: flip axes if a key doesn't move it.
        string key=sideways?(target<cursor?"Left":"Right"):(target<cursor?"Up":"Down");
        if(frame%12==0)
        {
            if(key==lastKey && cursor==lastIndex && ++stuck>=3) {sideways=!sideways;stuck=0;}
            else if(cursor!=lastIndex)stuck=0;
            lastKey=key;lastIndex=cursor;
        }
        Pulse(s,frame,key,"Strongest");
    }
    // Party slot (corps order) of a character_status id (Cecil 1, Yang 8),
    // or -1 when not in the party. Corps entries hold the owned-character id.
    private static int Slot(int statusId)
    {
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        if(corps==null || owned==null)return -1;
        int n=0;
        for(int c=0;c<corps.Count;c++)
        {
            int who=corps[c]?.CharacterId??0;if(who==0)continue;
            for(int o=0;o<owned.Count;o++)if(owned[o]?.Id==who && owned[o].characterStatusId==statusId)return n;
            n++;
        }
        return -1;
    }
    private static void Pulse(Snapshot s,int frame,string key,string detail)
    {
        s.Detail="Equip: "+detail;
        if(frame%12==0)s.MenuInput=key;
    }
}
