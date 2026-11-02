namespace FF4Tas.Core;

public sealed class Route
{
    public int Schema { get; set; } = 1;
    public string Name { get; set; } = "";
    public Step[] Steps { get; set; } = Array.Empty<Step>();
    public void Validate()
    {
        if(Schema!=1 || string.IsNullOrWhiteSpace(Name) || Steps==null || Steps.Length is <1 or >10000)
            throw new ArgumentException("Invalid route header");
        foreach(var s in Steps)
        {
            if(s==null || string.IsNullOrWhiteSpace(s.Label) || s.Phases==null || s.Keys==null
                || s.Phases.Length==0 || s.Phases.Any(p=>!KnownPhases.Contains(p)))
                throw new ArgumentException("Each step needs a label and allowed game phases");
            if(s.Kind is not ("hold" or "wait" or "mash" or "checkpoint"))throw new ArgumentException("Unknown step kind");
            if(s.TimeoutFrames is <1 or >216000 || s.StableFrames is <1 or >600)
                throw new ArgumentException("Invalid step timeout or stability interval");
            if(s.Keys.Any(k=>!KnownKeys.Contains(k)) || s.Keys.Distinct().Count()!=s.Keys.Length)
                throw new ArgumentException("Unknown/duplicate logical input");
            if((s.Keys.Contains("Up") && s.Keys.Contains("Down")) || (s.Keys.Contains("Left") && s.Keys.Contains("Right")))
                throw new ArgumentException("Opposing directions");
            if(s.Kind=="hold" && (s.Frames<1 || s.Frames>s.TimeoutFrames || s.Keys.Length==0))throw new ArgumentException("Invalid hold");
            if(s.Kind=="mash" && (s.Keys.Length==0 || s.PeriodFrames<2 || s.PressedFrames<1 || s.PressedFrames>=s.PeriodFrames))
                throw new ArgumentException("Mash requires a release interval");
            if(s.Kind is "wait" or "mash" or "checkpoint")
                if(s.Until==null || (s.Until.Phase==null && s.Until.MapId==null && string.IsNullOrWhiteSpace(s.Until.Visual)))throw new ArgumentException("Step needs a completion condition");
            if(s.Until?.Phase!=null && !KnownPhases.Contains(s.Until.Phase))throw new ArgumentException("Unknown completion phase");
            if(s.Kind is "wait" or "checkpoint" && s.Keys.Length!=0)throw new ArgumentException("Wait/checkpoint cannot press keys");
        }
    }
    public static readonly string[] KnownKeys={"Confirm","Back","Menu","Shortcut","Up","Down","Left","Right","SwitchLeft","SwitchRight","PageUp","PageDown","Start","Select"};
    public static readonly string[] KnownPhases={"None","Init","ChangeMap","FieldReady","Player","Transport","Menu","GpsMenu","ExpandMinimap","Message","Shop","ChangeName","Gimmick","Event","Battle","GotoTitle","MiniGame","FieldHelp","MenuLibraryUi","MenuLibraryInfo"};
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
}
public sealed class Condition
{
    public string? Phase { get; set; }
    public int? MapId { get; set; }
    public string? Visual { get; set; }
    public bool Matches(Snapshot state)=>(Phase==null || state.Phase==Phase) && (MapId==null || state.MapId==MapId)
        && (Visual==null || state.VisualMatches.Contains(Visual));
}
public sealed class Snapshot
{
    public string Phase { get; set; } = "None";
    public int MapId { get; set; }
    public bool Focused { get; set; } = true;
    public string[] VisualMatches { get; set; } = Array.Empty<string>();
}

// Frames are observed Unity updates, not emulated/savestated simulation frames.
// All reads within a frame see the same input, regardless of polling order.
public sealed class RouteEngine
{
    private readonly Route route;
    private readonly HashSet<string> current=new(), previous=new();
    private long lastFrame=-1;
    private int elapsed,stable;
    private bool finishStep;
    public RouteEngine(Route route) { route.Validate();this.route=route; }
    public int Index { get; private set; }
    public string Status { get; private set; } = "running";
    public string Reason { get; private set; } = "";
    public string Label=>Index<route.Steps.Length?route.Steps[Index].Label:"Finished";
    public bool Running=>Status=="running";
    public bool Held(string key)=>current.Contains(key);
    public bool Down(string key)=>current.Contains(key) && !previous.Contains(key);
    public bool Up(string key)=>!current.Contains(key) && previous.Contains(key);
    public void Stop(string reason) { Status="stopped";Reason=reason;current.Clear();previous.Clear(); }
    private void Fail(string reason) { Stop(reason);Status="failed"; }
    public void Tick(long frame,Snapshot state)
    {
        if(!Running || frame==lastFrame)return;
        if(frame<lastFrame) { Fail("Frame counter moved backwards");return; }
        lastFrame=frame;
        previous.Clear();previous.UnionWith(current);current.Clear();
        if(!state.Focused) { Stop("Game lost focus");return; }
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
        if(!step.Phases.Contains(state.Phase) || (step.MapId.HasValue && step.MapId!=state.MapId))
        { Fail($"Unexpected state at {step.Label}: {state.Phase}, map {state.MapId}");return; }
        if(++elapsed>step.TimeoutFrames) { Fail("Timed out at "+step.Label);return; }
        if(step.Kind=="checkpoint") { Fail("Checkpoint mismatch: "+step.Label);return; }
        if(step.Kind=="hold")
        {
            current.UnionWith(step.Keys);
            if(elapsed>=step.Frames)finishStep=true;
        }
        else if(step.Kind=="mash" && (elapsed-1)%step.PeriodFrames<step.PressedFrames)current.UnionWith(step.Keys);
    }
}
