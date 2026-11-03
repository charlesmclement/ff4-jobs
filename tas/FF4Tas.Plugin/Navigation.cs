using FF4Tas.Core;
using Last.Map;
using UnityEngine;

namespace FF4Tas;

// Query the game's collision-aware search; execution is ordinary directional input.
// No MoveTo, transform writes, map warps, collision changes, or event invocation.
internal sealed class Navigation
{
    private Step? step;
    private IntPtr owner;
    private readonly List<Vector3> path=new();
    private int cursor,unchanged,retryAt,greedyTurn;
    private Vector3 lastPosition,target,previousFrame;
    private bool havePrevious;
    private bool observed,greedy;
    // Waypoint progress survives Reset (battles, events) and restarts per step.
    private Step? waypointStep;
    private int waypoint;
    // After any scene/battle (Reset) the player may have been moved: resync to
    // the nearest remaining waypoint before pushing toward the old one.
    private bool resync;
    // Path cells still to walk on this step (0 = no path yet).
    internal int Remaining=>Math.Max(0,path.Count-cursor);
    internal void Reset() {resync=true;step=null;owner=IntPtr.Zero;path.Clear();cursor=unchanged=retryAt=greedyTurn=0;observed=greedy=havePrevious=false;}
    internal void Read(FieldMap field,Step active,Snapshot state,int frame)
    {
        var controller=field.fieldController;
        var player=controller?.GetPlayerEntity();
        if(controller==null || player==null)return;
        var accessor=controller.Cast<IMapAccessor>();
        var cell=MapRouteSearcher.EntityWorldPositionToCellPosition(accessor,player);
        state.X=cell.x;state.Y=cell.y;state.Layer=(int)cell.z;
        // Vehicles: the navigator steers them the same way; pressing Confirm at
        // a landing tile (navigate's arrival pulse) gets off.
        var fieldPlayer=player.TryCast<Last.Entity.Field.FieldPlayer>();
        state.Riding=fieldPlayer!=null && (int)fieldPlayer.moveState is not (0 or 1);
        EncounterWatch.PlayerPos=player.transform.position;
        if(active.Kind!="navigate" || active.Goal==null || active.MapId!=state.MapId)return;
        var goal=active.Goal;
        if(!ReferenceEquals(step,active) || owner!=player.Pointer)
        {Reset();step=active;owner=player.Pointer;}
        var pos=player.transform.position;
        var destination=controller.ConvertCellPositionToWorldPosition(new Vector3(goal.X,goal.Y,goal.Layer));
        var neighbor=controller.ConvertCellPositionToWorldPosition(new Vector3(goal.X+1,goal.Y,goal.Layer));
        float tile=Math.Max(.01f,Math.Abs(neighbor.x-destination.x)),tolerance=tile*.16f;
        // Speed boost (or a low frame rate) moves farther per frame; widen the
        // arrival window to the observed stride so points are never stepped over.
        if(havePrevious)tolerance=Math.Min(tile*.45f,Math.Max(tolerance,Distance(pos,previousFrame)*.6f));
        previousFrame=pos;havePrevious=true;
        // Positions are compared in world x/y; layer mismatches are reported but
        // a goal cell reached on another layer still counts.
        state.AtDestination=Near(pos,destination,tolerance);
        // Loop (grinding): at the goal, start over at the first waypoint.
        if(state.AtDestination && active.Loop && active.Waypoints is {Length:>0}) {state.AtDestination=false;waypointStep=active;waypoint=0;resync=false;}
        if(state.AtDestination) {state.Detail="At route destination";return;}
        if(observed && Near(lastPosition,pos,tile*.01f))unchanged++;
        else {lastPosition=pos;unchanged=0;observed=true;}
        if(unchanged>600) {state.Failure=$"Movement stalled at map {state.MapId} ({cell.x},{cell.y}); target ({goal.X},{goal.Y})";return;}
        if(unchanged>0 && unchanged%90==0)path.Clear();
        var goalCell=new Vector3(goal.X,goal.Y,goal.Layer);
        if(!ReferenceEquals(waypointStep,active)) {waypointStep=active;waypoint=0;}
        var waypoints=active.Waypoints;
        if(resync && waypoints!=null && waypoint<waypoints.Length)
        {
            int best=waypoint;float bestDistance=float.MaxValue;
            for(int i=waypoint;i<waypoints.Length;i++)
            {
                var c=controller.ConvertCellPositionToWorldPosition(new Vector3(waypoints[i].X,waypoints[i].Y,waypoints[i].Layer));
                float d=Distance(pos,c);if(d<bestDistance) {bestDistance=d;best=i;}
            }
            waypoint=best;
        }
        resync=false;
        // A waypoint the player can't move toward (stalled ~0.75 s) is skipped.
        if(waypoints!=null && waypoint<waypoints.Length && unchanged==45)waypoint++;
        while(waypoints!=null && waypoint<waypoints.Length)
        {
            var w=waypoints[waypoint];
            var world=controller.ConvertCellPositionToWorldPosition(new Vector3(w.X,w.Y,w.Layer));
            if(Near(pos,world,tolerance)) {waypoint++;continue;}
            float wx=world.x-pos.x,wy=world.y-pos.y;
            state.NavigationInput=Math.Abs(wx)>Math.Abs(wy)?(wx>0?"Right":"Left"):(wy>0?"Up":"Down");
            state.Detail=$"Walk to ({goal.X},{goal.Y},{goal.Layer}); waypoint {waypoint+1}/{waypoints.Length} ({w.X},{w.Y})";
            return;
        }
        if(path.Count==0 && frame>=retryAt)
        {
            retryAt=frame+30;greedy=false;
            foreach(var candidate in Candidates(cell,goalCell,cell.z))
            {
                var route=MapRouteSearcher.Search(accessor,cell,candidate,true);
                if(route==null || route.Count==0)continue;
                for(int i=0;i<route.Count;i++)path.Add(route[i]);
                target=candidate;break;
            }
            cursor=0;
            // Native search returns world positions. Accept either ordering.
            if(path.Count>1 && Distance(pos,path[^1])<Distance(pos,path[0]))path.Reverse();
            // No native route to the goal or any closer cell: steer directly and
            // let ordinary collision stop us; stalls rotate the axis below.
            if(path.Count==0)greedy=true;
        }
        if(greedy)
        {
            if(unchanged>0 && unchanged%24==0)greedyTurn++;
            state.NavigationInput=Toward(cell,goalCell,greedyTurn);
            state.Detail=$"Walk to ({goal.X},{goal.Y},{goal.Layer}); no native route, steering {state.NavigationInput}";
            return;
        }
        while(cursor<path.Count && Near(pos,path[cursor],tolerance))cursor++;
        if(cursor>=path.Count)
        {
            path.Clear();
            // A partial leg ended. Next to the goal (doors, cave mouths and other
            // trigger tiles the search treats as blocked), step into it; otherwise
            // plan the next leg immediately.
            if(Math.Abs(cell.x-goal.X)+Math.Abs(cell.y-goal.Y)==1)
            {
                state.NavigationInput=Toward(cell,goalCell,0);
                state.Detail=$"Step into ({goal.X},{goal.Y},{goal.Layer})";
                return;
            }
            retryAt=frame;state.Detail="Planning next leg";return;
        }
        // Every push must move him. The native search ignores wandering NPCs,
        // so when a step doesn't move him, let go briefly for the way to
        // clear, then push again; every 90 still frames the path is replanned.
        if(unchanged%90 is >=12 and <36) {state.Detail=$"Blocked at ({cell.x},{cell.y}); waiting for the way to clear";return;}
        var next=path[cursor];float dx=next.x-pos.x,dy=next.y-pos.y;
        // Straight native path segments stay aligned; ordinary movement handles stairs/layers.
        state.NavigationInput=Math.Abs(dx)>Math.Abs(dy)?(dx>0?"Right":"Left"):(dy>0?"Up":"Down");
        bool partial=(int)target.x!=goal.X || (int)target.y!=goal.Y;
        state.Detail=$"Walk to ({goal.X},{goal.Y},{goal.Layer}){(partial?$" via ({target.x},{target.y})":"")}; path {cursor+1}/{path.Count}";
    }
    // The native search fails when the goal is a trigger tile it marks as blocked
    // or lies beyond its 64x64 window. Try the goal, its neighbours, then cells
    // progressively closer to us along the straight line, with small offsets.
    private static IEnumerable<Vector3> Candidates(Vector3 from,Vector3 goal,float playerLayer)
    {
        yield return goal;
        // A route written with the wrong collision layer (Bottom 0, Ground 1,
        // Upper 2) never finds a path; retry the same cell on the player's layer.
        if((int)playerLayer!=(int)goal.z)yield return new Vector3(goal.x,goal.y,playerLayer);
        foreach(var (ox,oy) in Offsets(1))yield return new Vector3(goal.x+ox,goal.y+oy,goal.z);
        foreach(var f in new[]{.75f,.5f,.3f,.15f})
        {
            float cx=Mathf.Round(from.x+(goal.x-from.x)*f),cy=Mathf.Round(from.y+(goal.y-from.y)*f);
            if(Math.Abs(cx-from.x)+Math.Abs(cy-from.y)<2)continue;
            yield return new Vector3(cx,cy,goal.z);
            foreach(var (ox,oy) in Offsets(2))yield return new Vector3(cx+ox,cy+oy,goal.z);
        }
    }
    private static IEnumerable<(int,int)> Offsets(int d)=>new[]{(d,0),(-d,0),(0,d),(0,-d)};
    // Directional input toward a cell. turn rotates through the main axis, the
    // other axis, then their opposites so a blocked direct line can slide around.
    private static string Toward(Vector3 from,Vector3 to,int turn)
    {
        float dx=to.x-from.x,dy=to.y-from.y;
        string h=dx>=0?"Right":"Left",v=dy>=0?"Down":"Up";
        bool horizontalFirst=Math.Abs(dx)>=Math.Abs(dy);
        var order=horizontalFirst?new[]{h,v,Opposite(v),Opposite(h)}:new[]{v,h,Opposite(h),Opposite(v)};
        return order[turn%order.Length];
    }
    private static string Opposite(string d)=>d switch{"Right"=>"Left","Left"=>"Right","Up"=>"Down",_=>"Up"};
    private static bool Near(Vector3 a,Vector3 b,float epsilon)=>Math.Abs(a.x-b.x)<=epsilon && Math.Abs(a.y-b.y)<=epsilon;
    private static float Distance(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y);
}
