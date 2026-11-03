using FF4Tas.Core;
int checks=0;
void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
Step Hold()=>new(){Label="walk",Kind="hold",Keys=new[]{"Right"},Phases=new[]{"Player"},MapId=10,Frames=2};
RouteEngine Engine(params Step[] steps)=>new(new Route{Name="test",Steps=steps});
var player=new Snapshot{Phase="Player",MapId=10};
var e=Engine(Hold());e.Tick(1,player);Check(e.Down("Right"),"first frame presses");e.Tick(1,player);Check(e.Down("Right"),"same-frame reads stable");e.Tick(2,player);Check(e.Held("Right")&&!e.Down("Right"),"second frame held");e.Tick(3,player);Check(e.Status=="complete"&&!e.Held("Right")&&e.Up("Right"),"completion releases");
e=Engine(Hold());e.Tick(1,new Snapshot{Phase="Battle",MapId=10});Check(e.Status=="failed"&&!e.Held("Right"),"unexpected encounter stops movement");
e=Engine(Hold());e.Tick(1,new Snapshot{Phase="Player",MapId=11});Check(e.Status=="failed","wrong map stops");
e=Engine(Hold());e.Tick(1,player);e.Tick(2,new Snapshot{Phase="Player",MapId=10,Focused=false});Check(e.Running&&e.Paused&&!e.Held("Right"),"focus loss pauses and releases");
e=Engine(new Step{Label="dialogue",Kind="mash",Keys=new[]{"Confirm"},Phases=new[]{"Message"},Until=new(){Phase="Player"},StableFrames=2,PeriodFrames=3});
var message=new Snapshot{Phase="Message",MapId=10};e.Tick(1,message);Check(e.Down("Confirm"),"mash press");e.Tick(2,message);Check(e.Up("Confirm"),"mash release");e.Tick(3,message);Check(!e.Held("Confirm"),"mash release interval");e.Tick(4,message);Check(e.Down("Confirm"),"next pulse");e.Tick(5,player);Check(!e.Held("Confirm")&&e.Running,"destination disables input immediately");e.Tick(6,player);e.Tick(7,player);Check(e.Status=="complete","stable destination finishes");
e=Engine(new Step{Label="wait",Kind="wait",Phases=new[]{"Message"},Until=new(){Phase="Player"},TimeoutFrames=2});e.Tick(1,message);e.Tick(2,message);e.Tick(3,message);Check(e.Status=="failed","timeout");
e=Engine(new Step{Label="check",Kind="checkpoint",Phases=new[]{"Player"},Until=new(){MapId=11}});e.Tick(1,player);Check(e.Status=="failed","checkpoint cannot silently pass");
e=Engine(Hold());e.Tick(20,player);e.Tick(19,player);Check(e.Status=="failed","reversed frames");
e=Engine(Hold());e.Tick(1,player);e.Stop("test");Check(!e.Held("Right")&&!e.Down("Right"),"manual stop releases");
try {var bad=Hold();bad.Keys=new[]{"Typo"};Engine(bad);throw new Exception("accepted invalid key");}catch(ArgumentException){checks++;}
e=Engine(new Step{Label="visual wait",Kind="wait",Phases=new[]{"Player"},Until=new(){Visual="boss-menu"},TimeoutFrames=2});
e.Tick(1,player);Check(e.Running,"unmatched visual waits");e.Tick(2,new Snapshot{Phase="Player",VisualMatches=new[]{"boss-menu"}});e.Tick(3,player);Check(e.Status=="complete","local visual match advances");
// Every shipped route parses and validates (the plugin's own JSON options).
var jsonOptions=new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true};
foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"../../../../routes"),"*.json"))
{var r=System.Text.Json.JsonSerializer.Deserialize<Route>(File.ReadAllText(file),jsonOptions)!;r.Validate();checks++;}
var steering=System.Text.Json.JsonSerializer.Deserialize<Route>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../routes/mist-octomammoth.json")),jsonOptions)!;
Check(steering.Rng?.CureBurns==2 && steering.Rng.AvoidMonsters.Contains(9),"segment 2 RNG plan loads");
// EXP plan rests: grind until levels or low MP; later rest blocks skip as a group once the levels are in.
var lv=new Dictionary<int,int>{[1]=16};
var grindUntil=new Condition{Phase="Player",Any=new[]{new Condition{LevelAtLeast=lv},new Condition{MpBelow=new(){[3]=9}}}};
Check(!grindUntil.Matches(new Snapshot{Phase="Player",Levels=new(){[1]=15,[3]=10},Mp=new(){[3]=20}}),"grind continues");
Check(grindUntil.Matches(new Snapshot{Phase="Player",Levels=new(){[1]=15,[3]=10},Mp=new(){[3]=8}}),"low MP ends the grind");
Check(grindUntil.Matches(new Snapshot{Phase="Player",Levels=new(){[1]=16},Mp=new(){[3]=30}}),"levels end the grind");
Check(!grindUntil.Matches(new Snapshot{Phase="Menu",Levels=new(){[1]=16}}),"not mid-menu");
Check(!new Condition{LevelAtLeast=lv}.Matches(new Snapshot{Phase="Player"}),"unread levels never count as reached");
Step W(string label,string group="",Condition skip=null!)=>new Step{Label=label,Kind="wait",Phases=new[]{"Player"},Until=new(){Phase="Message"},Group=group==""?null:group,SkipIf=skip};
var rest=new Route{Name="rest",Steps=new[]{W("a"),W("b","r2",new Condition{LevelAtLeast=lv}),W("c","r2"),W("d")}};
var re=new RouteEngine(rest);var msg=new Snapshot{Phase="Message",Levels=new(){[1]=16}};
re.Tick(1,msg);re.Tick(2,msg);Check(re.Index==3,"rest group skipped as a whole");
re=new RouteEngine(rest);var low=new Snapshot{Phase="Message",Levels=new(){[1]=15}};re.Tick(1,low);re.Tick(2,low);Check(re.Index==1,"rest group runs below the levels");
Console.WriteLine($"PASS: {checks} scripted-route regression checks");
