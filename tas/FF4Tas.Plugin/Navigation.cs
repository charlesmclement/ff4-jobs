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
    private int cursor,unchanged,retryAt;
    private Vector3 lastPosition;
    private bool observed;
    internal void Reset() {step=null;owner=IntPtr.Zero;path.Clear();cursor=unchanged=retryAt=0;observed=false;}
    internal void Read(FieldMap field,Step active,Snapshot state,int frame)
    {
        var controller=field.fieldController;
        var player=controller?.GetPlayerEntity();
        if(controller==null || player==null)return;
        var accessor=controller.Cast<IMapAccessor>();
        var cell=MapRouteSearcher.EntityWorldPositionToCellPosition(accessor,player);
        state.X=cell.x;state.Y=cell.y;state.Layer=(int)cell.z;
        if(active.Kind!="navigate" || active.Goal==null || active.MapId!=state.MapId)return;
        var goal=active.Goal;
        if(!ReferenceEquals(step,active) || owner!=player.Pointer)
        {Reset();step=active;owner=player.Pointer;}
        var pos=player.transform.position;
        var destination=controller.ConvertCellPositionToWorldPosition(new Vector3(goal.X,goal.Y,goal.Layer));
        var neighbor=controller.ConvertCellPositionToWorldPosition(new Vector3(goal.X+1,goal.Y,goal.Layer));
        float tile=Math.Max(.01f,Math.Abs(neighbor.x-destination.x)),tolerance=tile*.16f;
        state.AtDestination=Near(pos,destination,tolerance) && (int)cell.z==goal.Layer;
        if(state.AtDestination) {state.Detail="At route destination";return;}
        if(observed && Near(lastPosition,pos,tile*.01f))unchanged++;
        else {lastPosition=pos;unchanged=0;observed=true;}
        if(unchanged>600) {state.Failure=$"Movement stalled at map {state.MapId} ({cell.x},{cell.y}); target ({goal.X},{goal.Y})";return;}
        if(unchanged>0 && unchanged%90==0)path.Clear();
        if(path.Count==0 && frame>=retryAt)
        {
            retryAt=frame+30;
            var route=MapRouteSearcher.Search(accessor,cell,new Vector3(goal.X,goal.Y,goal.Layer),true);
            if(route!=null)
                for(int i=0;i<route.Count;i++)path.Add(route[i]);
            cursor=0;
            // Native search returns world positions. Accept either ordering.
            if(path.Count>1 && Distance(pos,path[^1])<Distance(pos,path[0]))path.Reverse();
        }
        while(cursor<path.Count && Near(pos,path[cursor],tolerance))cursor++;
        if(cursor>=path.Count)
        {
            path.Clear();state.Detail="Waiting for a collision-safe path";return;
        }
        var next=path[cursor];float dx=next.x-pos.x,dy=next.y-pos.y;
        // Straight native path segments stay aligned; ordinary movement handles stairs/layers.
        state.NavigationInput=Math.Abs(dx)>Math.Abs(dy)?(dx>0?"Right":"Left"):(dy>0?"Up":"Down");
        state.Detail=$"Walk to ({goal.X},{goal.Y},{goal.Layer}); path {cursor+1}/{path.Count}";
    }
    private static bool Near(Vector3 a,Vector3 b,float epsilon)=>Math.Abs(a.x-b.x)<=epsilon && Math.Abs(a.y-b.y)<=epsilon;
    private static float Distance(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y);
}
