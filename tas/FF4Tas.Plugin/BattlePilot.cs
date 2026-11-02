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
    private int pendingItem,pendingTarget,pendingCommand=1,pendingAbility,lastInfo=-1;
    // Ability lists are grids of unknown width; learn the row stride from the
    // first vertical move instead of assuming the item grid's two columns.
    private int abilityStride,abilityLastIndex=-1;
    private string? abilityLastKey;
    // Plan rules that failed this battle (spell missing or not castable, e.g.
    // out of MP) are skipped until the next battle so the menu never loops.
    private readonly HashSet<int> blockedRules=new();
    private int pendingRule=-1,abilityConfirms;
    private int stalled,lastCursor=-1;
    private IntPtr lastActor;
    private int previousHp=-1;
    internal void Reset() {menu=null;pendingItem=pendingTarget=pendingAbility=stalled=abilityStride=0;pendingCommand=1;abilityLastIndex=-1;abilityLastKey=null;lastInfo=lastCursor=-1;lastActor=IntPtr.Zero;previousHp=-1;}
    internal void Read(Snapshot state,int frame,BattleRule[]? plan=null)
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
                   stage==4?info.abilityController?.selectCursorIndex??-1:
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
                pendingCommand=1;pendingAbility=0;
                if(pendingItem==0 && !mist)ChoosePlan(info,actor,plan);
                SelectCommand(info,pendingItem!=0?3:mist?2:pendingCommand,state,frame);
                break;
            case 4:
                if(pendingAbility==0) {Block();Pulse(state,frame,"Back","Cancel unexpected ability list");break;}
                SelectAbility(info,state,frame);break;
            case 5:
                SelectEnemy(info,state,frame,"Cast on selected enemy");break;
            case 2:
                if(pendingItem==0) {Pulse(state,frame,"Back","Cancel unexpected item list");break;}
                SelectItem(info,pendingItem,state,frame);break;
            case 3:
                SelectHealingTarget(info,state,frame);break;
            case 6:
                if(mist) {Pulse(state,frame,"Back","Cancel attack: dragon became mist");break;}
                SelectEnemy(info,state,frame,"Attack selected enemy");break;
            case 0:break;
            default:Pulse(state,frame,"Back","Return to supported battle commands");break;
        }
    }
    private static void SelectEnemy(BattleInfomationController info,Snapshot state,int frame,string action)
    {
        var t=info.targetSelectController;
        if(t?.stateMachine==null)return;
        if((int)t.stateMachine.Current!=3) {Pulse(state,frame,"Left","Select enemy side");return;}
        var selected=t.GetEnemyTarget();
        // Whole-side targeting (e.g. Darkness) selects every living enemy.
        bool living=selected!=null && selected.Count>0;
        for(int i=0;living && i<selected!.Count;i++)living=selected[i].BattleUnitDataInfo.Parameter.CurrentHP>0;
        if(living)Pulse(state,frame,"Confirm",action);
        else Pulse(state,frame,"Down","Select living enemy");
    }
    // First plan rule for this actor whose command is visible and enabled and
    // whose HP condition holds. Ability availability is checked in the list.
    private void ChoosePlan(BattleInfomationController info,BattlePlayerData actor,BattleRule[]? plan)
    {
        if(plan==null)return;
        var stats=actor.BattleUnitDataInfo?.Parameter;
        int id=actor.GetOwnedCharacterData()?.characterStatusId??0;
        for(int r=0;r<plan.Length;r++)
        {
            var rule=plan[r];
            if(blockedRules.Contains(r))continue;
            if(rule.Character!=null && rule.Character!=id)continue;
            if(stats!=null && rule.MinHpPercent>0 && stats.CurrentHP*100<rule.MinHpPercent*Math.Max(1,stats.ConfirmedMaxHp()))continue;
            if(stats!=null && rule.MinMp>0 && stats.CurrentMP<rule.MinMp)continue;
            if(!CommandEnabled(info,rule.Command))continue;
            pendingCommand=rule.Command;pendingAbility=rule.Ability??0;pendingRule=r;abilityConfirms=0;abilityStride=0;abilityLastIndex=-1;abilityLastKey=null;
            return;
        }
    }
    private void Block() {if(pendingRule>=0)blockedRules.Add(pendingRule);pendingRule=-1;pendingAbility=0;}
    private static bool CommandEnabled(BattleInfomationController info,int id)
    {
        var list=info.commandSelectController?.contentList;
        if(list==null)return false;
        for(int i=0;i<list.Count;i++)if(list[i]?.TargetCommand?.Id==id)return list[i].IsEnable;
        return false;
    }
    private void SelectAbility(BattleInfomationController info,Snapshot state,int frame)
    {
        var baseController=info.abilityController;
        var list=baseController?.TryCast<Serial.FF0.UI.KeyInput.BattleQuantityAbilityInfomationController>()?.dataList;
        if(baseController==null || list==null)return;
        int index=baseController.selectCursorIndex,desired=-1;
        for(int i=0;i<list.Count;i++)if(list[i]?.Ability?.Id==pendingAbility) {desired=i;break;}
        // Not learned (or no list): back out; the next command pass falls through to Attack.
        if(desired<0 || abilityConfirms>3) {Block();Pulse(state,frame,"Back","Planned ability not available");return;}
        if(frame%12!=0) {state.Detail="Find planned ability";return;}
        if(abilityLastKey is "Down" or "Up" && abilityStride==0 && abilityLastIndex>=0)
            abilityStride=index!=abilityLastIndex?Math.Abs(index-abilityLastIndex):int.MaxValue;
        abilityLastIndex=index;
        string key;
        if(index==desired) {key="Confirm";abilityConfirms++;}
        else if(abilityStride==0)key=desired>index?"Down":"Up";
        else if(abilityStride==int.MaxValue || index/abilityStride==desired/abilityStride)key=desired>index?"Right":"Left";
        else key=desired>index?"Down":"Up";
        abilityLastKey=key;
        Pulse(state,frame,key,key=="Confirm"?"Choose planned ability":"Find planned ability");
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
