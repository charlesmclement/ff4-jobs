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
    private const int MenuEquipment=3,EquipCommand=1,Strongest=2,RemoveEverything=3,WindowCommand=1,WindowInfo=2,WindowSelect=3,AfterEquip=30,MaxSwitches=24,LapSwitches=6;
    private int[] wanted=Array.Empty<int>();
    private readonly HashSet<int> done=new(),removed=new(),strong=new();
    // Exact items after Strongest (Step.EquipItems), and which are done.
    private EquipItem[] items=Array.Empty<EquipItem>();
    private readonly HashSet<int> itemsDone=new();
    private int[] reset=Array.Empty<int>();
    private readonly HashSet<int> seen=new();private bool stepping;private int stepFrom,stepTries;
    private bool finished,sideways;
    private int wait,switches,stuck,lastIndex=-1;
    private string? lastKey;
    private MainMenuController? main;
    private EquipmentWindowController? window;
    private bool exactOnly;
    internal void Reset(int[] characters,int[]? removeFirst=null,EquipItem[]? exact=null,bool exactOnlyItems=false)
    {
        items=exact??Array.Empty<EquipItem>();itemsDone.Clear();strong.Clear();exactOnly=exactOnlyItems;
        wanted=characters;seen.Clear();stepping=false;stepFrom=stepTries=0;reset=removeFirst??Array.Empty<int>();removed.Clear();done.Clear();finished=sideways=false;wait=switches=stuck=0;lastIndex=-1;lastKey=null;main=null;window=null;
    }
    internal void Read(Snapshot s,int frame)
    {
        if(finished && s.Phase=="Player") {s.MenuDone=true;s.Detail="Equip: done";return;}
        if(wait>0) {wait--;s.Detail="Equip: equipping";return;}
        // equip [0] = everyone: Strongest on whoever the window shows, step to
        // the next member, stop once a member comes round again. Needs no slot
        // matching (the king's chamber never matched Cecil in any counting).
        bool everyone=wanted.Length==1 && wanted[0]==0;
        int next=everyone?-1:Array.Find(wanted,id=>!done.Contains(id) && InParty(id));
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
        int shown=window.targetIndex;
        if(everyone)
        {
            if(stepping)
            {
                if(shown==stepFrom)
                {
                    if(frame%12==0 && ++stepTries>LapSwitches)finished=true;
                    Pulse(s,frame,"SwitchRight",$"Next member (window {shown})");return;
                }
                stepping=false;stepTries=0;
            }
            if(seen.Contains(shown)) {finished=true;return;}
        }
        else
        {
            // Match on the character the window shows, never a slot number
            // (user: "always use character id not slot" - the slot count put
            // Yang's claws into Cecil's equipment window).
            int showing=ShownCharacter();
            if(showing!=next)
            {
                if(frame%12==0)switches++;
                Pulse(s,frame,"SwitchRight",$"Next member (#{next}: window shows #{showing})");return;
            }
        }
        // Exact-only steps never press Strongest: straight to the items.
        if(!everyone && exactOnly && !strong.Contains(next) && Array.IndexOf(reset,next)<0)strong.Add(next);
        if(!everyone && exactOnly && !strong.Contains(next) && removed.Contains(next))strong.Add(next);
        // Strongest done for this member: the exact items, one at a time
        // (Equip -> slot row -> item), then the member is finished.
        if(!everyone && strong.Contains(next))
        {
            int k=Array.FindIndex(items,e=>e.Character==next && !itemsDone.Contains(Array.IndexOf(items,e)));
            if(k<0)
            {
                if((int)window.stateMachine.Current!=WindowCommand) {Pulse(s,frame,"Back","Back to the equip commands");return;}
                done.Add(next);return;
            }
            ExactItem(s,frame,k);return;
        }
        if((int)window.stateMachine.Current!=WindowCommand) {Pulse(s,frame,"Back","Back to the equip commands");return;}
        var c=window.commandController;
        if(c?.contents==null || c.selectCursor==null)return;
        // Members in `reset` take everything off first, then Strongest. If the
        // removal asks to confirm and gets backed out, Strongest still runs.
        bool clearing=Array.IndexOf(reset,next)>=0 && !removed.Contains(next);
        int command=clearing?RemoveEverything:Strongest,target=-1;
        for(int i=0;i<c.contents.Count;i++)if(c.contents[i]?.Data!=null && (int)c.contents[i].Data.Id==command) {target=i;break;}
        if(target<0 && clearing) {removed.Add(next);return;}
        if(target<0) {finished=true;return;}
        int cursor=c.selectCursor.Index;
        if(cursor==target)
        {
            if(frame%12==0)
            {
                if(clearing)removed.Add(next);
                else if(!everyone && Array.Exists(items,e=>e.Character==next))strong.Add(next);
                else done.Add(next);
                if(everyone && !clearing) {seen.Add(shown);stepping=true;stepFrom=shown;}
                wait=AfterEquip;
            }
            Pulse(s,frame,"Confirm",(clearing?"Remove everything from #":"Strongest for ")+(everyone?$"window {shown}":"#"+next));return;
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
    private int exactPresses;
    // One exact item: Equip in the command row, the slot row in the info
    // window, the item (by content id) in the select list, Confirm.
    private void ExactItem(Snapshot s,int frame,int k)
    {
        var e=items[k];int state=(int)window!.stateMachine.Current;
        // Not in the bag: skip it without opening anything (live: the inn's
        // Thunder pair before any were bought looped in the slot rows).
        if(!Owned(e.Content)) {itemsDone.Add(k);exactPresses=0;return;}
        if(state==WindowCommand)
        {
            var c=window.commandController;
            if(c?.contents==null || c.selectCursor==null)return;
            int target=-1;
            for(int i=0;i<c.contents.Count;i++)if(c.contents[i]?.Data!=null && (int)c.contents[i].Data.Id==EquipCommand) {target=i;break;}
            if(target<0) {itemsDone.Add(k);return;}
            int cursor=c.selectCursor.Index;
            if(cursor==target) {Pulse(s,frame,"Confirm","Equip (slot pick)");return;}
            string key=sideways?(target<cursor?"Left":"Right"):(target<cursor?"Up":"Down");
            if(frame%12==0)
            {
                if(key==lastKey && cursor==lastIndex && ++stuck>=3) {sideways=!sideways;stuck=0;}
                else if(cursor!=lastIndex)stuck=0;
                lastKey=key;lastIndex=cursor;
            }
            Pulse(s,frame,key,"Equip command");return;
        }
        if(state==WindowInfo)
        {
            // Rows by their own slot type (EquipSlotType Slot1.. = slot 0..):
            // live, Down from the right hand jumped straight to row 2, so row
            // numbers aren't slots. A row that's missing, disabled or not
            // reached within a few presses is skipped.
            var info=window.infoWindowController;var cur=info?.selectCursor;var rows=info?.contentList;
            if(cur==null || rows==null)return;
            int row=-1;
            for(int i=0;i<rows.Count;i++)if(rows[i]!=null && (int)rows[i].Slot==e.Slot+1) {row=i;break;}
            int at=cur.Index;
            if(row<0 || !rows[row].IsEnable || (frame%12==0 && ++exactPresses>8)) {itemsDone.Add(k);exactPresses=0;Pulse(s,frame,"Back",$"Slot {e.Slot} not reachable (row {row})");return;}
            if(at==row) {exactPresses=0;Pulse(s,frame,"Confirm",$"Slot {e.Slot} (row {row}) for #{e.Character}");return;}
            Pulse(s,frame,row<at?"Up":"Down",$"Slot {e.Slot}: row {row}, cursor {at}");return;
        }
        if(state==WindowSelect)
        {
            var sel=window.selectWindowController;var list=sel?.ContentDataList;var cur=sel?.selectCursor;
            if(list==null || cur==null)return;
            int want=-1;
            for(int i=0;i<list.Count;i++)if(list[i]!=null && list[i].contentId==e.Content) {want=i;break;}
            // Not in the bag (or already worn there): skip it.
            if(want<0) {if(frame%12==0)itemsDone.Add(k);Pulse(s,frame,"Back",$"Item {e.Content} not in the list ({list.Count} entries)");return;}
            int at=cur.Index;
            // Equipped: the other choices for this hand are spent too (a list
            // of preferences per slot - Thunder, else Fire, else Ice Claw).
            if(at==want)
            {
                if(frame%12==0)
                {
                    for(int o=0;o<items.Length;o++)if(items[o].Character==e.Character && items[o].Slot==e.Slot)itemsDone.Add(o);
                    wait=AfterEquip;exactPresses=0;
                }
                Pulse(s,frame,"Confirm",$"Equip item {e.Content} in slot {e.Slot}");return;
            }
            // A lap without landing (cursor index isn't the list index): give up on it.
            if(frame%12==0 && ++exactPresses>2*list.Count+6) {itemsDone.Add(k);exactPresses=0;Pulse(s,frame,"Back",$"Can't reach item {e.Content}");return;}
            Pulse(s,frame,want<at?"Up":"Down",$"Item {e.Content}: list {want}, cursor {at}");return;
        }
    }
    // character_status id of the member the equipment window shows, or -1.
    private int ShownCharacter()
    {
        try {return window!.GetTargetCharacterData(window.targetIndex)?.characterStatusId??-1;}
        catch(System.Exception) {return -1;}
    }
    private static bool Owned(int content)
    {
        var owned=UserDataManager.Instance()?.normalOwnedItems;
        return owned!=null && owned.TryGetValue(content,out var entry) && entry!=null && entry.Count>0;
    }
    // Whether a character_status id is in the party (membership only - the
    // window is matched by the character it shows, never by a slot).
    private static bool InParty(int statusId)
    {
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        if(corps==null || owned==null)return false;
        for(int c=0;c<corps.Count;c++)
        {
            int who=corps[c]?.CharacterId??0;if(who==0)continue;
            for(int o=0;o<owned.Count;o++)if(owned[o]?.Id==who && owned[o].characterStatusId==statusId)return true;
        }
        return false;
    }
    private static void Pulse(Snapshot s,int frame,string key,string detail)
    {
        s.Detail="Equip: "+detail;
        if(frame%12==0)s.MenuInput=key;
    }
}
