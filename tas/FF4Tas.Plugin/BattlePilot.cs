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
        menu=null;pendingItem=pendingTarget=pendingAbility=stalled=abilityStride=0;pendingCommand=1;
        abilityLastIndex=-1;abilityLastKey=null;lastInfo=lastCursor=-1;lastActor=IntPtr.Zero;previousHp=-1;
        pendingRule=-1;abilityConfirms=abilityStuck=targetPresses=0;decision="";
        blockedRules.Clear();blockedCure.Clear();blockedItems.Clear();blockedStatusSpell.Clear();usedOnce.Clear();wasMist=false;
        fleeFrames=0;sidePresses=0;bossSeen=false;turnActor=IntPtr.Zero;turnRule=-1;fightSeconds=0;fightFrame=-1;rowDone=false;
    }
    internal void Read(Snapshot state,int frame,BattleRule[]? plan=null,int[]? fleeMonsters=null)
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
        BattlePlayerData? injured=null,dead=null;int alive=0,totalHp=0;float injuredRatio=2;casterCanCure=false;injuredRatioNow=1;toad=mini=null;
        rosaParty=rosaCanCure=false;rosaTarget=rosaLow=null;float rosaKey=99;
        for(int i=0;i<players.Length;i++)if(players[i]?.GetOwnedCharacterData()?.characterStatusId==Rosa)rosaParty=true;
        for(int i=0;i<players.Length;i++)
        {
            var p=players[i];var stats=p?.BattleUnitDataInfo?.Parameter;if(stats==null)continue;
            int hp=stats.CurrentHP;totalHp+=hp;
            int rank=HealRank(p!);
            // Rydia and Edward leave the party after Mount Hobs: once dead they
            // stay down and the rest keep killing (user: "they can die").
            bool skipRevive=rosaParty && p!.GetOwnedCharacterData()?.characterStatusId is Rydia or Edward;
            if(hp<=0) {if(rank>=0 && !skipRevive && (dead==null || rank<HealRank(dead)))dead=p;continue;}
            alive++;
            float ratio=hp/(float)Math.Max(1,stats.ConfirmedMaxHp());
            int who=p!.GetOwnedCharacterData()?.characterStatusId??0;
            // Toad/Mini stop casting and last after the fight: cure them first.
            var conditions=stats.CurrentConditionList;
            for(int c=0;conditions!=null && c<conditions.Count;c++)
            {
                int cid=conditions[c]?.Id??0;
                if(cid is ToadCondition or ToadCondition2 && toad==null)toad=p;
                if(cid is MiniCondition or MiniCondition2 && mini==null)mini=p;
            }
            if(who is Rydia or Tellah or Rosa && stats.CurrentMP>=CureMp)casterCanCure=true;
            if(who==Rosa && stats.CurrentMP>=CureMp)rosaCanCure=true;
            if(who==Rosa && ratio<HealBelow)rosaLow=p;
            if(rank<0)continue;
            // Rosa tops up her priority allies earlier (under 75%).
            if(rosaParty && ratio<(rank==0?RosaHealBelow:HealBelow) && rank+ratio<rosaKey) {rosaTarget=p;rosaKey=rank+ratio;}
            if(ratio>=HealBelow)continue;
            float key=rosaParty?rank+ratio:ratio-(IsCecil(p!)?.05f:0);
            if(key<injuredRatio) {injured=p;injuredRatio=key;injuredRatioNow=ratio;}
        }
        // Scripted losses (Titan at Mist) wipe the party and the story goes on;
        // a real game over leaves the battle for GotoTitle, which no step allows.
        if(players.Length>0 && alive==0) {state.Detail="Party down; waiting for the game";return;}
        var enemyText=new System.Text.StringBuilder();bool mist=false,hasEnemy=false;scripted=false;fleeTarget=false;undead=null;weakest=null;undeadCount=livingEnemies=0;tough=boss=false;Array.Clear(elementCounts);livingIds.Clear();
        for(int i=0;i<enemies.Length;i++)
        {
            var e=enemies[i];var p=e?.BattleUnitDataInfo?.Parameter;
            if(e==null || p==null || p.CurrentHP<=0 || e.IsRemove)continue;
            hasEnemy=true;livingEnemies++;
            if(e.MonsterMaster!=null)livingIds.Add(e.MonsterMaster.Id);
            var master=e.MonsterMaster;
            if(master!=null)
            {
                if(master.Defense>=ToughDefense)tough=true;
                if(master.Boss!=0)boss=bossSeen=true;
                foreach(var el in WeakTo(master))if(el is >=1 and <=3)elementCounts[el]++;
                enemyText.Append(enemyText.Length>0?", ":"").Append(Db.MonsterName(master.Id)).Append(' ').Append(p.CurrentHP).Append("hp");
            }
            // Story fights you can't win or lose normally (the Girl at Mist has
            // 60000 HP): never spend items or MP on them.
            if(e.MonsterMaster?.Id==207 || p.ConfirmedMaxHp()>=30000)scripted=true;

            if(weakest==null || p.CurrentHP<weakest.BattleUnitDataInfo.Parameter.CurrentHP)weakest=e;
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
        // Run from listed encounter groups (never bosses or story fights): hold both
        // shoulder keys, which BattleMenuController.IsEscapeInput polls. If the
        // party hasn't escaped after ~10 s (fight can't be fled), fight on.
        // ...and run from any ordinary fight that is taking too long.
        // A toad/mini in an ordinary fight: run first, cure with the item in the
        // next fight that is actually fought (user's call).
        bool slow=fightSeconds>SlowFightSeconds,toughFight=FleeTough && tough,status=toad!=null || mini!=null;
        if((fleeTarget || slow || toughFight || status) && !boss && !scripted && fleeFrames<FleeGiveUp)
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
                SelectCommand(info,pendingItem!=0?3:mist?2:pendingCommand,state,frame);
                break;
            case 4:
                if(pendingAbility==0) {Block();Pulse(state,frame,"Back","Cancel unexpected ability list");break;}
                SelectAbility(info,state,frame);break;
            case 5:
                if(pendingTarget!=0)SelectHealingTarget(info,state,frame);
                else SelectEnemy(info,state,frame,"Cast on selected enemy",castOnUndead?undead:weakest,targetAll || (castOnUndead && undeadCount>1));
                break;
            case 2:
                if(pendingItem==0) {Pulse(state,frame,"Back","Cancel unexpected item list");break;}
                SelectItem(info,pendingItem,state,frame);break;
            case 3:
                if(itemOnEnemies)SelectEnemy(info,state,frame,"Throw item at the enemies",weakest,true);
                else SelectHealingTarget(info,state,frame);
                break;
            case 6:
                if(mist) {Pulse(state,frame,"Back","Cancel attack: dragon became mist");break;}
                SelectEnemy(info,state,frame,"Attack weakest enemy",weakest);break;
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
        if(side!=EnemySingleState) {if(frame%12==0)sidePresses++;Pulse(state,frame,"Left","Select enemy side");return;}
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
    private const int DarknessCommand=10;
    private const int EnemySingleState=3,EnemyAllState=4;
    private int undeadCount,livingEnemies;
    private bool targetAll,tough,boss,fleeTarget;
    internal List<HashSet<int>> FleeParties=new();
    internal bool FleeTough;
    private int fleeFrames;
    private const int FleeGiveUp=600;
    private const float SlowFightSeconds=60;
    private float fightSeconds;private int fightFrame=-1;
    // Back attack flips the rows: one non-Cecil actor uses Row once to fix it.
    private bool rowDone;
    private const int Row=5,Rydia=4,Rosa=3;
    // Master condition ids (both entries named Toad/Mini) and their cure items.
    private const int ToadCondition=14,ToadCondition2=58,MiniCondition=13,MiniCondition2=57,MaidensKiss=11,Mallet=12;
    private BattlePlayerData? toad,mini;
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
    private const int Defend=2,UndeadSpecies=4,WhiteMagic=7,CureAbility=6,CureMp=3,Tellah=6;
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
    private bool rosaParty,rosaCanCure,bossSeen;
    private IntPtr turnActor;private int turnRule=-1;private bool itemOnEnemies;
    private readonly HashSet<int> livingIds=new();private BattlePlayerData? rosaTarget,rosaLow;
    private const float RosaHealBelow=.75f;
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
    private void Decide(BattleInfomationController info,BattlePlayerData actor,BattlePlayerData? dead,BattlePlayerData? injured,bool mist,BattleRule[]? plan)
    {
        pendingItem=0;pendingTarget=0;pendingCommand=1;pendingAbility=0;pendingRule=-1;castOnUndead=false;targetAll=false;itemOnEnemies=false;
        abilityConfirms=abilityStuck=abilityStride=0;abilityLastIndex=-1;abilityLastKey=null;
        if(mist) {Log(actor,"Defend: Mist Dragon in mist");return;}
        if(scripted) {Log(actor,"Attack: scripted fight, no items or MP");return;}
        var stats=actor.BattleUnitDataInfo?.Parameter;
        bool isTellah=actor.GetOwnedCharacterData()?.characterStatusId==Tellah;
        bool canCure=stats!=null && stats.CurrentMP>=CureMp && CommandEnabled(info,WhiteMagic) && !blockedCure.Contains(actor.Pointer);
        int me=actor.GetOwnedCharacterData()?.characterStatusId??0;
        // Back attack: the weak front-liner (Rydia now; anyone but Cecil) swaps
        // the party's rows back with Row, once per fight.
        if(info.isBackAttack && !rowDone && !IsCecil(actor) && livingEnemies>0)
        {rowDone=true;pendingCommand=Row;Log(actor,"Row (back attack)");return;}
        // Status cures before healing: Maiden's Kiss for Toad, Mallet for Mini.
        if(toad!=null && HasItem(MaidensKiss)) {pendingItem=MaidensKiss;pendingTarget=toad.GetOwnedCharacterData().characterStatusId;Log(actor,"Maiden's Kiss on #"+pendingTarget+" (toad)");return;}
        if(mini!=null && HasItem(Mallet)) {pendingItem=Mallet;pendingTarget=mini.GetOwnedCharacterData().characterStatusId;Log(actor,"Mallet on #"+pendingTarget+" (mini)");return;}
        // No item: the same spell toggles the status back (Toad on a toad, Mini
        // on a mini), cast by someone who isn't affected and can afford it.
        if(stats!=null && !blockedStatusSpell.Contains(actor.Pointer) && actor.Pointer!=(toad?.Pointer??IntPtr.Zero) && actor.Pointer!=(mini?.Pointer??IntPtr.Zero))
        {
            if(toad!=null && stats.CurrentMP>=Db.SpellMp(ToadSpell,7) && CommandEnabled(info,BlackMagic))
            {pendingCommand=BlackMagic;pendingAbility=ToadSpell;pendingTarget=toad.GetOwnedCharacterData().characterStatusId;Log(actor,"Toad on #"+pendingTarget+" (undo toad)");return;}
            if(mini!=null && stats.CurrentMP>=Db.SpellMp(MiniSpell,6) && CommandEnabled(info,WhiteMagic))
            {pendingCommand=WhiteMagic;pendingAbility=MiniSpell;pendingTarget=mini.GetOwnedCharacterData().characterStatusId;Log(actor,"Mini on #"+pendingTarget+" (undo mini)");return;}
        }
        // Rosa always heals: Rosa/Cecil/Yang under 75% (lowest first), then
        // Rydia/Edward under 50%; else she Defends.
        if(me==Rosa && rosaTarget!=null && canCure)
        {pendingCommand=WhiteMagic;pendingAbility=CureAbility;pendingTarget=rosaTarget.GetOwnedCharacterData().characterStatusId;Log(actor,"Cure #"+pendingTarget+" (Rosa heals)");return;}
        if(dead!=null && HasItem(9)) {pendingItem=9;pendingTarget=dead.GetOwnedCharacterData().characterStatusId;Log(actor,"Phoenix Down on #"+pendingTarget);return;}
        // With Rosa alive and able to Cure, nobody else heals: Rydia Cured and
        // Yang/Cecil used Potions through the Mom Bomb split instead of
        // fighting, and the party lost.
        // ...except Rosa herself: when she is under half HP anyone heals her
        // (user), with Cure or a Potion.
        if(rosaParty && me!=Rosa && rosaLow!=null)injured=rosaLow;
        bool leaveToRosa=rosaParty && me!=Rosa && rosaCanCure && injured!=rosaLow;
        if(injured!=null && !leaveToRosa)
        {
            int target=injured.GetOwnedCharacterData().characterStatusId;
            // White Magic first for everyone; Tellah only above his plan's MP
            // reserve. Potions are the fallback when nobody can cast.
            bool tellahCanSpare=!isTellah || stats!.CurrentMP>=CureMp+Reserve(plan,Tellah);
            if(canCure && tellahCanSpare) {pendingCommand=WhiteMagic;pendingAbility=CureAbility;pendingTarget=target;Log(actor,"Cure ally #"+target+" (under half HP)");return;}
            // Non-casters (Cecil) leave healing to the casters: a Potion only
            // when no caster has the MP to Cure (user: Cecil kept healing with
            // Rydia right behind him in the ATB).
            if(HasItem(2) && (!casterCanCure || (injured==rosaLow && me!=Rosa)))
            {pendingItem=2;pendingTarget=target;Log(actor,"Potion on ally #"+target);return;}
        }
        // Cure hurts undead: cast it (on all of them when there are several).
        // Tellah joins in while he stays above the MP his route plan reserves.
        if(undead!=null && canCure && (!isTellah || stats!.CurrentMP>=CureMp+Reserve(plan,Tellah)))
        {pendingCommand=WhiteMagic;pendingAbility=CureAbility;castOnUndead=true;Log(actor,$"Cure{(undeadCount>1?" on all":"")} vs undead");return;}
        if(me==Rosa && rosaParty) {pendingCommand=Defend;Log(actor,"Defend (nobody to heal)");return;}
        // Edward just attacks (user: "edward can just attack and die").
        if(me==Edward) {Log(actor,"Attack weakest enemy");return;}
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
    private readonly HashSet<IntPtr> blockedCure=new();
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
    private void ChoosePlan(BattleInfomationController info,BattlePlayerData actor,BattleRule[]? plan)
    {
        if(plan==null)return;
        var stats=actor.BattleUnitDataInfo?.Parameter;
        int id=actor.GetOwnedCharacterData()?.characterStatusId??0;
        for(int r=0;r<plan.Length;r++)
        {
            var rule=plan[r];
            if(blockedRules.Contains(r))continue;
            // Decide re-runs every frame of a turn: the rule this turn already
            // took stays allowed, so a once-per-battle rule isn't skipped
            // before it is ever input (Yang skipped Attack x2 + Focus).
            if(rule.OncePerBattle && usedOnce.Contains((r,actor.Pointer)) && !(actor.Pointer==turnActor && r==turnRule))continue;
            if(rule.BossOnly && !boss)continue;
            if(rule.Character!=null && rule.Character!=id)continue;
            if(stats!=null && rule.MinHpPercent>0 && stats.CurrentHP*100<rule.MinHpPercent*Math.Max(1,stats.ConfirmedMaxHp()))continue;
            if(stats!=null && rule.MinMp>0 && stats.CurrentMP<rule.MinMp)continue;
            if(rule.MinEnemies>0 && livingEnemies<rule.MinEnemies && !(rule.OrTough && tough))continue;
            if(rule.WhileMonster is int whileId && !livingIds.Contains(whileId))continue;
            // Darkness is an HP-costing wave that can kill Cecil (user rule).
            // Only on large groups (4+), and only while Cecil is healthy.
            if(rule.Command==DarknessCommand && (livingEnemies<4 || (stats!=null && stats.CurrentHP*10<8*Math.Max(1,stats.ConfirmedMaxHp()))))continue;
            if(rule.Item is int item)
            {
                if(!HasItem(item))continue;
                pendingItem=item;pendingTarget=rule.TargetEnemies?0:rule.TargetCharacter??id;pendingRule=r;itemOnEnemies=rule.TargetEnemies;
                if(rule.OncePerBattle) {usedOnce.Add((r,actor.Pointer));turnActor=actor.Pointer;turnRule=r;}
                Log(actor,$"item {item} on #{pendingTarget} (plan rule {r+1})");
                return;
            }
            int ability=rule.Ability??0;bool all=rule.TargetAll;
            if(rule.ByWeakness)
            {
                if(bestElement==0)continue;
                ability=ElementSpell[bestElement];
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
        else if(pendingAbility is ToadSpell or MiniSpell && lastActor!=IntPtr.Zero)blockedStatusSpell.Add(lastActor);
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
        {Pulse(state,frame,"Confirm",wanted==Defend?"Choose Defend":wanted==Row?"Choose Row":wanted==3?"Use recovery item":"Choose Attack");return;}
        int wantedIndex=-1;
        for(int i=0;i<c.contentList.Count;i++)if(c.contentList[i]?.TargetCommand?.Id==wanted) {wantedIndex=i;break;}
        // Row missing from its group: Defend instead of stopping the run.
        if(wantedIndex<0 && wanted==Row) {pendingCommand=Defend;return;}
        if(wantedIndex<0) {state.Failure=$"Required native command {wanted} not visible; stop instead of guessing";return;}
        Pulse(state,frame,wantedIndex<index?"Up":"Down","Select battle command");
    }
    private void SelectItem(BattleInfomationController info,int wanted,Snapshot state,int frame)
    {
        var c=info.itemSelectController;if(c?.selectCursor==null || c.displayDataList==null)return;
        int index=c.selectCursor.Index,desired=-1;
        for(int i=0;i<c.displayDataList.Count;i++)
            if(c.displayDataList[i]?.contentId==wanted && c.displayDataList[i].Count>0) {desired=i;break;}
        if(desired<0) {blockedItems.Add(wanted);Pulse(state,frame,"Back","Recovery item unavailable");return;}
        if(index==desired) {Pulse(state,frame,"Confirm","Choose recovery item");return;}
        // Native normal item grid has two columns (NormalItemSkipCount = 2).
        Pulse(state,frame,index%2!=desired%2?(desired%2==0?"Left":"Right"):(desired<index?"Up":"Down"),"Find recovery item");
    }
    private void SelectHealingTarget(BattleInfomationController info,Snapshot state,int frame)
    {
        var c=info.targetSelectController;if(c?.stateMachine==null)return;
        if(pendingTarget==0) {Pulse(state,frame,"Back","Cancel unknown healing target");return;}
        if((int)c.stateMachine.Current!=1) {Pulse(state,frame,"Right","Select party side");return;}
        var targets=c.GetPlayerTarget();
        if(targets!=null && targets.Count==1 && targets[0].GetOwnedCharacterData()?.characterStatusId==pendingTarget)
            Pulse(state,frame,"Confirm","Heal selected party member");
        else Pulse(state,frame,"Down","Find injured party member");
    }
    private static void Pulse(Snapshot state,int frame,string key,string detail)
    {state.Detail=string.IsNullOrEmpty(state.Detail)?detail:state.Detail+"; "+detail;if(frame%12==0)state.BattleInput=key;}
}
