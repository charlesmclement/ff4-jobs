using FF4Tas.Core;
using Last.Battle;
using Last.Data;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// Read the existing menus, move their cursor, then Confirm. The game validates
// commands, spends items, selects targets and calculates every action normally.
internal sealed class BattlePilot
{
    private BattleMenuController? menu;
    private int pendingItem,pendingTarget,lastInfo=-1;
    private int stalled,lastCursor=-1;
    private IntPtr lastActor;
    private int previousHp=-1;
    internal void Reset() {menu=null;pendingItem=pendingTarget=stalled=0;lastInfo=lastCursor=-1;lastActor=IntPtr.Zero;previousHp=-1;}
    internal void Read(Snapshot state,int frame)
    {
        var plug=BattlePlugManager.Instance();
        if(plug==null)return;
        if(plug.IsWinResult()) {Pulse(state,frame,"Confirm","Advance victory rewards");return;}
        var players=plug.GetPlayerUnits();var enemies=plug.GetEnemyUnits();
        if(players==null || enemies==null)return;
        BattlePlayerData? injured=null,dead=null;int alive=0,totalHp=0;
        for(int i=0;i<players.Length;i++)
        {
            var p=players[i];var stats=p?.BattleUnitDataInfo?.Parameter;if(stats==null)continue;
            int hp=stats.CurrentHP;totalHp+=hp;
            if(hp<=0) {dead??=p;continue;}
            alive++;
            if(hp<Math.Max(60,stats.ConfirmedMaxHp()*45/100) && (injured==null || hp<injured.BattleUnitDataInfo.Parameter.CurrentHP))injured=p;
        }
        if(players.Length>0 && alive==0) {state.Failure="Party defeated; run stopped";return;}
        bool mist=false,hasEnemy=false;
        for(int i=0;i<enemies.Length;i++)
        {
            var e=enemies[i];var p=e?.BattleUnitDataInfo?.Parameter;
            if(e==null || p==null || p.CurrentHP<=0 || e.IsRemove)continue;
            hasEnemy=true;
            if(e.MonsterMaster?.Id==162)
            {
                var ep=p.TryCast<EnemyCharacterParameter>();
                if(ep==null) {state.Failure="Cannot read Mist Dragon's form";return;}
                // Installed sc_ai_162_MistDragon: modes 1/3 counter attacks in mist.
                mist=ep.CurrentMode is 1 or 3;
                state.Detail=$"Mist Dragon HP {p.CurrentHP}; mode {ep.CurrentMode}";
            }
        }
        if(!hasEnemy) {Pulse(state,frame,"Confirm","Waiting for battle result");return;}
        if(menu==null || !menu)menu=UnityEngine.Object.FindObjectOfType<BattleMenuController>();
        if(menu==null || !menu.IsShow)return;
        if(menu.IsSpecialHelpPause) {Pulse(state,frame,"Confirm","Advance battle tutorial");return;}
        if(menu.IsAuto) {state.Failure="Auto-battle is enabled; disable it before restarting the route";return;}
        if(menu.IsPause) {state.Failure="Battle paused; run stopped";return;}
        var info=menu.infomationController;
        if(info?.stateMachine==null || menu.stateMachine==null || (int)menu.stateMachine.Current!=2)return;
        int stage=(int)info.stateMachine.Current;
        var actor=menu.SelectedTarget;
        if(actor==null)return;
        int cursor=stage==1?info.commandSelectController?.selectCursor?.Index??-1:
                   stage==2?info.itemSelectController?.selectCursor?.Index??-1:
                   info.targetSelectController?.selectCursor?.Index??-1;
        if(stage!=lastInfo || actor.Pointer!=lastActor || cursor!=lastCursor || totalHp!=previousHp)stalled=0;
        else if(++stalled>3600) {state.Failure=$"Battle menu stalled (state {stage}, cursor {cursor})";return;}
        lastInfo=stage;lastActor=actor.Pointer;lastCursor=cursor;previousHp=totalHp;
        switch(stage)
        {
            case 1:
                pendingItem=0;pendingTarget=0;
                // Native content IDs: Phoenix Down = 9, Potion = 2.
                var victim=dead!=null && HasItem(9)?dead:injured!=null && HasItem(2)?injured:null;
                if(victim!=null)
                {pendingItem=victim==dead?9:2;pendingTarget=victim.GetOwnedCharacterData().characterStatusId;}
                SelectCommand(info,pendingItem!=0?3:mist?2:1,state,frame);
                break;
            case 2:
                if(pendingItem==0) {Pulse(state,frame,"Back","Cancel unexpected item list");break;}
                SelectItem(info,pendingItem,state,frame);break;
            case 3:
                SelectHealingTarget(info,state,frame);break;
            case 6:
                if(mist) {Pulse(state,frame,"Back","Cancel attack: dragon became mist");break;}
                var t=info.targetSelectController;
                if(t?.stateMachine==null)break;
                if((int)t.stateMachine.Current!=3) {Pulse(state,frame,"Left","Select enemy side");break;}
                var selected=t.GetEnemyTarget();
                if(selected!=null && selected.Count==1 && selected[0].BattleUnitDataInfo.Parameter.CurrentHP>0)
                    Pulse(state,frame,"Confirm","Attack selected enemy");
                else Pulse(state,frame,"Down","Select living enemy");
                break;
            case 0:break;
            default:Pulse(state,frame,"Back","Return to supported battle commands");break;
        }
    }
    private static bool HasItem(int contentId)
    {
        var items=UserDataManager.Instance()?.normalOwnedItems;
        return items!=null && items.TryGetValue(contentId,out var item) && item!=null && item.Count>0;
    }
    private static void SelectCommand(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        var c=info.commandSelectController;if(c?.selectCursor==null || c.stateMachine==null)return;
        int group=(int)c.stateMachine.Current;
        if(wanted==2 && group!=3) {Pulse(state,frame,"Right","Open Defend commands");return;}
        if(wanted!=2 && group!=2) {Pulse(state,frame,group==3?"Left":"Right","Return to normal commands");return;}
        int index=c.selectCursor.Index;
        if(c.contentList==null || index<0 || index>=c.contentList.Count)return;
        var selected=c.contentList[index];
        if(selected?.TargetCommand?.Id==wanted && selected.IsEnable)
        {Pulse(state,frame,"Confirm",wanted==2?"Defend during mist":wanted==3?"Use recovery item":"Choose Attack");return;}
        int wantedIndex=-1;
        for(int i=0;i<c.contentList.Count;i++)if(c.contentList[i]?.TargetCommand?.Id==wanted) {wantedIndex=i;break;}
        if(wantedIndex<0) {state.Failure=$"Required native command {wanted} not visible; stop instead of guessing";return;}
        Pulse(state,frame,wantedIndex<index?"Up":"Down","Select battle command");
    }
    private static void SelectItem(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        var c=info.itemSelectController;if(c?.selectCursor==null || c.displayDataList==null)return;
        int index=c.selectCursor.Index,desired=-1;
        for(int i=0;i<c.displayDataList.Count;i++)
            if(c.displayDataList[i]?.contentId==wanted && c.displayDataList[i].Count>0) {desired=i;break;}
        if(desired<0) {Pulse(state,frame,"Back","Recovery item unavailable");return;}
        if(index==desired) {Pulse(state,frame,"Confirm","Choose recovery item");return;}
        // Native normal item grid has two columns (NormalItemSkipCount = 2).
        Pulse(state,frame,index%2!=desired%2?(desired%2==0?"Left":"Right"):(desired<index?"Up":"Down"),"Find recovery item");
    }
    private void SelectHealingTarget(BattleInfomationController info,Snapshot state,int frame)
    {
        var c=info.targetSelectController;if(c?.stateMachine==null)return;
        if(pendingItem==0 || pendingTarget==0) {Pulse(state,frame,"Back","Cancel unknown healing target");return;}
        if((int)c.stateMachine.Current!=1) {Pulse(state,frame,"Right","Select party side");return;}
        var targets=c.GetPlayerTarget();
        if(targets!=null && targets.Count==1 && targets[0].GetOwnedCharacterData()?.characterStatusId==pendingTarget)
            Pulse(state,frame,"Confirm","Heal selected party member");
        else Pulse(state,frame,"Down","Find injured party member");
    }
    private static void Pulse(Snapshot state,int frame,string key,string detail)
    {state.Detail=string.IsNullOrEmpty(state.Detail)?detail:state.Detail+"; "+detail;if(frame%12==0)state.BattleInput=key;}
}
