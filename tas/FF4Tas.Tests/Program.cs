using FF4Tas.Core;
int checks=0;
void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
Step Hold()=>new(){Label="walk",Kind="hold",Keys=new[]{"Right"},Phases=new[]{"Player"},MapId=10,Frames=2};
RouteEngine Engine(params Step[] steps)=>new(new Route{Name="test",Steps=steps});
var player=new Snapshot{Phase="Player",MapId=10};
var e=Engine(Hold());e.Tick(1,player);Check(e.Down("Right"),"first frame presses");e.Tick(1,player);Check(e.Down("Right"),"same-frame reads stable");e.Tick(2,player);Check(e.Held("Right")&&!e.Down("Right"),"second frame held");e.Tick(3,player);Check(e.Status=="complete"&&!e.Held("Right")&&e.Up("Right"),"completion releases");
e=Engine(Hold());e.Tick(1,new Snapshot{Phase="Battle",MapId=10});Check(e.Status=="failed"&&!e.Held("Right"),"unexpected encounter stops movement");
e=Engine(Hold());e.Tick(1,new Snapshot{Phase="Player",MapId=11});Check(e.Status=="failed","wrong map stops");
e=Engine(Hold());e.Tick(1,player);e.Tick(2,new Snapshot{Phase="Player",MapId=10,Focused=false});Check(!e.Running&&!e.Held("Right"),"focus loss releases");
e=Engine(new Step{Label="dialogue",Kind="mash",Keys=new[]{"Confirm"},Phases=new[]{"Message"},Until=new(){Phase="Player"},StableFrames=2,PeriodFrames=3});
var message=new Snapshot{Phase="Message",MapId=10};e.Tick(1,message);Check(e.Down("Confirm"),"mash press");e.Tick(2,message);Check(e.Up("Confirm"),"mash release");e.Tick(3,message);Check(!e.Held("Confirm"),"mash release interval");e.Tick(4,message);Check(e.Down("Confirm"),"next pulse");e.Tick(5,player);Check(!e.Held("Confirm")&&e.Running,"destination disables input immediately");e.Tick(6,player);e.Tick(7,player);Check(e.Status=="complete","stable destination finishes");
e=Engine(new Step{Label="wait",Kind="wait",Phases=new[]{"Message"},Until=new(){Phase="Player"},TimeoutFrames=2});e.Tick(1,message);e.Tick(2,message);e.Tick(3,message);Check(e.Status=="failed","timeout");
e=Engine(new Step{Label="check",Kind="checkpoint",Phases=new[]{"Player"},Until=new(){MapId=11}});e.Tick(1,player);Check(e.Status=="failed","checkpoint cannot silently pass");
e=Engine(Hold());e.Tick(20,player);e.Tick(19,player);Check(e.Status=="failed","reversed frames");
e=Engine(Hold());e.Tick(1,player);e.Stop("test");Check(!e.Held("Right")&&!e.Down("Right"),"manual stop releases");
try {var bad=Hold();bad.Keys=new[]{"Typo"};Engine(bad);throw new Exception("accepted invalid key");}catch(ArgumentException){checks++;}
e=Engine(new Step{Label="visual wait",Kind="wait",Phases=new[]{"Player"},Until=new(){Visual="boss-menu"},TimeoutFrames=2});
e.Tick(1,player);Check(e.Running,"unmatched visual waits");e.Tick(2,new Snapshot{Phase="Player",VisualMatches=new[]{"boss-menu"}});e.Tick(3,player);Check(e.Status=="complete","local visual match advances");
Console.WriteLine($"PASS: {checks} scripted-route regression checks");
