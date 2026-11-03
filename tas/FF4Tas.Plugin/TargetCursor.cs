using Last.UI.KeyInput;
using UnityEngine;
using Contents=Il2CppSystem.Collections.Generic.List<Last.UI.KeyInput.ItemTargetSelectContentController>;

namespace FF4Tas;

// Which party member a field target cursor is on, read from the screen instead
// of counted from a list index. The target lists keep an empty entry where a
// member left (Tellah) and the cursor skips it, so an index count is off for
// some party setups (live: a Potion for Edward looped 30 min after the
// Antlion). The game's own answer comes first (the item screen's GetTargets,
// the spell screen's currentContent); else the cursor's position, since
// Cursor.SetSource pins it to the chosen member's CursorParent.
internal static class TargetCursor
{
    // Owned-character id under the cursor, or 0 when it can't be read.
    internal static int Under(ItemTargetSelectContentController? chosen,Last.UI.Cursor? cursor,Contents? contents)
    {
        int id=Id(chosen);
        return id!=0?id:Nearest(cursor,contents);
    }
    internal static int Under(ItemUseController use)
    {
        int id=0;
        try
        {
            var targets=use.GetTargets();
            var list=targets?.TryCast<Contents>();
            if(list!=null) {if(list.Count==1)id=Id(list[0]);}
            else if(targets!=null)
            {
                var e=targets.GetEnumerator();var walk=e?.TryCast<Il2CppSystem.Collections.IEnumerator>();
                int n=0;
                while(walk!=null && walk.MoveNext()) {n++;id=Id(e!.Current);}
                if(n!=1)id=0;
            }
        }
        catch {id=0;}
        return id!=0?id:Nearest(use.selectCursor,use.contentList);
    }
    private static int Id(ItemTargetSelectContentController? c)=>c!=null && c && c.gameObject.activeInHierarchy?c.CurrentData?.Id??0:0;
    private static int Nearest(Last.UI.Cursor? cursor,Contents? contents)
    {
        if(cursor==null || !cursor || contents==null)return 0;
        Vector3 at=cursor.transform.position;float best=float.MaxValue;int id=0;
        for(int i=0;i<contents.Count;i++)
        {
            int who=Id(contents[i]);var parent=who!=0?contents[i].CursorParent:null;
            if(parent==null)continue;
            float d=(parent.transform.position-at).sqrMagnitude;
            if(d<best) {best=d;id=who;}
        }
        return id;
    }
}
