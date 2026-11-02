using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// "menu" steps: heal the party out of battle through the native field menu.
// Like the battle pilot it only presses keys: Menu opens the main menu, then
// Items -> Use -> item -> target and Confirm; the game spends the items.
// Phoenix Down on fallen members first, then Potions on anyone under 70% HP,
// then Back until the field is back. Reads the menu's own state machines and
// cursors (Last.UI.KeyInput.MainMenuController / ItemWindowController).
internal sealed class FieldMenu
{
    private const int Potion=2,PhoenixDown=9,MaxUses=12,AfterUse=48,Rosa=3;
    private const int MenuItem=1,ItemUse=1;
    // ItemWindowController.State and ItemUseController.State.
    private const int CommandSelect=1,UseSelect=2,TargetSelect=5,UseAll=2;
    private float topUpBelow=.7f;
    private bool finished,oneColumn;
    private int uses,wait,lastIndex=-1,stuck,item,target;
    private string? lastKey;
    private string party="";
    private MainMenuController? main;
    private ItemWindowController? items;
    // topUp: Potion anyone under this share of max HP (.7 for the route's
    // menu steps; lower between fights so the run doesn't menu after each one).
    internal void Reset(float topUp=.7f)
    {
        topUpBelow=topUp;finished=oneColumn=false;uses=wait=stuck=item=target=0;lastIndex=-1;lastKey=null;main=null;items=null;
    }
    internal void Read(Snapshot s,int frame)
    {
        // Every frame: deciding "nobody to heal" before the first check ended
        // the step on its first frame without looking at the party.
        Need();
        if(finished && s.Phase=="Player") {s.MenuDone=true;s.Detail="Menu: party healed";return;}
        if(wait>0) {wait--;s.Detail="Menu: item in use";return;}
        if(s.Phase=="Player")
        {
            if(item==0) {finished=true;s.MenuDone=true;s.Detail="Menu: nobody to heal ("+party+")";return;}
            Pulse(s,frame,"Menu","Open the menu to heal");return;
        }
        if(s.Phase!="Menu")return;
        if(item==0 || uses>=MaxUses) {finished=true;Pulse(s,frame,"Back","Close the menu");return;}
        if(main==null || !main)main=UnityEngine.Object.FindObjectOfType<MainMenuController>();
        if(main?.stateMachine==null)return;
        int menuState=(int)main.stateMachine.Current;
        if(menuState==0)
        {
            var commands=main.commandMenuController;
            if(commands?.contents==null || commands.selectCursor==null)return;
            int want=-1;
            for(int i=0;i<commands.contents.Count;i++)if(commands.contents[i]?.Data!=null && (int)commands.contents[i].Data.Id==MenuItem) {want=i;break;}
            if(want<0) {finished=true;Pulse(s,frame,"Back","No Items command");return;}
            Step(s,frame,commands.selectCursor.Index,want,"Up","Down","Items");return;
        }
        if(menuState!=MenuItem) {Pulse(s,frame,"Back","Leave this submenu");return;}
        if(items==null || !items)items=UnityEngine.Object.FindObjectOfType<ItemWindowController>();
        if(items?.stateMachine==null)return;
        switch((int)items.stateMachine.Current)
        {
            case CommandSelect:
            {
                var c=items.commandController;
                if(c?.contentList==null || c.selectCursor==null)return;
                int want=-1;
                for(int i=0;i<c.contentList.Count;i++)if(c.contentList[i]?.Data!=null && (int)c.contentList[i].Data.Id==ItemUse) {want=i;break;}
                if(want<0)want=0;
                Step(s,frame,c.selectCursor.Index,want,"Left","Right","Use");return;
            }
            case UseSelect:
            {
                var list=items.itemListController;
                if(list?.dataList==null || list.selectCursor==null)return;
                int want=IndexOf(list.dataList,item);
                if(want<0) {item=0;Pulse(s,frame,"Back","Item not in the list");return;}
                int at=list.selectCursor.Index;
                // Two columns like the battle item grid; if a key doesn't move
                // the cursor, fall back to a single column.
                string key=at==want?"Confirm":oneColumn?(want<at?"Up":"Down"):at%2!=want%2?(want%2==0?"Left":"Right"):(want<at?"Up":"Down");
                if(frame%12==0 && key!="Confirm")
                {
                    if(key==lastKey && at==lastIndex && ++stuck>=3) {oneColumn=!oneColumn;stuck=0;}
                    else if(at!=lastIndex)stuck=0;
                    lastKey=key;lastIndex=at;
                }
                Pulse(s,frame,key,item==PhoenixDown?"Phoenix Down":"Potion");return;
            }
            case TargetSelect:
            {
                var use=items.itemUseController;
                if(use?.targetList==null || use.selectCursor==null)return;
                if(use.selectedItemData!=null && use.selectedItemData.contentId!=item) {Pulse(s,frame,"Back","Switch item");return;}
                if(use.stateMachine!=null && (int)use.stateMachine.Current==UseAll)
                {
                    if(frame%12==0) {uses++;wait=AfterUse;}
                    Pulse(s,frame,"Confirm","Use on the party");return;
                }
                int want=-1;
                for(int i=0;i<use.targetList.Count;i++)if(use.targetList[i]?.Id==target) {want=i;break;}
                if(want<0) {Pulse(s,frame,"Back","Target not in the list");return;}
                int at=use.selectCursor.Index;
                if(at==want && frame%12==0) {uses++;wait=AfterUse;}
                Step(s,frame,at,want,"Up","Down","Use on #"+target);return;
            }
            default:Pulse(s,frame,"Back","Back to the item commands");return;
        }
    }
    // Something to use right now (checked before opening the menu).
    internal bool Wanted() {Need();return item!=0;}
    // Phoenix Down on the first fallen member, else a Potion on the lowest
    // member under 70%. Party order comes from the corps list.
    private void Need()
    {
        item=target=0;
        var user=UserDataManager.Instance();
        var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        if(corps==null || owned==null)return;
        float lowest=topUpBelow;int dead=0,hurt=0;
        var seen=new System.Text.StringBuilder();
        for(int c=0;c<corps.Count;c++)
        {
            int id=corps[c]?.CharacterId??0;if(id==0)continue;
            for(int o=0;o<owned.Count;o++)
            {
                var member=owned[o];var p=member?.Parameter;
                if(member==null || p==null || member.Id!=id)continue;
                seen.Append(seen.Length>0?", ":"").Append(member.Name).Append(' ').Append(p.CurrentHP).Append('/').Append(p.ConfirmedMaxHp());
                // Rosa (the healer) is revived first, then the party order.
                if(p.CurrentHP<=0) {if(dead==0 || id==Rosa)dead=id;break;}
                float ratio=p.CurrentHP/(float)Math.Max(1,p.ConfirmedMaxHp());
                if(ratio<lowest) {lowest=ratio;hurt=id;}
                break;
            }
        }
        party=$"{seen}; Potions {Count(Potion)}, Phoenix Downs {Count(PhoenixDown)}";
        if(dead!=0 && Has(PhoenixDown)) {item=PhoenixDown;target=dead;}
        else if(hurt!=0 && Has(Potion)) {item=Potion;target=hurt;}
    }
    private static bool Has(int contentId)=>Count(contentId)>0;
    private static int Count(int contentId)
    {
        var owned=UserDataManager.Instance()?.normalOwnedItems;
        return owned!=null && owned.TryGetValue(contentId,out var entry) && entry!=null?entry.Count:0;
    }
    private static int IndexOf(Il2CppSystem.Collections.Generic.IEnumerable<Last.UI.ItemListContentData> data,int contentId)
    {
        var list=data.TryCast<Il2CppSystem.Collections.Generic.List<Last.UI.ItemListContentData>>();
        if(list!=null)
        {
            for(int i=0;i<list.Count;i++)if(list[i]?.contentId==contentId && list[i].Count>0)return i;
            return -1;
        }
        var e=data.GetEnumerator();var walk=e?.TryCast<Il2CppSystem.Collections.IEnumerator>();
        for(int i=0;walk!=null && walk.MoveNext();i++)if(e!.Current?.contentId==contentId && e.Current.Count>0)return i;
        return -1;
    }
    private static void Step(Snapshot s,int frame,int at,int want,string prev,string next,string what)
    {
        Pulse(s,frame,at==want?"Confirm":want<at?prev:next,what);
    }
    private static void Pulse(Snapshot s,int frame,string key,string detail)
    {
        s.Detail="Menu: "+detail;
        if(frame%12==0)s.MenuInput=key;
    }
}
