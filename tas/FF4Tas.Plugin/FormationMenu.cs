using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// "menu" steps with formation: Menu -> Sort, swap members until Cecil is in
// slot 2 and Yang in slot 4, then put them in the front row and everyone else
// in the back (user: never in battle). Keys only; the result is read back from
// UserDataManager.corpsList (order = slots, Corps.Id Front 1 / Back 2).
// StatusWindowController: SortSourceSelect 3 picks a member (selectCursor),
// SortTargetSelect 4 the slot to swap with (targetSelectCursor); the same
// member as source and target changes rows (user: press A twice on a portrait).
internal sealed class FormationMenu
{
    private const int MenuSort=5,SortSource=3,SortTarget=4,Front=1,MaxActions=60;
    private const int CecilDark=1,CecilPaladin=13,Yang=8,CecilSlot=1,YangSlot=3;
    private bool finished;
    private int actions;
    private MainMenuController? main;
    internal void Reset() {finished=false;actions=0;main=null;}
    internal void Read(Snapshot s,int frame)
    {
        var slots=Slots(out var rows);
        int cecil=Array.FindIndex(slots,id=>id is CecilDark or CecilPaladin),yang=Array.IndexOf(slots,Yang);
        // Order first (only for members who are here), then rows.
        int source=-1,dest=-1;
        if(cecil>=0 && cecil!=CecilSlot && CecilSlot<slots.Length) {source=cecil;dest=CecilSlot;}
        else if(yang>=0 && yang!=YangSlot && YangSlot<slots.Length) {source=yang;dest=YangSlot;}
        int wrongRow=-1;
        for(int i=0;i<slots.Length && source<0;i++)
        {
            if(slots[i]==0)continue;
            bool front=slots[i] is CecilDark or CecilPaladin or Yang;
            if((rows[i]==Front)!=front) {wrongRow=i;break;}
        }
        bool done=source<0 && wrongRow<0;
        s.Detail=$"Formation: {string.Join(",",slots)} rows {string.Join("",rows)}";
        if((done || actions>=MaxActions) && s.Phase=="Player") {finished=true;s.MenuDone=true;return;}
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
            int want=-1;
            for(int i=0;i<commands.contents.Count;i++)if(commands.contents[i]?.Data!=null && (int)commands.contents[i].Data.Id==MenuSort) {want=i;break;}
            if(want<0) {actions=MaxActions;return;}
            int at=commands.selectCursor.Index;
            Pulse(s,frame,at==want?"Confirm":want<at?"Up":"Down");return;
        }
        if(menuState!=MenuSort) {Pulse(s,frame,"Back");return;}
        var status=UnityEngine.Object.FindObjectOfType<StatusWindowController>();
        if(status?.stateMachine==null)return;
        int state=(int)status.stateMachine.Current;
        if(state==SortSource)
        {
            int at=status.selectCursor?.Index??-1;if(at<0)return;
            int want=source>=0?source:wrongRow;
            if(at!=want) {Pulse(s,frame,want<at?"Up":"Down");return;}
            // Order swap, or a row change: picking the same member as source
            // and target swaps the rows (user).
            Count(frame);Pulse(s,frame,"Confirm");return;
        }
        if(state==SortTarget)
        {
            int at=status.targetSelectCursor?.Index??-1;if(at<0)return;
            if(dest<0)dest=wrongRow;
            if(dest<0) {Pulse(s,frame,"Back");return;}
            if(at!=dest) {Pulse(s,frame,dest<at?"Up":"Down");return;}
            Count(frame);Pulse(s,frame,"Confirm");return;
        }
    }
    private void Count(int frame) {if(frame%12==0)actions++;}
    // character_status id and row per corps slot (0 = empty).
    private static int[] Slots(out int[] rows)
    {
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        var ids=new int[corps?.Count??0];rows=new int[ids.Length];
        for(int c=0;corps!=null && owned!=null && c<corps.Count;c++)
        {
            rows[c]=(int)(corps[c]?.Id??0);
            int who=corps[c]?.CharacterId??0;
            for(int o=0;who!=0 && o<owned.Count;o++)if(owned[o]?.Id==who) {ids[c]=owned[o].characterStatusId;break;}
        }
        return ids;
    }
    private static void Pulse(Snapshot s,int frame,string key)
    {
        if(frame%12==0)s.MenuInput=key;
    }
}
