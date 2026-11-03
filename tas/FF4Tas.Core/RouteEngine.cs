namespace FF4Tas.Core;

public sealed class Route
{
    public int Schema { get; set; } = 1;
    public string Name { get; set; } = "";
    public Step[] Steps { get; set; } = Array.Empty<Step>();
    // Monster id -> elements it is weak to (1 fire, 2 ice, 3 lightning), from the
    // master monster/attribute_group tables (tas/weaknesses.py). Read by plans.
    public Dictionary<int,int[]> Weaknesses { get; set; } = new();
    // Specific encounter groups to run from, each the set of distinct monster
    // ids in the group (e.g. [22, 9] = 4 Sahagin + 2 Tiny Mage at the
    // waterfall). Escape = hold SwitchLeft + SwitchRight. Never bosses.
    public int[][] FleeParties { get; set; } = Array.Empty<int[]>();
    // Run at once when a fight has at least this many of one monster:
    // [monster id, count], e.g. [36, 4] = 4 Skeletons on Mount Hobs.
    public int[][] FleeCounts { get; set; } = Array.Empty<int[]>();
    // Run at once from tough ordinary fights (an enemy with defense >= 200,
    // which attacks barely scratch) instead of spending MP on them.
    public bool FleeTough { get; set; }
    public void Validate()
    {
        if(Schema!=1 || string.IsNullOrWhiteSpace(Name) || Steps==null || Steps.Length is <1 or >10000)
            throw new ArgumentException("Invalid route header");
        foreach(var s in Steps)
        {
            if(s==null || string.IsNullOrWhiteSpace(s.Label) || s.Phases==null || s.Keys==null
                || s.Phases.Length==0 || s.Phases.Any(p=>!KnownPhases.Contains(p)))
                throw new ArgumentException("Each step needs a label and allowed game phases");
            if(s.Kind is not ("hold" or "wait" or "mash" or "checkpoint" or "navigate" or "interact" or "battle" or "menu"))throw new ArgumentException("Unknown step kind");
            if(s.TimeoutFrames is <1 or >216000 || s.StableFrames is <1 or >600)
                throw new ArgumentException("Invalid step timeout or stability interval");
            if(s.Keys.Any(k=>!KnownKeys.Contains(k)) || s.Keys.Distinct().Count()!=s.Keys.Length)
                throw new ArgumentException("Unknown/duplicate logical input");
            if((s.Keys.Contains("Up") && s.Keys.Contains("Down")) || (s.Keys.Contains("Left") && s.Keys.Contains("Right")))
                throw new ArgumentException("Opposing directions");
            if(s.Kind=="hold" && (s.Frames<1 || s.Frames>s.TimeoutFrames || s.Keys.Length==0))throw new ArgumentException("Invalid hold");
            if(s.Kind=="mash" && (s.Keys.Length==0 || s.PeriodFrames<2 || s.PressedFrames<1 || s.PressedFrames>=s.PeriodFrames))
                throw new ArgumentException("Mash requires a release interval");
            if(s.Kind is "wait" or "mash" or "checkpoint" or "navigate" or "interact" or "battle" or "menu")
                if(s.Until==null || (s.Until.Phase==null && s.Until.MapId==null && s.Until.StoryFlag==null && s.Until.TreasureFlag==null && s.Until.Flag2==null && s.Until.Riding==null && !s.Until.AtDestination && !s.Until.MenuDone && s.Until.GilAtLeast==null && string.IsNullOrWhiteSpace(s.Until.Visual)))throw new ArgumentException("Step needs a completion condition");
            if(s.Until?.Phase!=null && !KnownPhases.Contains(s.Until.Phase))throw new ArgumentException("Unknown completion phase");
            if(s.Kind=="navigate" && (s.Goal==null || s.MapId==null || s.Goal.X is <0 or >1024 || s.Goal.Y is <0 or >1024 || s.Goal.Layer is <0 or >15))
                throw new ArgumentException("Navigation needs a map and valid destination");
            if(s.Waypoints!=null && (s.Kind!="navigate" || s.Waypoints.Length>256 || s.Waypoints.Any(w=>w==null || w.X is <0 or >1024 || w.Y is <0 or >1024 || w.Layer is <0 or >15)))
                throw new ArgumentException("Waypoints need a navigate step and valid cells");
            if(s.BattlePlan!=null && (!s.HandleBattles || s.BattlePlan.Length>32 || s.BattlePlan.Any(r=>r==null || r.Command is <1 or >64 || r.MinHpPercent is <0 or >100 || r.MinMp is <0 or >999 || r.ReserveMp is <0 or >999 || r.MinEnemies is <0 or >8 || r.Item is <1 or >5000 || r.Ability is <1 or >2000)))
                throw new ArgumentException("Battle plans need handleBattles and valid rules");
            if(s.Choice is <0 or >15)throw new ArgumentException("Invalid choice index");
            if((s.MenuItem!=null || s.Equip!=null || s.Shop!=null || s.Formation!=null) && s.Kind!="menu")throw new ArgumentException("Item/equip/shop need a menu step");
            if(s.Shop!=null && (s.Shop.Buy.Any(b=>b==null || b.Content<1 || b.Count is <1 or >99) || s.Shop.Sell.Any(c=>c<1)))throw new ArgumentException("Invalid shop order");
            if(s.Facing!=null && s.Facing is not ("Up" or "Down" or "Left" or "Right"))throw new ArgumentException("Invalid facing direction");
            if(s.Until?.StoryFlag is <0 or >255)throw new ArgumentException("Invalid story flag");
            if(s.Until?.TreasureFlag is <0 or >4095)throw new ArgumentException("Invalid treasure flag");
            if(s.Until?.AtDestination==true && s.Kind!="navigate")throw new ArgumentException("Destination condition requires navigation");
            if(s.Kind is "wait" or "checkpoint" && s.Keys.Length!=0)throw new ArgumentException("Wait/checkpoint cannot press keys");
        }
    }
    public static readonly string[] KnownKeys={"Confirm","Back","Menu","Shortcut","Up","Down","Left","Right","SwitchLeft","SwitchRight","PageUp","PageDown","Start","Select"};
    public static readonly string[] KnownPhases={"None","Init","ChangeMap","FieldReady","Player","Transport","Menu","GpsMenu","ExpandMinimap","Message","Shop","ChangeName","Gimmick","Event","Battle","GotoTitle","MiniGame","FieldHelp","MenuLibraryUi","MenuLibraryInfo"};
}
public sealed class FormationOrder
{
    public int[] Order {get;set;}=Array.Empty<int>();
    public int[] Front {get;set;}=Array.Empty<int>();
}
public sealed class ShopOrder
{
    public ShopBuy[] Buy {get;set;}=Array.Empty<ShopBuy>();
    // Content ids to sell every unequipped copy of (old gear).
    public int[] Sell {get;set;}=Array.Empty<int>();
}
public sealed class ShopBuy
{
    public int Content {get;set;}
    public int Count {get;set;}=1;
}
public sealed class Destination
{
    public int X {get;set;}
    public int Y {get;set;}
    public int Layer {get;set;}
}
public sealed class Step
{
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "wait";
    public string[] Phases { get; set; } = Array.Empty<string>();
    public int? MapId { get; set; }
    public string[] Keys { get; set; } = Array.Empty<string>();
    public int Frames { get; set; }
    public int TimeoutFrames { get; set; } = 3600;
    public int StableFrames { get; set; } = 1;
    public int PeriodFrames { get; set; } = 6;
    public int PressedFrames { get; set; } = 1;
    public Condition? Until { get; set; }
    public Destination? Goal {get;set;}
    // Optional hand/offline-planned cells walked in order before Goal, for maps
    // where the native search cannot plan (the overworld has no collision layer).
    public Destination[]? Waypoints {get;set;}
    // Walk the waypoints and goal over and over until the step's condition
    // holds (grinding random fights for gil).
    public bool Loop {get;set;}
    public string? Facing {get;set;}
    // 0-based option to pick whenever a native choice window opens during this
    // step (e.g. the Mist Cave warning defaults to "No"; 0 is "Yes").
    public int? Choice {get;set;}
    // Item to pick (content id, e.g. Sand Ruby 64) when an event opens the
    // native item list (SelectFieldContentManager), e.g. curing Rosa.
    public int? ItemChoice {get;set;}
    // "menu" step modes (default: Phoenix Down / Potion heal through the
    // field menu). MenuItem: use this item once on the party (Tent 21).
    public int? MenuItem {get;set;}
    // Equip: character_status ids to give the native "Strongest" equipment.
    public int[]? Equip {get;set;}
    // Shop: talk to the shopkeeper the step faces, then sell, then buy in
    // order (each buy only while affordable and owned+equipped < Count).
    public ShopOrder? Shop {get;set;}
    // Formation: character_status id per party slot in order (0 = leave the
    // slot empty, e.g. for Yang to join into), and the 0-based slots that
    // must be in the front row; everyone else goes to the back.
    public FormationOrder? Formation {get;set;}
    public bool HandleBattles {get;set;}
    // Ordered per-character battle rules; the first usable one is taken, and
    // anything unusable falls back to Attack. Recovery items still come first.
    public BattleRule[]? BattlePlan {get;set;}
    // During this step, run from any non-boss fight containing one of these
    // monster ids (e.g. Toadgre 19 / Tiny Mage 9 right before Octomammoth).
    public int[]? FleeMonsters {get;set;}
}
public sealed class BattleRule
{
    // Native character_status id (Cecil 1, Rydia 4, Tellah 6); null = anyone.
    public int? Character {get;set;}
    // Native command id (Attack 1, Black Magic 8, Darkness 10, ...).
    public int Command {get;set;}=1;
    // Native ability id inside a list command (Thunder 39, Fire 33, ...).
    public int? Ability {get;set;}
    // Only while the actor's HP is at least this percent of max.
    public int MinHpPercent {get;set;}
    // Only while the actor has at least this much MP (e.g. Thunder costs 5).
    public int MinMp {get;set;}
    // MP this character never spends outside the rule list (e.g. on Cure vs
    // undead), so it is still there for the boss.
    public int ReserveMp {get;set;}
    // Only when at least this many enemies are alive (group spells).
    public int MinEnemies {get;set;}
    // Only while this monster id is alive (Mom Bomb 165: before it explodes).
    public int? WhileMonster {get;set;}
    // ...and only while that monster's HP is at most / above this (Mom Bomb
    // grows at 10000 HP and explodes two actions later: everyone Defends).
    public int? WhileMonsterHpAtMost {get;set;}
    public int? WhileMonsterHpAbove {get;set;}
    // ...and only once the monster has (true) / hasn't yet (false) changed
    // sprite: the Mom Bomb's grow action is ChangeSprite (function 45).
    public bool? WhileMonsterGrown {get;set;}
    // Target the whole enemy side (one Thunder on all instead of three singles).
    public bool TargetAll {get;set;}
    // Pick Fire/Blizzard/Thunder by the living enemies' shared weakness
    // (Route.Weaknesses); skipped when nothing on screen is weak to one.
    public bool ByWeakness {get;set;}
    // Also allowed below MinEnemies when an enemy shrugs off physical hits
    // (defense >= 200, e.g. Red Mousse / Yellow Jelly at 254).
    public bool OrTough {get;set;}
    // Use once per battle per character, then fall through to the next rule
    // (community Mist Dragon/Octomammoth strat: one Darkness wave, then
    // Attack; re-armed after the Mist Dragon's mist phase).
    public bool OncePerBattle {get;set;}
    // Use this item (content id, e.g. Hermes Shoes 55) instead of a command,
    // on TargetCharacter (character_status id) or, when null, on the actor.
    public int? Item {get;set;}
    public int? TargetCharacter {get;set;}
    // Throw the item at the enemies instead (Antarctic Wind 38, Bomb Fragment).
    public bool TargetEnemies {get;set;}
    // Only in boss battles (monster master boss = 1), e.g. HP-costing Darkness.
    public bool BossOnly {get;set;}
}
public sealed class Condition
{
    public string? Phase { get; set; }
    public int? MapId { get; set; }
    public string? Visual { get; set; }
    public int? StoryFlag {get;set;}
    // Treasure chest opened (DataStorage kTreasureFlag1, the map entity's flag_id).
    public int? TreasureFlag {get;set;}
    // Second scenario flag bank (kScenarioFlag2), e.g. Damcyan jail switch 27.
    public int? Flag2 {get;set;}
    public bool AtDestination {get;set;}
    // Riding a vehicle (hovercraft, ship...) or on foot.
    public bool? Riding {get;set;}
    // A "menu" step finished its field-menu work and the field is back.
    public bool MenuDone {get;set;}
    // Party gil at least this much (grind before the Fabul shops).
    public int? GilAtLeast {get;set;}
    public bool Matches(Snapshot state)=>(Phase==null || state.Phase==Phase) && (MapId==null || state.MapId==MapId)
        && (Visual==null || state.VisualMatches.Contains(Visual))
        && (StoryFlag==null || state.StoryFlags.Contains(StoryFlag.Value))
        && (TreasureFlag==null || state.TreasureFlags.Contains(TreasureFlag.Value))
        && (Flag2==null || state.Flag2s.Contains(Flag2.Value))
        && (!AtDestination || state.AtDestination)
        && (Riding==null || state.Riding==Riding)
        && (!MenuDone || state.MenuDone)
        && (GilAtLeast==null || state.Gil>=GilAtLeast);
}
public sealed class Snapshot
{
    public string Phase { get; set; } = "None";
    public int MapId { get; set; }
    public bool Focused { get; set; } = true;
    public string[] VisualMatches { get; set; } = Array.Empty<string>();
    public int[] StoryFlags {get;set;}=Array.Empty<int>();
    public int[] TreasureFlags {get;set;}=Array.Empty<int>();
    public int[] Flag2s {get;set;}=Array.Empty<int>();
    public float? X {get;set;}
    public float? Y {get;set;}
    public int? Layer {get;set;}
    public bool AtDestination {get;set;}
    // FieldPlayer.moveState is a vehicle (not Walk/Dush).
    public bool Riding {get;set;}
    public string? NavigationInput {get;set;}
    public string? BattleInput {get;set;}
    // Field-menu driver ("menu" steps): key to press, and whether it is done.
    public string? MenuInput {get;set;}
    public bool MenuDone {get;set;}
    public int Gil {get;set;}
    // The plugin is healing between fights through the field menu (after a
    // battle on a walking step): MenuInput keys go out instead of the step's.
    public bool FieldHeal {get;set;}
    // A one-button modal popup (native or a mod's, e.g. "Job unlocked!") is
    // open over the field; it blocks movement until confirmed.
    public bool Modal {get;set;}
    // Cursor index of an open native choice window, null when none is open.
    public int? ChoiceIndex {get;set;}
    // Event item list: cursor index and the wanted entry's index (-1 = absent).
    public int? ItemListIndex {get;set;}
    public int ItemListWanted {get;set;}=-1;
    public string? Failure {get;set;}
    public string Detail {get;set;}="";
    // Battle log line: enemies on screen and the last decision with its reason.
    public string Battle {get;set;}="";
    // Read-only encounter forecast (EncounterWatch).
    public string Encounter {get;set;}="";
}

