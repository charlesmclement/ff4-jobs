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
    private int pendingRule=-1,abilityConfirms,abilityStuck;
    private int stalled,lastCursor=-1;
    private IntPtr lastActor;
    private int previousHp=-1;
    // Called whenever the game leaves the Battle phase: everything below is per fight.
    internal void Reset()
    {
        // One line per fight (benchmark: game seconds, Rosa's shots at flyers).
        if(fightSeconds>0)
            try {File.AppendAllText(Path.Combine(Plugin.DirectoryPath,"fights.log"),$"{DateTime.Now:HH:mm:ss}\t{fightSeconds:0.0}s\taims {flyerAims}\t{enemyLine}\n");} catch {}
        flyerAims=0;
        menu=null;pendingItem=pendingTarget=pendingAbility=stalled=abilityStride=0;pendingCommand=1;
        abilityLastIndex=-1;abilityLastKey=null;lastInfo=lastCursor=-1;lastActor=IntPtr.Zero;previousHp=-1;
        pendingRule=-1;abilityConfirms=abilityStuck=targetPresses=0;decision="";
        blockedRules.Clear();blockedCure.Clear();blockedLife.Clear();blockedItems.Clear();blockedStatusSpell.Clear();usedOnce.Clear();wasMist=false;
        fleeFrames=0;sidePresses=0;unreachable.Clear();fellThisFight.Clear();targetPresses=0;bossSeen=false;turnActor=IntPtr.Zero;turnRule=-1;fightSeconds=0;fightFrame=-1;rowDone=false;firstSprite.Clear();grown=false;startSprite.Clear();changed=false;changeVotes=0;spriteLine="";queuedHeals.Clear();killStage=0;killSeen.Clear();
    }
    internal void Read(Snapshot state,int frame,BattleRule[]? plan=null,int[]? fleeMonsters=null,bool grinding=false,bool story=false)
    {
        var plug=BattlePlugManager.Instance();
        if(plug==null)return;
        // Game-time length of this fight (unaffected by the speed boost).
        if(fightFrame!=frame) {fightFrame=frame;fightSeconds+=Time.deltaTime;}
        if(plug.IsWinResult()) {Pulse(state,frame,"Confirm","Advance victory rewards");return;}
        var players=plug.GetPlayerUnits();var enemies=plug.GetEnemyUnits();
        if(players==null || enemies==null)return;
        // Heal anyone under half HP. Once Rosa is in the party, Rosa, Cecil and
        // Yang come first (lowest of them), then Rydia and Edward.
        // Without Rosa: lowest percentage first, Cecil wins near-ties.
        BattlePlayerData? injured=null,dead=null;int alive=0,totalHp=0;float injuredRatio=2;casterCanCure=false;injuredRatioNow=1;palomMp=poromMp=0;toad=mini=null;afflicted=null;afflictedRank=int.MaxValue;afflictedStatus=0;
        rosaParty=rosaCanCure=edwardCanItem=false;rydiaMp=-1;rosaTarget=rosaLow=null;float rosaKey=99;
        for(int i=0;i<players.Length;i++)if(players[i]?.GetOwnedCharacterData()?.characterStatusId==Rosa)rosaParty=true;
        // A heal or revive someone else already queued on a member makes that
        // member nobody else's target until it lands (user: no double heals,
        // Rosa Cured herself right after Rydia Cured her).
        if(menu==null || !menu)menu=UnityEngine.Object.FindObjectOfType<BattleMenuController>();
        var deciding=(menu!=null && menu.IsShow)?menu.SelectedTarget?.Pointer??IntPtr.Zero:IntPtr.Zero;
        hpByStatus.Clear();
        for(int i=0;i<players.Length;i++)
        {
            var p=players[i];var stats=p?.BattleUnitDataInfo?.Parameter;if(stats==null)continue;
            int hp=stats.CurrentHP;totalHp+=hp;
            int rank=HealRank(p!);
            int member=p!.GetOwnedCharacterData()?.characterStatusId??0;
            hpByStatus[member]=hp;
            if(queuedHeals.TryGetValue(member,out var queued) && (hp>queued.Hp || fightSeconds-queued.At>HealLandSeconds))queuedHeals.Remove(member);
            bool taken=queuedHeals.TryGetValue(member,out queued) && queued.Healer!=deciding;
            if(hp<=0) {fellThisFight.Add(member);if(rank>=0 && !taken && (dead==null || rank<HealRank(dead)))dead=p;continue;}
            alive++;
            float ratio=hp/(float)Math.Max(1,stats.ConfirmedMaxHp());
            int who=p!.GetOwnedCharacterData()?.characterStatusId??0;
            // Toad/Mini stop casting and last after the fight: cure them first.
            var conditions=stats.CurrentConditionList;bool away=unreachable.Contains(who);
            for(int c=0;conditions!=null && c<conditions.Count;c++)
            {
                int cid=conditions[c]?.Id??0;
                // Hidden (Edward's Hide, 32) or in the air (Jump, 36): no one
                // can target them, so they are nobody's heal (user: Rosa looped
                // looking for a hidden Edward).
                if(cid is HiddenCondition or JumpCondition)away=true;
                if(cid is ToadCondition or ToadCondition2 && toad==null)toad=p;
                if(cid is MiniCondition or MiniCondition2 && mini==null)mini=p;
                int rankNow=Array.IndexOf(StatusOrder,cid);
                if(rankNow>=0 && (afflicted==null || rankNow<afflictedRank)) {afflicted=p;afflictedRank=rankNow;afflictedStatus=cid;}
            }
            if(who==PalomId)palomMp=stats.CurrentMP;
            if(who==PoromId)poromMp=stats.CurrentMP;
            if(who==EdwardId && !away)edwardCanItem=true;
            if(who is Rydia or Tellah or Rosa && stats.CurrentMP>=CureMp)casterCanCure=true;
            if(who==Rosa && stats.CurrentMP>=CureMp)rosaCanCure=true;
            if(who==Rydia && Knows(p!,CureAbility))rydiaMp=stats.CurrentMP;
            if(who==Rosa && ratio<HealBelow && !taken)rosaLow=p;
            if(rank<0 || taken || away)continue;
            // Rosa tops up Rosa/Cecil/Yang under 75%, Rydia/Edward under 65%;
            // lowest share first (HealKey: a small edge for the first three,
            // a member raised this fight and still low before all; she once topped up a 200 HP Yang
            // while Edward sat at 17).
            float heal=HealKey(rank,ratio,fellThisFight.Contains(member));
            if(rosaParty && ratio<(rank==0?RosaHealBelow:OthersHealBelow) && heal<rosaKey) {rosaTarget=p;rosaKey=heal;}
            if(ratio>=HealBelow)continue;
            float key=rosaParty?heal:ratio-(IsCecil(p!)?.05f:0);
            if(key<injuredRatio) {injured=p;injuredRatio=key;injuredRatioNow=ratio;}
        }
        // Scripted losses (Titan at Mist) wipe the party and the story goes on;
        // a real game over leaves the battle for GotoTitle, which no step allows.
        if(players.Length>0 && alive==0) {state.Detail="Party down; waiting for the game";return;}
        var enemyText=new System.Text.StringBuilder();bool mist=false,hasEnemy=false;scripted=false;fleeTarget=false;undead=null;weakest=null;toughestFlyer=null;weakestRank=int.MaxValue;undeadCount=livingEnemies=0;tough=boss=false;Array.Clear(elementCounts);livingIds.Clear();livingHp.Clear();livingMode.Clear();targetReflect=false;
        for(int i=0;i<enemies.Length;i++)
        {
            var e=enemies[i];var p=e?.BattleUnitDataInfo?.Parameter;
            if(e==null || p==null || p.CurrentHP<=0 || e.IsRemove)continue;
            hasEnemy=true;livingEnemies++;
            if(e.MonsterMaster!=null) {livingIds.Add(e.MonsterMaster.Id);livingHp[e.MonsterMaster.Id]=p.CurrentHP;livingMode[e.MonsterMaster.Id]=p.TryCast<EnemyCharacterParameter>()?.CurrentMode??-1;}
            var master=e.MonsterMaster;
            if(master!=null)
            {
                if(master.Defense>=ToughDefense)tough=true;
                if(master.Boss!=0)boss=bossSeen=true;
                if(Db.Monsters.TryGetValue(master.Id,out var known) && known.Flying &&
                   (toughestFlyer==null || p.CurrentHP>toughestFlyer.BattleUnitDataInfo.Parameter.CurrentHP))toughestFlyer=e;
                foreach(var el in WeakTo(master))if(el is >=1 and <=3)elementCounts[el]++;
                enemyText.Append(enemyText.Length>0?", ":"").Append(Db.MonsterName(master.Id)).Append(' ').Append(p.CurrentHP).Append("hp");
            }
            // Story fights you can't win or lose normally (the Girl at Mist has
            // 60000 HP): never spend items or MP on them.
            if((e.MonsterMaster?.Id==207 || p.ConfirmedMaxHp()>=30000) && Array.IndexOf(RealFights,e.MonsterMaster?.Id??0)<0)scripted=true;

            // Kill-first monsters (Route.KillFirst, in order) before anything
            // else, then the lowest HP (user: Gatlinger before the Domovoi
            // imps; the party was wiped while it picked off the imps).
            int rank=KillRank(e.MonsterMaster?.Id??0);
            // The kill order only moves forward: a monster whose turn has
            // passed (it died) isn't chased again when it comes back (live:
            // Baigan's Right Arm regrew after every kill and the attackers
            // never reached Baigan; user: "kill right arm, kill body, kill
            // left arm").
            if(rank<killStage)rank=int.MaxValue;
            if(weakest==null || rank<weakestRank || rank==weakestRank && p.CurrentHP<weakest.BattleUnitDataInfo.Parameter.CurrentHP) {
                weakest=e;weakestRank=rank;targetReflect=false;
                var conds=p.CurrentConditionList;
                for(int c=0;conds!=null && c<conds.Count;c++)if(conds[c]?.Id==ReflectCondition)targetReflect=true;
            }
            if(e.MonsterMaster?.Species==UndeadSpecies)
            {
                undeadCount++;
                if(undead==null || p.CurrentHP<undead.BattleUnitDataInfo.Parameter.CurrentHP)undead=e;
            }
            if(e.MonsterMaster?.Id==162)
            {
                var ep=p.TryCast<EnemyCharacterParameter>();
                if(ep==null) {state.Failure="Cannot read Mist Dragon's form";return;}
                // Installed sc_ai_162_MistDragon: modes 1/3 counter attacks in mist.
                mist=ep.CurrentMode is 1 or 3;
                state.Detail=$"Mist Dragon HP {p.CurrentHP}; mode {ep.CurrentMode}";
            }
        }
        // A boss fight stays one after the boss splits or leaves (Mom Bomb
        // explodes into plain Bombs): never flee it.
        if(bossSeen)boss=true;
        // Mom Bomb "gets big": its grow action is ChangeSprite (function 45),
        // which swaps the drawn sprite while MonsterAssetId stays the same (the
        // first try watched the asset id and never fired). Watch each enemy
        // view's drawn sprite and size, and as a backup the "I'M GONNA BLOW!"
        // line (E0020_01_180_b_01) in the battle message. Latched per fight.
        // Live change for WhileMonsterChanged (Cagnazzo's shell): two checks
        // in a row that disagree with the start flip it, so a single attack
        // animation frame doesn't.
        if(frame%12==0 && plan!=null && Array.Exists(plan,r=>r.WhileMonsterChanged!=null))
        {
            bool differs=false;
            foreach(var view in UnityEngine.Object.FindObjectsOfType<BattleEnemyEntity>())
            {
                if(view==null)continue;
                var sr=view.characterSR;
                string sig=$"{view.MonsterAssetId}/{(sr!=null?sr.Width:0)}x{(sr!=null?sr.Height:0)}/{(sr?.sprite!=null?sr.sprite.name:"")}";
                if(!startSprite.TryGetValue(view.Pointer,out var first))startSprite[view.Pointer]=sig;
                else if(first!=sig)differs=true;
            }
            if(differs!=changed) {if(++changeVotes>=2) {changed=differs;changeVotes=0;}}
            else changeVotes=0;
        }
        if(changed)enemyText.Append(" [changed]");
        if(targetReflect)enemyText.Append(" [reflect]");
        if(plan!=null && Array.Exists(plan,r=>r.WhileModeIn!=null || r.WhileModeNot!=null))foreach(var kv in livingMode)enemyText.Append($" [mode {kv.Key}:{kv.Value}]");
        if(frame%12==0 && !grown && plan!=null && Array.Exists(plan,r=>r.WhileMonsterGrown!=null))
        {
            foreach(var view in UnityEngine.Object.FindObjectsOfType<BattleEnemyEntity>())
            {
                if(view==null)continue;
                var sr=view.characterSR;
                string sig=$"{view.MonsterAssetId}/{(sr!=null?sr.Width:0)}x{(sr!=null?sr.Height:0)}/{(sr?.sprite!=null?sr.sprite.name:"")}";
                spriteLine=sig;
                if(!firstSprite.TryGetValue(view.Pointer,out var first))firstSprite[view.Pointer]=sig;
                else if(first!=sig)grown=true;
            }
            var warning=Last.Management.MessageManager.Instance?.GetMessage(GrowWarning,false);
            var shown=UnityEngine.Object.FindObjectOfType<Last.UI.KeyInput.BattleCommandMessageController>()?.view?.messageText?.nameText?.text;
            if(!string.IsNullOrEmpty(warning) && !string.IsNullOrEmpty(shown) && shown.Contains(warning.Trim()))grown=true;
        }
        if(spriteLine.Length>0 && livingIds.Contains(MomBomb))enemyText.Append(" {").Append(spriteLine).Append('}');
        if(grown)enemyText.Append(" [grown]");
        enemyLine=enemyText.ToString();
        state.Battle=decision.Length>0?$"{enemyLine} | {decision}":enemyLine;
        bestElement=bestElementCount=0;
        for(int el=1;el<=3;el++)if(elementCounts[el]>bestElementCount) {bestElement=el;bestElementCount=elementCounts[el];}
        // Community strat: after the Mist Dragon's mist phase, Darkness again.
        if(mist && !wasMist)usedOnce.Clear();
        wasMist=mist;
        if(!hasEnemy) {Pulse(state,frame,"Confirm","Waiting for battle result");return;}
        // The whole group (alive or not) decides whether this is a listed fight.
        var group=new HashSet<int>();
        for(int i=0;i<enemies.Length;i++)if(enemies[i]?.MonsterMaster!=null)group.Add(enemies[i].MonsterMaster.Id);
        foreach(var flee in FleeParties)if(flee.SetEquals(group))fleeTarget=true;
        if(fleeMonsters!=null)foreach(var id in fleeMonsters)if(group.Contains(id))fleeTarget=true;
        // Route.Rng.AvoidMonsters: a dangerous group the steering couldn't dodge.
        foreach(var id in EncounterWatch.Avoid)if(group.Contains(id))fleeTarget=true;
        while(livingEnemies>0 && killStage<KillFirst.Length && !livingIds.Contains(KillFirst[killStage]) && killSeen.Contains(KillFirst[killStage]))killStage++;
        foreach(int id in livingIds)killSeen.Add(id);
        foreach(var rule in FleeCounts)
        {
            int count=0;
            for(int i=0;i<enemies.Length;i++)if(rule.Length==2 && enemies[i]?.MonsterMaster?.Id==rule[0])count++;
            if(rule.Length==2 && count>=rule[1])fleeTarget=true;
        }
        // Run from listed encounter groups (never bosses or story fights): hold both
        // shoulder keys, which BattleMenuController.IsEscapeInput polls. If the
        // party hasn't escaped after ~10 s (fight can't be fled), fight on.
        // ...and run from any ordinary fight that is taking too long.
        // A toad/mini in an ordinary fight: run first, cure with the item in the
        // next fight that is actually fought (user's call).
        // Grinds (looping navigate steps) fight to the end: the exp is the point,
        // and slow fights were fled when nearly won (user).
        bool slow=!grinding && fightSeconds>SlowFightSeconds,toughFight=FleeTough && tough,status=toad!=null || mini!=null;
        if((fleeTarget || slow || toughFight || status) && !boss && !scripted && !story && fleeFrames<(fleeTarget?FleeGiveUpListed:FleeGiveUp))
        {
            // L+R escape works only from the command list: back out of any
            // open item, spell or target list first.
            if(menu==null || !menu)menu=UnityEngine.Object.FindObjectOfType<BattleMenuController>();
            var open=menu!=null && menu.IsShow?menu.infomationController?.stateMachine:null;
            if(open!=null && (int)open.Current>1)
            {Pulse(state,frame,"Back","Close menu to run");state.Battle=$"{enemyLine} | Party: run away (closing menu)";return;}
            fleeFrames++;state.BattleInput="SwitchLeft+SwitchRight";
            state.Detail="Running away";
            decision=fleeTarget?"Party: run away (listed encounter)":status?"Party: run away (toad/mini; cure after)":toughFight?"Party: run away (tough enemy; save MP for the boss)":$"Party: run away (fight past {SlowFightSeconds:0} s)";
            state.Battle=$"{enemyLine} | {decision}";return;
        }
        if(menu==null || !menu)menu=UnityEngine.Object.FindObjectOfType<BattleMenuController>();
        if(menu==null || !menu.IsShow)return;
        if(menu.IsSpecialHelpPause) {Pulse(state,frame,"Confirm","Advance battle tutorial");return;}
        if(menu.IsAuto) {state.Failure="Auto-battle is enabled; disable it before restarting the route";return;}
        if(menu.IsPause) {state.Failure="Battle paused; run stopped";return;}
        var info=menu.infomationController;
        if(info?.stateMachine==null || menu.stateMachine==null || (int)menu.stateMachine.Current!=2) {turnActor=IntPtr.Zero;return;}
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
                Decide(info,actor,dead,injured,mist,plan);
                // A wanted command that is on the list but greyed out (Items
                // while hidden): Attack instead of cycling around it forever.
                if(pendingCommand!=Row && !mist && (pendingItem!=0?!CommandEnabled(info,ItemsCommand):pendingCommand!=Defend && pendingCommand!=Row && !CommandEnabled(info,pendingCommand)) && CommandEnabled(info,1))
                {if(pendingTarget!=0 && queuedHeals.TryGetValue(pendingTarget,out var q) && q.Healer==actor.Pointer)queuedHeals.Remove(pendingTarget);
                 pendingItem=0;pendingAbility=0;pendingTarget=0;pendingRule=-1;targetAll=false;aimFlyer=false;pendingCommand=1;decision+=" -> greyed out, Attack";}
                SelectCommand(info,pendingItem!=0?3:mist && pendingCommand!=Row?2:pendingCommand,state,frame);
                break;
            // Row asks for a target too (the party side, every member): the
            // enemy-side cursor logic pushed Left at it forever (segment 6,
            // Scarmiglione's back attack). Confirm whatever it offers.
            case 3 or 5 or 6 when pendingCommand==Row:
                Pulse(state,frame,"Confirm","Confirm Row");break;
            case 2 or 4 when pendingTarget!=0 && unreachable.Contains(pendingTarget):
                // Backing out of a heal on someone the cursor can't reach: up
                // to the command list, where Decide picks someone else.
                Pulse(state,frame,"Back","Target unreachable; choose again");break;
            case 4:
                if(pendingAbility==0) {Block();Pulse(state,frame,"Back","Cancel unexpected ability list");break;}
                SelectAbility(info,state,frame);break;
            case 5:
                if(pendingTarget!=0)SelectHealingTarget(info,state,frame);
                else SelectEnemy(info,state,frame,"Cast on selected enemy",castOnUndead?undead:aimFlyer?toughestFlyer:weakest,targetAll || (castOnUndead && undeadCount>1));
                break;
            case 2:
                if(pendingItem==0) {Pulse(state,frame,"Back","Cancel unexpected item list");break;}
                SelectItem(info,pendingItem,state,frame);break;
            case 3:
                if(itemOnEnemies)SelectEnemy(info,state,frame,heldUse?"Held item at the target":"Throw item at the enemies",weakest,!heldUse);
                else SelectHealingTarget(info,state,frame);
                break;
            case 6:
                if(mist) {Pulse(state,frame,"Back","Cancel attack: dragon became mist");break;}
                SelectEnemy(info,state,frame,aimFlyer?"Aim at the flyer":"Attack weakest enemy",aimFlyer?toughestFlyer:weakest);break;
            case 0:break;
            default:Pulse(state,frame,"Back","Return to supported battle commands");break;
        }
    }
    private int targetPresses,sidePresses;
    private void SelectEnemy(BattleInfomationController info,Snapshot state,int frame,string action,BattleEnemyData? wanted,bool all=false)
    {
        var t=info.targetSelectController;
        if(t?.stateMachine==null)return;
        int side=(int)t.stateMachine.Current;
        // EnemyAll (4): PageDown/PageUp toggles it from single-enemy targeting
        // (Last.UI.KeyInput.BattleTargetSelectController.EnemysUpdate).
        if(all && side==EnemyAllState) {if(frame%12==0)targetPresses=0;Pulse(state,frame,"Confirm","Cast on all enemies");return;}
        if(all && side==EnemySingleState && targetPresses<6)
        {
            if(frame%12!=0)return;
            targetPresses++;
            Pulse(state,frame,targetPresses<=3?"PageDown":"PageUp","Target all enemies");return;
        }
        // Commands that always hit every enemy (Sing, Kick) open on EnemyAll and
        // can't be narrowed: confirm. Otherwise Left a few times, then confirm.
        if(side==EnemyAllState && (pendingCommand!=1 || sidePresses>=3))
        {if(frame%12==0)sidePresses=0;Pulse(state,frame,"Confirm",action+" (all enemies)");return;}
        if(side!=EnemySingleState) {if(frame%12==0)sidePresses++;Pulse(state,frame,SideKey(info,true,sidePresses),"Select enemy side");return;}
        sidePresses=0;
        var selected=t.GetEnemyTarget();
        // Whole-side targeting (e.g. Darkness) selects every living enemy.
        bool living=selected!=null && selected.Count>0;
        for(int i=0;living && i<selected!.Count;i++)living=selected[i].BattleUnitDataInfo.Parameter.CurrentHP>0;
        bool onWanted=wanted==null || (selected!=null && selected.Count>1);
        for(int i=0;!onWanted && selected!=null && i<selected.Count;i++)onWanted=SameEnemy(selected[i],wanted!);
        // Cycle toward the wanted (weakest/undead) enemy; after one lap take
        // whatever living enemy is selected. Counters change only on frames
        // where a key is actually sent (every 12th), never before the press.
        if(frame%12!=0) {state.Detail=living && onWanted?action:"Find weakest enemy";return;}
        if(living && (onWanted || targetPresses>=livingEnemies+1)) {targetPresses=0;Pulse(state,frame,"Confirm",action);return;}
        targetPresses++;
        Pulse(state,frame,"Down",living?"Find weakest enemy":"Select living enemy");
    }
    // Target lists hand back different wrapper objects for the same enemy, so
    // match the native parameter object, else monster id plus current HP.
    private static bool SameEnemy(BattleEnemyData a,BattleEnemyData b)
    {
        var pa=a.BattleUnitDataInfo?.Parameter;var pb=b.BattleUnitDataInfo?.Parameter;
        if(pa==null || pb==null)return false;
        if(pa.Pointer==pb.Pointer)return true;
        return a.MonsterMaster?.Id==b.MonsterMaster?.Id && pa.CurrentHP==pb.CurrentHP;
    }
    private const float HealBelow=.5f;
    private const int DarknessCommand=10,Aim=13,ItemsCommand=3,ReturnCommand=58;
    private const int EnemySingleState=3,EnemyAllState=4;
    private int undeadCount,livingEnemies;
    private bool targetAll,tough,boss,fleeTarget;
    internal List<HashSet<int>> FleeParties=new();
    internal int[][] FleeCounts=Array.Empty<int[]>();
    internal bool FleeTough;
    internal int[] KillFirst=Array.Empty<int>();
    // Position in KillFirst this fight, and the kill-order monsters seen
    // alive in it (a stage passes only once its monster was seen and died).
    private int killStage;private readonly HashSet<int> killSeen=new();
    internal int[] RealFights=Array.Empty<int>();
    private int weakestRank;
    // The current target (kill order, else lowest HP) has Reflect (Baigan's Wall).
    private bool targetReflect;
    private const int ReflectCondition=28;
    private int KillRank(int id) {int i=Array.IndexOf(KillFirst,id);return i<0?int.MaxValue:i;}
    private int fleeFrames;
    private const int FleeGiveUp=600;
    // Listed groups (Skeletons on Mount Hobs) keep trying longer: 600 frames
    // ran out once and the party fought four Skeletons.
    private const int FleeGiveUpListed=1800;
    private const float SlowFightSeconds=60;
    private readonly HashSet<int> unreachable=new();
    private const int TargetGiveUp=10;
    private float fightSeconds;private int fightFrame=-1;
    // Back attack flips the rows: the first member with a turn uses Row once.
    private bool rowDone;
    private const int Row=5,Rydia=4,Rosa=3,EdwardId=7;
    // Edward alive and not hidden: with no Cure caster up (Cecil, Yang and
    // Edward at the Fabul defense) he is the item healer and the fighters
    // keep attacking (user: "Ed should always be the preferred item healer").
    private bool edwardCanItem;
    // Master condition ids (both entries named Toad/Mini) and their cure items.
    private const int HiddenCondition=32,JumpCondition=36,ToadCondition=14,ToadCondition2=58,MiniCondition=13,MiniCondition2=57,MaidensKiss=11,Mallet=12;
    private BattlePlayerData? toad,mini,afflicted;private int afflictedRank,afflictedStatus;
    // Every status worth a turn, worst first (user: "stone or confuse
    // anything... all status effects"): Petrify 11, gradual Petrify 17,
    // Confuse 12, Sleep 7, Paralyze 8, Toad 14/58, Pig 15/59, Silence 6,
    // Mini 13/57 - these come before healing - then Darkness 9, Poison 10,
    // Curse 18, after healing.
    private static readonly int[] StatusOrder={11,17,12,7,8,14,58,15,59,6,13,57,9,10,18};
    private const int MinorStatusRank=12,RemedyItem=18;
    private const int PalomId=9,PoromId=10,TwincastCommand=18,TwincastMp=20;private int palomMp,poromMp;
    // The item that clears each status in battle (master function groups);
    // Sleep, Paralyze and Confuse have none (Esuna or a Remedy).
    private static int StatusItem(int status)=>status switch
    {
        11 or 17=>10,   // Gold Needle
        14 or 58=>11,   // Maiden's Kiss
        13 or 57=>12,   // Mallet
        15 or 59=>13,   // Diet Food
        6=>14,          // Echo Herbs
        9=>15,          // Eye Drops
        10=>16,         // Antidote
        18=>17,         // Cross
        _=>0,
    };
    // The cure for the worst status on someone else: its item, else Esuna
    // (clears them all) from someone who knows it, isn't Silenced and can
    // afford it, else a Remedy if one is owned (never bought: "spendy").
    private bool CureStatus(BattleInfomationController info,BattlePlayerData actor,bool minor)
    {
        if(afflicted==null || afflicted.Pointer==actor.Pointer || (afflictedRank>=MinorStatusRank)!=minor)return false;
        int target=afflicted.GetOwnedCharacterData()?.characterStatusId??0;if(target==0 || unreachable.Contains(target))return false;
        if(queuedHeals.TryGetValue(target,out var q) && q.Healer!=actor.Pointer)return false;
        var stats=actor.BattleUnitDataInfo?.Parameter;
        int item=StatusItem(afflictedStatus);
        if(item!=0 && HasItem(item)) {pendingItem=item;pendingTarget=target;Queue(actor,target);Log(actor,$"Item {item} on #{target} (status {afflictedStatus})");return true;}
        if(stats!=null && stats.CurrentMP>=EsunaMp && Knows(actor,EsunaAbility) && CommandEnabled(info,WhiteMagic) && !blockedStatusSpell.Contains(actor.Pointer))
        {pendingCommand=WhiteMagic;pendingAbility=EsunaAbility;pendingTarget=target;Queue(actor,target);Log(actor,$"Esuna on #{target} (status {afflictedStatus})");return true;}
        if(HasItem(RemedyItem)) {pendingItem=RemedyItem;pendingTarget=target;Queue(actor,target);Log(actor,$"Remedy on #{target} (status {afflictedStatus}, no cheaper cure)");return true;}
        return false;
    }
    private const int BlackMagic=8,ToadSpell=45,MiniSpell=26;
    private readonly HashSet<IntPtr> blockedStatusSpell=new();
    internal Dictionary<int,int[]> Weaknesses=new();
    internal BattleDb Db=new();
    private string decision="",enemyLine="";
    private int[] WeakTo(Last.Data.Master.Monster master)=>Db.Monsters.TryGetValue(master.Id,out var m)?m.WeakIds:Weaknesses.TryGetValue(master.Id,out var w)?w:Array.Empty<int>();
    private readonly int[] elementCounts=new int[4];
    private int bestElement,bestElementCount;
    private const int ToughDefense=200;
    // Element -> native black magic ability: Fire 33, Blizzard 36, Thunder 39.
    private static readonly int[] ElementSpell={0,33,36,39};
    private const int EsunaAbility=10,EsunaMp=20;
    private const int Defend=2,UndeadSpecies=4,WhiteMagic=7,CureAbility=6,CureMp=3,Tellah=6,LifeAbility=11,LifeMp=8;
    private bool scripted,castOnUndead,casterCanCure;
    private float injuredRatioNow=1;
    private BattleEnemyData? undead,weakest;
    private void Log(BattlePlayerData actor,string what)
    {
        string who=Names.TryGetValue(actor.GetOwnedCharacterData()?.characterStatusId??0,out var n)?n:"?";
        decision=who+": "+what;
    }
    private static readonly Dictionary<int,string> Names=new(){[1]="Cecil",[13]="Cecil",[2]="Kain",[4]="Rydia",[6]="Tellah",[3]="Rosa",[7]="Edward",[8]="Yang"};
    private static bool IsCecil(BattlePlayerData p)=>p.GetOwnedCharacterData()?.characterStatusId is 1 or 13;
    private bool rosaParty,rosaCanCure,bossSeen,aimFlyer;
    private int rydiaMp;
    // Rydia covers Cures while Rosa shoots, but keeps her summon MP for the boss.
    private bool RydiaCovers=>rydiaMp>=CureMp+(boss?0:RydiaBossReserve);
    private BattleEnemyData? toughestFlyer;
    private int flyerAims;
    private IntPtr decidingActor;
    private IntPtr turnActor;private int turnRule=-1;private bool itemOnEnemies;
    private readonly Dictionary<int,int> livingMode=new();private readonly HashSet<int> livingIds=new();private readonly Dictionary<int,int> livingHp=new();private readonly Dictionary<IntPtr,string> firstSprite=new();private bool grown,changed;private int changeVotes;private readonly Dictionary<IntPtr,string> startSprite=new();private string spriteLine="";private const string GrowWarning="E0020_01_180_b_01";private const int MomBomb=165;private BattlePlayerData? rosaTarget,rosaLow;
    private const float RosaHealBelow=.75f,CriticalHp=.35f;
    // Lowest HP share first; Rosa, Cecil and Yang only get a small edge
    // (user: "loosen that up", "I would have healed Ed there"), and anyone
    // under CriticalHp comes before everyone else.
    private const float PriorityEdge=.1f,OthersHealBelow=.65f;
    // Raised this fight and still under CriticalHp: before everyone (user:
    // "only if raised"; a Phoenix Down is wasted if they are left to die).
    private static float HealKey(int rank,float ratio,bool raised)=>raised && ratio<CriticalHp?ratio-1:ratio-(rank==0?PriorityEdge:0);
    private readonly HashSet<int> fellThisFight=new();
    private const int Summon=9,ChocoboSummon=65,ChocoboMp=7,RydiaBossReserve=14,Edward=7;
    // Heal order with Rosa in the party: Rosa, Cecil and Yang (rank 0, the
    // lowest HP% of them first), then everyone else (rank 1). Without Rosa
    // everyone is rank 0.
    private int HealRank(BattlePlayerData p)
    {
        if(!rosaParty)return 0;
        int who=p.GetOwnedCharacterData()?.characterStatusId??0;
        return who is Rosa or 1 or 13 or 8?0:1;
    }
    // One decision per turn. Order: scripted fights just attack; revive;
    // heal (Cure first for everyone, Potion only if nobody can cast); Cure on
    // undead; route plan; Attack the weakest enemy. Tellah keeps the MP
    // reserve his route plan sets.
    // The member most in need of healing this frame, for TargetInjured rules.
    private BattlePlayerData? injuredNow;
    private void Decide(BattleInfomationController info,BattlePlayerData actor,BattlePlayerData? dead,BattlePlayerData? injured,bool mist,BattleRule[]? plan)
    {
        injuredNow=injured;
        pendingItem=0;pendingTarget=0;pendingCommand=1;pendingAbility=0;pendingRule=-1;castOnUndead=false;targetAll=false;itemOnEnemies=false;aimFlyer=false;heldUse=false;
        // Decide re-runs every frame of a turn; count a turn once.
        bool newTurn=actor.Pointer!=decidingActor;decidingActor=actor.Pointer;
        abilityConfirms=abilityStuck=abilityStride=0;abilityLastIndex=-1;abilityLastKey=null;
        // Back attack: the first member who gets a turn (anyone, Cecil too;
        // user: "really important, for the whole game") swaps the rows back
        // with Row, in every fight (user: "it should always be on"), before any
        // other rule. Done only once Row is confirmed: marking it on the first
        // frame let Decide's next frame pick Attack, so Row was never used.
        if(info.isBackAttack && !rowDone && livingEnemies>0)
        {pendingCommand=Row;Log(actor,"Row (back attack)");return;}
        if(mist) {Log(actor,"Defend: Mist Dragon in mist");return;}
        // Scripted fights (Mist Dragon story fight, the Dark Knight trial at
        // 60000 HP) attack unless the route's plan names a rule for them: the
        // trial is survived by Defending (guide: "defend for about three
        // rounds"), and hitting the Dark Knight only draws counters.
        if(scripted)
        {
            // Only rules tied to a monster (whileMonster), so older routes'
            // general rules don't change their scripted fights.
            ChoosePlan(info,actor,plan==null?null:Array.FindAll(plan,r=>r.WhileMonster!=null));
            if(pendingRule>=0)return;
            pendingCommand=1;pendingAbility=0;
            Log(actor,"Attack: scripted fight, no items or MP");return;
        }
        // Edward hides by himself at low HP (Hide auto-executes); hidden, his
        // list offers Return and greys out Items, and the run looped on the
        // greyed Items. Come back out; the next turn acts normally.
        if(CommandEnabled(info,ReturnCommand)) {pendingCommand=ReturnCommand;Log(actor,"Return (came out of hiding)");return;}
        var stats=actor.BattleUnitDataInfo?.Parameter;
        bool isTellah=actor.GetOwnedCharacterData()?.characterStatusId==Tellah;
        bool canCure=stats!=null && stats.CurrentMP>=CureMp && CommandEnabled(info,WhiteMagic) && !blockedCure.Contains(actor.Pointer);
        int me=actor.GetOwnedCharacterData()?.characterStatusId??0;
        // Rules marked First beat every heal and cure (Cecil at Baigan).
        if(plan!=null && Array.Exists(plan,r=>r.First)) {ChoosePlan(info,actor,plan,true);if(pendingRule>=0)return;}
        // Status cures before healing: the worst status on anyone (Petrify,
        // Confuse, Sleep, Paralyze, Toad, Pig, Silence, Mini) - its item, else
        // Esuna, else a Remedy; Toad/Mini then fall back to the toggle spells.
        if(CureStatus(info,actor,false))return;
        // No item or Esuna: the same spell toggles Toad/Mini back.
        if(stats!=null && !blockedStatusSpell.Contains(actor.Pointer) && actor.Pointer!=(toad?.Pointer??IntPtr.Zero) && actor.Pointer!=(mini?.Pointer??IntPtr.Zero))
        {
            if(toad!=null && stats.CurrentMP>=Db.SpellMp(ToadSpell,7) && CommandEnabled(info,BlackMagic))
            {pendingCommand=BlackMagic;pendingAbility=ToadSpell;pendingTarget=toad.GetOwnedCharacterData().characterStatusId;Log(actor,"Toad on #"+pendingTarget+" (undo toad)");return;}
            if(mini!=null && stats.CurrentMP>=Db.SpellMp(MiniSpell,6) && CommandEnabled(info,WhiteMagic))
            {pendingCommand=WhiteMagic;pendingAbility=MiniSpell;pendingTarget=mini.GetOwnedCharacterData().characterStatusId;Log(actor,"Mini on #"+pendingTarget+" (undo mini)");return;}
        }
        // A flyer on the field: Rosa's arrows hit its weakness (user: "if you
        // see a weakness you should try to exploit it. period."). Rosa Aims at
        // the flyer with the most HP (she one-shots most; user); top-ups
        // wait, and anyone under half HP is Rydia's
        // to Cure. Rosa still heals that when Rydia can't, and revives first.
        bool urgent=injured!=null || rosaLow!=null;
        // Someone raised this fight and still low: Rosa heals now instead of
        // leaving it to Rydia.
        bool critical=injured!=null && injuredRatioNow<CriticalHp && fellThisFight.Contains(injured.GetOwnedCharacterData()?.characterStatusId??0);
        if(me==Rosa && toughestFlyer!=null && CommandEnabled(info,Aim) && (dead==null || !Knows(actor,LifeAbility)) && (!urgent || RydiaCovers && !critical))
        {pendingCommand=Aim;aimFlyer=true;if(newTurn)flyerAims++;Log(actor,$"Aim at {Db.MonsterName(toughestFlyer.MonsterMaster.Id)} (weak to arrows){(urgent?"; Rydia heals":"")}");return;}
        // Rosa always heals: Rosa/Cecil/Yang under 75% (lowest first), then
        // Rydia/Edward under 50%; else she Defends.
        if(me==Rosa && rosaTarget!=null && canCure)
        {pendingCommand=WhiteMagic;pendingAbility=CureAbility;pendingTarget=rosaTarget.GetOwnedCharacterData().characterStatusId;Queue(actor,pendingTarget);Log(actor,"Cure #"+pendingTarget+" (Rosa heals)");return;}
        // Revive: Life (white magic) from anyone who knows it and has the MP,
        // else a Phoenix Down.
        if(dead!=null && stats!=null && stats.CurrentMP>=LifeMp && CommandEnabled(info,WhiteMagic) && !blockedLife.Contains(actor.Pointer) && Knows(actor,LifeAbility))
        {pendingCommand=WhiteMagic;pendingAbility=LifeAbility;pendingTarget=dead.GetOwnedCharacterData().characterStatusId;Queue(actor,pendingTarget);Log(actor,"Life on #"+pendingTarget);return;}
        // Cecil and Yang fight: they only raise Rosa, Cecil or Yang. Rydia and
        // Edward are Rosa's (Life/Phoenix Down), Edward's, or the field heal's
        // after the fight (user: both spent the Mom Bomb split on Phoenix
        // Downs for Rydia and Edward).
        bool fighter=me is 1 or 13 or 8;
        bool edwardHeals=fighter && edwardCanItem && !casterCanCure;
        if(dead!=null && HasItem(9) && !edwardHeals && (!fighter || HealRank(dead)==0)) {pendingItem=9;pendingTarget=dead.GetOwnedCharacterData().characterStatusId;Queue(actor,pendingTarget);Log(actor,"Phoenix Down on #"+pendingTarget);return;}
        // With Rosa alive and able to Cure, nobody else heals: Rydia Cured and
        // Yang/Cecil used Potions through the Mom Bomb split instead of
        // fighting, and the party lost.
        // ...except Rosa herself: when she is under half HP anyone heals her
        // (user), with Cure or a Potion.
        if(rosaParty && me!=Rosa && rosaLow!=null)injured=rosaLow;
        bool leaveToRosa=rosaParty && me!=Rosa && rosaCanCure && injured!=rosaLow && !(me==Rydia && toughestFlyer!=null && RydiaCovers);
        if(injured!=null && !leaveToRosa)
        {
            int target=injured.GetOwnedCharacterData().characterStatusId;
            // White Magic first for everyone; Tellah only above his plan's MP
            // reserve. Potions are the fallback when nobody can cast.
            bool tellahCanSpare=!isTellah || stats!.CurrentMP>=CureMp+Reserve(plan,Tellah);
            if(canCure && tellahCanSpare) {pendingCommand=WhiteMagic;pendingAbility=CureAbility;pendingTarget=target;Queue(actor,target);Log(actor,"Cure ally #"+target+" (under half HP)");return;}
            // Non-casters (Cecil) leave healing to the casters: a Potion only
            // when no caster has the MP to Cure (user: Cecil kept healing with
            // Rydia right behind him in the ATB).
            if(HasItem(2) && !edwardHeals && (!casterCanCure || (injured==rosaLow && me!=Rosa)))
            {pendingItem=2;pendingTarget=target;Queue(actor,target);Log(actor,"Potion on ally #"+target);return;}
        }
        // Darkness, Poison, Curse: once nobody needs healing.
        if(CureStatus(info,actor,true))return;
        // Porom's default with nobody to heal: Twincast with Palom, in every
        // fight (user: "they aren't twincasting anymore... just on
        // scarmiglione"). It can't be reflected and beats her Cure on undead.
        // Both need the MP (Comet costs 20 each); greyed out (Palom down,
        // silenced) she does what she did before.
        // Palom too: live, only the first Twincast landed while Porom alone
        // chose it and Palom took his own turns.
        // A boss rule naming this twin while its monster lives (Baigan: Twincast
        // reflects off his Wall, user) beats the default.
        bool bossRule=targetReflect || plan!=null && Array.Exists(plan,r=>r.Character==me && r.WhileMonster is int w && livingIds.Contains(w));
        if(me is PoromId or PalomId && !bossRule && stats!=null && stats.CurrentMP>=TwincastMp && (me==PoromId?palomMp:poromMp)>=TwincastMp && CommandEnabled(info,TwincastCommand))
        {pendingCommand=TwincastCommand;Log(actor,"Twincast (default)");return;}
        // A boss rule naming this member (whileMonster) beats the automatic
        // Cure on undead: Porom Twincasts on Milon Z when nobody needs healing
        // (user: "the cure seems underwhelming"). Twincast greyed out (Palom
        // down) falls back to the Cure.
        if(undead!=null && plan!=null && Array.Exists(plan,r=>r.Character==me && r.WhileMonster!=null))
        {
            ChoosePlan(info,actor,plan);
            if(pendingRule>=0 && plan[pendingRule].WhileMonster!=null && (pendingItem!=0 || CommandEnabled(info,pendingCommand)))return;
            pendingItem=0;pendingTarget=0;pendingCommand=1;pendingAbility=0;pendingRule=-1;castOnUndead=false;targetAll=false;itemOnEnemies=false;aimFlyer=false;heldUse=false;
        }
        // Cure hurts undead: cast it (on all of them when there are several).
        // Tellah joins in while he stays above the MP his route plan reserves.
        if(undead!=null && canCure && (!isTellah || stats!.CurrentMP>=CureMp+Reserve(plan,Tellah)))
        {pendingCommand=WhiteMagic;pendingAbility=CureAbility;castOnUndead=true;Log(actor,$"Cure{(undeadCount>1?" on all":"")} vs undead");return;}
        // Nobody to heal: Rosa Aims with her bow (guide), unless a plan rule
        // names her (all Defend while the Mom Bomb is big); Defend without a bow.
        if(me==Rosa && rosaParty)
        {
            if(plan!=null && Array.Exists(plan,r=>r.Character==Rosa)) {ChoosePlan(info,actor,plan);if(pendingRule>=0)return;}
            if(CommandEnabled(info,Aim)) {pendingCommand=Aim;Log(actor,"Aim (nobody to heal)");return;}
            pendingCommand=Defend;Log(actor,"Defend (nobody to heal, no bow)");return;
        }
        // Edward just attacks (user: "edward can just attack and die"),
        // unless a plan rule names him (all Defend while the Mom Bomb is big).
        if(me==Edward && (plan==null || !Array.Exists(plan,r=>r.Character==Edward))) {Log(actor,"Attack weakest enemy");return;}
        ChoosePlan(info,actor,plan);
        if(pendingItem!=0)return;
        // Rydia with no plan rule: Chocobo summon. Ordinary fights keep 14 MP
        // for the boss.
        if(pendingCommand==1 && me==Rydia && stats!=null && CommandEnabled(info,Summon) && stats.CurrentMP>=ChocoboMp+(boss?0:RydiaBossReserve))
        {pendingCommand=Summon;pendingAbility=ChocoboSummon;Log(actor,"Summon Chocobo");return;}
        // Rydia's attacks do ~1 damage: she Defends instead of wasting the turn.
        if(pendingCommand==1 && me==Rydia) {pendingCommand=Defend;Log(actor,"Defend (her Attack does nothing)");return;}
        if(pendingCommand==1)Log(actor,"Attack weakest enemy");
    }
    private readonly HashSet<IntPtr> blockedCure=new(),blockedLife=new();
    private static bool Knows(BattlePlayerData actor,int ability)
    {
        var list=actor.GetOwnedCharacterData()?.OwnedAbilityList;
        for(int i=0;list!=null && i<list.Count;i++)if(list[i]?.Ability?.Id==ability)return true;
        return false;
    }
    // Member (character_status id) -> heal queued on them: their HP then, the
    // fight time and the healer. Cleared once their HP rises or after a while.
    private readonly Dictionary<int,(int Hp,float At,IntPtr Healer)> queuedHeals=new();
    private readonly Dictionary<int,int> hpByStatus=new();
    private const float HealLandSeconds=10;
    private void Queue(BattlePlayerData healer,int target)=>queuedHeals[target]=(hpByStatus.TryGetValue(target,out int hp)?hp:0,fightSeconds,healer.Pointer);
    private readonly HashSet<(int,IntPtr)> usedOnce=new();
    private bool wasMist;
    // MP a character's plan keeps back for the boss (BattleRule.ReserveMp).
    private static int Reserve(BattleRule[]? plan,int character)
    {
        int reserve=0;
        if(plan!=null)foreach(var r in plan)if(r.Character==character)reserve=Math.Max(reserve,r.ReserveMp);
        return reserve;
    }
    // First plan rule for this actor whose command is visible and enabled and
    // whose HP condition holds. Ability availability is checked in the list.
    private void ChoosePlan(BattleInfomationController info,BattlePlayerData actor,BattleRule[]? plan,bool firstOnly=false)
    {
        if(plan==null)return;
        var stats=actor.BattleUnitDataInfo?.Parameter;
        int id=actor.GetOwnedCharacterData()?.characterStatusId??0;
        for(int r=0;r<plan.Length;r++)
        {
            var rule=plan[r];
            if(blockedRules.Contains(r))continue;
            if(firstOnly && !rule.First)continue;
            // Decide re-runs every frame of a turn: the rule this turn already
            // took stays allowed, so a once-per-battle rule isn't skipped
            // before it is ever input (Yang skipped Attack x2 + Focus).
            if(rule.OncePerBattle && usedOnce.Contains((r,actor.Pointer)) && !(actor.Pointer==turnActor && r==turnRule))continue;
            if(rule.BossOnly && !boss)continue;
            if(rule.Character!=null && rule.Character!=id)continue;
            // Black magic and Twincast at a target behind Reflect bounce back
            // onto the party (user, Baigan's Wall: "focus damage so he doesnt
            // reflect"): wait for it to drop.
            if(targetReflect && !rule.IgnoreReflect && rule.Command is BlackMagic or TwincastCommand)continue;
            if(stats!=null && rule.MinHpPercent>0 && stats.CurrentHP*100<rule.MinHpPercent*Math.Max(1,stats.ConfirmedMaxHp()))continue;
            if(stats!=null && rule.MinMp>0 && stats.CurrentMP<rule.MinMp)continue;
            if(rule.MinEnemies>0 && livingEnemies<rule.MinEnemies && !(rule.OrTough && tough))continue;
            if(rule.WhileMonster is int whileId && !livingIds.Contains(whileId))continue;
            if(rule.WhileMonster is int hpId && livingHp.TryGetValue(hpId,out int monsterHp) &&
               (monsterHp>rule.WhileMonsterHpAtMost || monsterHp<=rule.WhileMonsterHpAbove))continue;
            if(rule.WhileMonsterGrown is bool wantGrown && wantGrown!=grown)continue;
            if(rule.WhileMonsterChanged is bool wantChanged && wantChanged!=changed)continue;
            if(rule.OnTarget is int onTarget && weakest?.MonsterMaster?.Id!=onTarget)continue;
            if((rule.WhileModeIn!=null || rule.WhileModeNot!=null) && rule.WhileMonster is int modeId)
            {
                int mode=livingMode.TryGetValue(modeId,out int m)?m:-1;
                if(rule.WhileModeIn!=null && Array.IndexOf(rule.WhileModeIn,mode)<0)continue;
                if(rule.WhileModeNot!=null && Array.IndexOf(rule.WhileModeNot,mode)>=0)continue;
            }
            // Darkness is an HP-costing wave that can kill Cecil (user rule).
            // Only on large groups (4+), and only while Cecil is above a fifth
            // of his HP (it costs ~8-9% a use; the explosion leaves him low and
            // the user wants it on the split bombs).
            if(rule.Command==DarknessCommand && (livingEnemies<4 || (stats!=null && stats.CurrentHP*5<Math.Max(1,stats.ConfirmedMaxHp()))))continue;
            if(rule.HeldItem is int held)
            {
                if(!Holds(actor,held) || blockedItems.Contains(held))continue;
                pendingItem=held;pendingTarget=0;pendingRule=r;itemOnEnemies=true;heldUse=true;
                Log(actor,$"held item {held} (plan rule {r+1})");
                return;
            }
            if(rule.Item is int item)
            {
                if(!HasItem(item))continue;
                if(rule.TargetInjured && injuredNow==null)continue;
                pendingItem=item;pendingTarget=rule.TargetInjured?injuredNow!.GetOwnedCharacterData().characterStatusId:rule.TargetEnemies?0:rule.TargetCharacter??id;pendingRule=r;itemOnEnemies=rule.TargetEnemies;
                if(rule.TargetInjured)Queue(actor,pendingTarget);
                if(rule.OncePerBattle) {usedOnce.Add((r,actor.Pointer));turnActor=actor.Pointer;turnRule=r;}
                Log(actor,$"item {item} on #{pendingTarget} (plan rule {r+1})");
                return;
            }
            int ability=rule.Ability??0;bool all=rule.TargetAll;
            if(rule.ByWeakness)
            {
                if(bestElement==0)continue;
                // Fire 33/34/35, Blizzard 36/37/38, Thunder 39/40/41.
                ability=ElementSpell[bestElement]+Math.Clamp(rule.WeaknessTier,0,2);
                // One spell on all when several enemies share the weakness.
                all=bestElementCount>1;
            }
            if(ability>0 && stats!=null && stats.CurrentMP<Db.SpellMp(ability,0))continue;
            // Defend sits in the side command group; SelectCommand switches to it.
            if(rule.Command!=Defend && !CommandEnabled(info,rule.Command))continue;
            pendingCommand=rule.Command;pendingAbility=ability;pendingRule=r;targetAll=all;
            if(rule.OncePerBattle) {usedOnce.Add((r,actor.Pointer));turnActor=actor.Pointer;turnRule=r;}
            Log(actor,$"{(ability>0?Db.SpellName(ability):"command "+rule.Command)}{(all?" on all":"")} (plan rule {r+1})");abilityConfirms=abilityStuck=0;abilityStride=0;abilityLastIndex=-1;abilityLastKey=null;
            return;
        }
    }
    private void Block()
    {
        if(pendingRule>=0)blockedRules.Add(pendingRule);
        else if(pendingAbility==CureAbility && lastActor!=IntPtr.Zero)blockedCure.Add(lastActor);
        else if(pendingAbility==LifeAbility && lastActor!=IntPtr.Zero)blockedLife.Add(lastActor);
        else if(pendingAbility is ToadSpell or MiniSpell or EsunaAbility && lastActor!=IntPtr.Zero)blockedStatusSpell.Add(lastActor);
        pendingRule=-1;pendingAbility=0;pendingTarget=0;castOnUndead=false;
    }
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
        var quantity=baseController?.TryCast<Serial.FF0.UI.KeyInput.BattleQuantityAbilityInfomationController>();
        var list=quantity?.dataList;
        if(baseController==null || list==null)return;
        // The live cursor object tracks the highlighted row; selectCursorIndex
        // is not updated by this list (it stayed 0 and the cursor ran to the end).
        var cursors=baseController.selectCursorList;
        int index=cursors!=null && cursors.Count>0 && cursors[0]!=null?cursors[0].Index:baseController.selectCursorIndex,desired=-1;
        for(int i=0;i<list.Count;i++)if(list[i]?.Ability?.Id==pendingAbility) {desired=i;break;}
        // Rows shown on screen are the authority when they carry data.
        var rows=quantity!.contentList;
        if(rows!=null && rows.Count==list.Count)
            for(int i=0;i<rows.Count;i++)if(rows[i]?.Data?.Ability?.Id==pendingAbility) {desired=i;break;}
        // Not learned (or no list): back out; the next command pass falls through to Attack.
        if(desired<0 || abilityConfirms>3 || abilityStuck>=3) {Block();Pulse(state,frame,"Back","Planned ability not available");return;}
        if(frame%12!=0) {state.Detail="Find planned ability";return;}
        // Every move must move the cursor; if it doesn't, the list can't be read
        // reliably, so give up on this spell for the battle instead of looping.
        if(abilityLastKey is "Up" or "Down" or "Left" or "Right" && abilityLastIndex>=0)
            abilityStuck=index==abilityLastIndex?abilityStuck+1:0;
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
    // Items the battle list didn't offer this fight (inventory and battle list
    // can disagree); never pick them again until the next battle.
    private readonly HashSet<int> blockedItems=new();
    private bool HasItem(int contentId)
    {
        if(blockedItems.Contains(contentId))return false;
        var items=UserDataManager.Instance()?.normalOwnedItems;
        return items!=null && items.TryGetValue(contentId,out var item) && item!=null && item.Count>0;
    }
    // Command groups (KeyInput BattleCommandSelectController.State): Change 1
    // (Left: Row), Normal 2, Defence 3 (Right: Defend).
    private void SelectCommand(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        var c=info.commandSelectController;if(c?.selectCursor==null || c.stateMachine==null)return;
        int group=(int)c.stateMachine.Current,needed=wanted==Defend?3:wanted==Row?1:2;
        if(group!=needed) {Pulse(state,frame,group<needed?"Right":"Left",needed==3?"Open Defend commands":needed==1?"Open Row command":"Return to normal commands");return;}
        int index=c.selectCursor.Index;
        if(c.contentList==null || index<0 || index>=c.contentList.Count)return;
        var selected=c.contentList[index];
        if(selected?.TargetCommand?.Id==wanted && selected.IsEnable)
        {if(wanted==Row && frame%12==0)rowDone=true;
         Pulse(state,frame,"Confirm",wanted==Defend?"Choose Defend":wanted==Row?"Choose Row":wanted==3?"Use recovery item":"Choose Attack");return;}
        int wantedIndex=-1;
        for(int i=0;i<c.contentList.Count;i++)if(c.contentList[i]?.TargetCommand?.Id==wanted) {wantedIndex=i;break;}
        // Row missing from its group: Defend instead of stopping the run.
        if(wantedIndex<0 && wanted==Row) {rowDone=true;pendingCommand=Defend;return;}
        if(wantedIndex<0) {state.Failure=$"Required native command {wanted} not visible; stop instead of guessing";return;}
        Pulse(state,frame,wantedIndex<index?"Up":"Down","Select battle command");
    }
    // Held items (a rod in hand): the item list's Up from the top opens the
    // hand row (BattleItemInfomationController.State UseEquipSelect 2,
    // equipContnetList), where Left/Right picks the hand (user: "you go to
    // item menu then push up, then it shows your held items").
    private bool heldUse;
    private const int ItemUseSelect=1,ItemUseEquipSelect=2;
    private static bool Holds(BattlePlayerData actor,int content)
    {
        var eq=actor.GetOwnedCharacterData()?.EquipmentData;
        if(eq==null)return true; // can't tell: try it (SelectItem backs out if absent)
        foreach(var kv in eq)if(kv.Value?.ContentId==content)return true;
        return false;
    }
    private void SelectHeld(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        var c=info.itemSelectController;if(c?.selectCursor==null || c.stateMachine==null)return;
        int st=(int)c.stateMachine.Current;
        if(st==ItemUseSelect) {Pulse(state,frame,"Up","Up to the held items");return;}
        if(st!=ItemUseEquipSelect)return;
        var rows=c.equipContnetList;int desired=-1;
        for(int i=0;rows!=null && i<rows.Count;i++)if(rows[i]?.Data?.contentId==wanted) {desired=i;break;}
        if(desired<0) {blockedItems.Add(wanted);Pulse(state,frame,"Back","Held item not in hand");return;}
        int index=c.selectCursor.Index;
        if(index==desired) {Pulse(state,frame,"Confirm","Use held item");return;}
        Pulse(state,frame,desired<index?"Left":"Right","Find held item");
    }
    private void SelectItem(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        if(heldUse) {SelectHeld(info,wanted,state,frame);return;}
        var c=info.itemSelectController;if(c?.selectCursor==null || c.displayDataList==null)return;
        int index=c.selectCursor.Index,desired=-1;
        for(int i=0;i<c.displayDataList.Count;i++)
            if(c.displayDataList[i]?.contentId==wanted && c.displayDataList[i].Count>0) {desired=i;break;}
        if(desired<0) {blockedItems.Add(wanted);Pulse(state,frame,"Back","Recovery item unavailable");return;}
        if(index==desired) {Pulse(state,frame,"Confirm","Choose recovery item");return;}
        // Native normal item grid has two columns (NormalItemSkipCount = 2).
        Pulse(state,frame,index%2!=desired%2?(desired%2==0?"Left":"Right"):(desired<index?"Up":"Down"),"Find recovery item");
    }
    private int partySidePresses;
    // Enemies are on the left and the party on the right, except in a back
    // attack, where the sides swap: Left never reached Scarmiglione (segment
    // 6, Milon Z). Every 3 presses that don't get there try the other way.
    private static string SideKey(BattleInfomationController info,bool enemies,int presses)
    {
        bool left=enemies!=info.isBackAttack;
        if(presses/3%2==1)left=!left;
        return left?"Left":"Right";
    }
    private void SelectHealingTarget(BattleInfomationController info,Snapshot state,int frame)
    {
        var c=info.targetSelectController;if(c?.stateMachine==null)return;
        if(pendingTarget==0) {Pulse(state,frame,"Back","Cancel unknown healing target");return;}
        if(unreachable.Contains(pendingTarget)) {Pulse(state,frame,"Back","Target unreachable; choose again");return;}
        if((int)c.stateMachine.Current!=1) {if(frame%12==0)partySidePresses++;Pulse(state,frame,SideKey(info,false,partySidePresses),"Select party side");return;}
        partySidePresses=0;
        var targets=c.GetPlayerTarget();
        if(targets!=null && targets.Count==1 && targets[0].GetOwnedCharacterData()?.characterStatusId==pendingTarget)
        {targetPresses=0;Pulse(state,frame,"Confirm","Heal selected party member");}
        // The cursor went round the party without reaching them (a state the
        // checks above missed): give up on them for this fight and back out.
        else if(targetPresses>TargetGiveUp) {unreachable.Add(pendingTarget);targetPresses=0;Pulse(state,frame,"Back","Target unreachable; choose again");}
        else {if(frame%12==0)targetPresses++;Pulse(state,frame,"Down","Find injured party member");}
    }
    private static void Pulse(Snapshot state,int frame,string key,string detail)
    {state.Detail=string.IsNullOrEmpty(state.Detail)?detail:state.Detail+"; "+detail;if(frame%12==0)state.BattleInput=key;}
}
