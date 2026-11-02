using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// "menu" steps with a formation: Menu -> Order, swap members into the step's
// slot order (0 = leave empty, e.g. for Yang to join into), then fix rows so
// only the step's front slots are in front (user: never in battle). Keys only;
// the result is read back from UserDataManager.corpsList (order = slots,
// Corps.Id Front 1 / Back 2). StatusWindowController: SortSourceSelect 3 picks
// a member (selectCursor), SortTargetSelect 4 the slot to swap with
// (targetSelectCursor); the same member as source and target changes rows
// (user: press A twice on a portrait).
internal sealed class FormationMenu
{
    private const int MenuMagic=2,MenuSort=5,SortSource=3,SortTarget=4,Front=1,MaxActions=60;
    private const int CecilDark=1,CecilPaladin=13,OrderBelowMagic=3;
    private FormationOrder goal=new();
    private int actions;
    private MainMenuController? main;
    internal void Reset(FormationOrder target) {goal=target;actions=0;main=null;}
    internal void Read(Snapshot s,int frame)
    {
        var slots=Slots(out var rows);
        // Order first: put each wanted member into its slot.
        int source=-1,dest=-1;
        for(int i=0;i<goal.Order.Length && i<slots.Length;i++)
        {
            int want=goal.Order[i];
            if(want==0 || slots[i]==want)continue;
            int at=Array.IndexOf(slots,want);
            if(at<0)continue;
            source=at;dest=i;break;
        }
        // Then rows: a member (or slot) on the wrong side.
        int wrongRow=-1;
        for(int i=0;i<slots.Length && source<0;i++)
        {
            bool front=Array.IndexOf(goal.Front,i)>=0;
            if((rows[i]==Front)!=front && slots[i]!=0) {wrongRow=i;break;}
        }
        bool done=source<0 && wrongRow<0;
        s.Detail=$"Formation: {string.Join(",",slots)} rows {string.Join("",rows)}";
        if((done || actions>=MaxActions) && s.Phase=="Player") {s.MenuDone=true;return;}
        if(s.Phase=="Player") {Pulse(s,frame,"Menu");return;}
        if(s.Phase!="Menu")return;
        if(done || actions>=MaxActions) {Pulse(s,frame,"Back");return;}
        if(main==null || !main)main=UnityEngine.Object.FindObjectOfType<MainMenuController>();
        if(main?.stateMachine==null)return;
        int menuState=(int)main.stateMachine.Current;
        if(menuState==0)
        {
            var commands=main.commandMenuController;
            if(commands?.contents==null || commands.selectCursor==null)return;
            int want=-1,magic=-1;
            for(int i=0;i<commands.contents.Count;i++)
            {
                int id=(int)(commands.contents[i]?.Data?.Id??0);
                if(id==MenuSort)want=i;
                if(id==MenuMagic)magic=i;
            }
            // User: Order is three below Magic.
            if(want<0 && magic>=0)want=magic+OrderBelowMagic;
            if(want<0) {actions=MaxActions;return;}
            int at=commands.selectCursor.Index;
            s.Detail+=$"; menu cursor {at} -> {want}";
            Pulse(s,frame,at==want?"Confirm":want<at?"Up":"Down");return;
        }
        var status=UnityEngine.Object.FindObjectOfType<StatusWindowController>();
        int state=status?.stateMachine!=null?(int)status.stateMachine.Current:0;
        // Items, Magic, Equipment or Status opened instead: back out. Anything
        // else may be the Order screen still opening: wait.
        if(state is not (SortSource or SortTarget) && menuState is >=1 and <=4) {Pulse(s,frame,"Back");return;}
        if(state==SortSource)
        {
            int at=status!.selectCursor?.Index??-1;if(at<0)return;
            int want=source>=0?source:wrongRow;
            if(at!=want) {Pulse(s,frame,want<at?"Up":"Down");return;}
            Count(frame);Pulse(s,frame,"Confirm");return;
        }
        if(state==SortTarget)
        {
            int at=status!.targetSelectCursor?.Index??-1;if(at<0)return;
            if(dest<0)dest=wrongRow;
            if(dest<0) {Pulse(s,frame,"Back");return;}
            if(at!=dest) {Pulse(s,frame,dest<at?"Up":"Down");return;}
            Count(frame);Pulse(s,frame,"Confirm");return;
        }
    }
    private void Count(int frame) {if(frame%12==0)actions++;}
    // character_status id (Cecil as 1) and row per corps slot (0 = empty).
    private static int[] Slots(out int[] rows)
    {
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        var ids=new int[corps?.Count??0];rows=new int[ids.Length];
        for(int c=0;corps!=null && owned!=null && c<corps.Count;c++)
        {
            rows[c]=(int)(corps[c]?.Id??0);
            int who=corps[c]?.CharacterId??0;
            for(int o=0;who!=0 && o<owned.Count;o++)if(owned[o]?.Id==who) {ids[c]=owned[o].characterStatusId==CecilPaladin?CecilDark:owned[o].characterStatusId;break;}
        }
        return ids;
    }
    private static void Pulse(Snapshot s,int frame,string key)
    {
        if(frame%12==0)s.MenuInput=key;
    }
}