// Frames are observed Unity updates, not emulated/savestated simulation frames.
// All reads within a frame see the same input, regardless of polling order.
public sealed class RouteEngine
{
    private readonly Route route;
    private readonly HashSet<string> current=new(), previous=new();
    private long lastFrame=-1;
    private int elapsed,stable,refocused;
    // Below this many focused frames after a pause, input stays released so the
    // click that refocused the window settles before the route acts again.
    private const int RefocusFrames=30;
    private bool finishStep;
    public RouteEngine(Route route) { route.Validate();this.route=route; }
    public int Index { get; private set; }
    public string Status { get; private set; } = "running";
    public string Reason { get; private set; } = "";
    public string Label=>Index<route.Steps.Length?route.Steps[Index].Label:"Finished";
    public bool Running=>Status=="running";
    public bool Paused { get; private set; }
    // The bridge prepares the upcoming step before Tick advances the index.
    public Step? ActiveStep=>Running && Index+(finishStep?1:0)<route.Steps.Length ? route.Steps[Index+(finishStep?1:0)] : null;
    public bool Held(string key)=>current.Contains(key);
    public bool Down(string key)=>current.Contains(key) && !previous.Contains(key);
    public bool Up(string key)=>!current.Contains(key) && previous.Contains(key);
    public void Stop(string reason) { Status="stopped";Paused=false;Reason=reason;current.Clear();previous.Clear(); }
    private void Fail(string reason) { Stop(reason);Status="failed"; }
    public void Tick(long frame,Snapshot state)
    {
        if(!Running || frame==lastFrame)return;
        if(frame<lastFrame) { Fail("Frame counter moved backwards");return; }
        lastFrame=frame;
        previous.Clear();previous.UnionWith(current);current.Clear();
        // Focus loss pauses instead of stopping: input is released and step timers
        // freeze until the window is focused again, then the run continues.
        if(!state.Focused)
        {
            if(!Paused) { Paused=true;Reason="Paused: game not focused (click the game to resume)"; }
            refocused=0;current.Clear();previous.Clear();return;
        }
        if(Paused)
        {
            if(++refocused<RefocusFrames)return;
            Paused=false;Reason="";
        }
        if(state.Failure!=null) { Fail(state.Failure);return; }
        if(finishStep) { Index++;elapsed=stable=0;finishStep=false; }
        if(Index==route.Steps.Length) { Status="complete";return; }
        var step=route.Steps[Index];
        // Completion is checked before the active-phase guard. Input is released
        // as soon as the destination appears, including during stability checks.
        if(step.Kind!="hold" && step.Until!.Matches(state))
        {
            if(++stable>=step.StableFrames)finishStep=true;
            if(++elapsed>step.TimeoutFrames)Fail("Timed out stabilizing "+step.Label);
            return;
        }
        stable=0;
        if(state.FieldHeal && state.Phase is "Player" or "Menu")
        {
            if(state.MenuInput!=null)current.Add(state.MenuInput);
            return;
        }
        if(!step.Phases.Contains(state.Phase) || (step.MapId.HasValue && step.MapId!=state.MapId && (step.Kind!="navigate" || state.Phase is "Player" or "Transport")))
        { Fail($"Unexpected state at {step.Label}: {state.Phase}, map {state.MapId}");return; }
        if(++elapsed>step.TimeoutFrames) { Fail("Timed out at "+step.Label);return; }
        if(step.Kind=="checkpoint") { Fail("Checkpoint mismatch: "+step.Label);return; }
        if(step.HandleBattles && state.Phase=="Battle")
        {
            // "A+B" holds several keys at once (escape needs both shoulders).
            if(state.BattleInput!=null)foreach(var key in state.BattleInput.Split('+'))current.Add(key);
            return;
        }
        // FieldHelp is the native controls/pause window. The opening audience ends
        // with ShowHelpAndExit, which opens it with FieldHelpManager.IsOpenHelp set;
        // UpdateDisplayHelp then ignores Back and Confirm and only closes on Start
        // (onCancelAction). Back still matters when the pause menu is shown without
        // that flag, so alternate Start and Back pulses.
        if(state.Phase=="FieldHelp" && step.Kind is "mash" or "navigate" or "interact" or "battle")
        {
            if(elapsed%16==0)current.Add("Start");
            else if(elapsed%16==8)current.Add("Back");
            return;
        }
        // Popups block field movement without changing the game phase: confirm
        // them away before any step-specific input. Choice windows are handled below.
        if(step.Kind=="menu" && state.Phase is "Player" or "Menu" or "Shop" or "Message")
        {
            if(state.MenuInput!=null)current.Add(state.MenuInput);
            return;
        }
        if(state.Modal && state.ChoiceIndex==null && state.ItemListIndex==null && state.Phase!="Battle")
        {
            if(elapsed%8==0)current.Add("Confirm");
            return;
        }
        // Event item list: walk the cursor to the wanted item (two columns:
        // Left/Right within a row, Up/Down across rows), then confirm.
        if(step.ItemChoice!=null && state.ItemListIndex is int at && state.ItemListWanted>=0)
        {
            int want=state.ItemListWanted;
            string key=at==want?"Confirm":at/2!=want/2?(want<at?"Up":"Down"):(want<at?"Left":"Right");
            if(elapsed%8==0)current.Add(key);
            return;
        }
        // Steer an open choice window to the requested option, then confirm it.
        // Without a requested option, dialogue keeps its default (Confirm).
        if(step.Choice!=null && state.ChoiceIndex!=null && state.Phase is "Event" or "Message")
        {
            if(elapsed%8==0)current.Add(state.ChoiceIndex<step.Choice?"Down":state.ChoiceIndex>step.Choice?"Up":"Confirm");
            return;
        }
        if(step.Kind is "navigate" or "interact" or "battle")
        {
            if(state.Phase is "Player" or "Transport")
            {
                if(step.Kind=="navigate")
                {
                    if(state.NavigationInput!=null)current.Add(state.NavigationInput);
                    else if(state.AtDestination && elapsed%12==0)current.Add("Confirm");
                }
                else if(step.Kind=="interact")
                {
                    // Face once, release, then pulse Confirm. Never keep walking
                    // into an NPC/door while a dialog opens.
                    if(elapsed<=2 && step.Facing!=null)current.Add(step.Facing);
                    else if(elapsed%12==0)current.Add("Confirm");
                }
            }
            else if(state.Phase is "Event" or "Message")
            { if(elapsed%8==0)current.Add("Confirm"); }
            return;
        }
        if(step.Kind=="hold")
        {
            current.UnionWith(step.Keys);
            if(elapsed>=step.Frames)finishStep=true;
        }
        else if(step.Kind=="mash" && (elapsed-1)%step.PeriodFrames<step.PressedFrames)current.UnionWith(step.Keys);
    }
}
