using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// The menu's party list, read by character (user: "none of the programming
// should be based on what slot the character is in"): each row of
// StatusWindowController.contentList carries the OwnedCharacterData it shows.
internal static class PartyList
{
    // character_status id shown on a row, 0 for an empty row, -1 if unreadable.
    internal static int IdAt(StatusWindowController? status,int row)
    {
        var rows=status?.contentList;
        if(rows==null || row<0 || row>=rows.Count)return -1;
        return rows[row]?.CharacterData?.characterStatusId??0;
    }
    // Row showing this character (Paladin and Dark Knight Cecil count as one
    // when `cecilAsOne`), or -1.
    internal static int RowOf(StatusWindowController? status,int statusId,bool cecilAsOne=false)
    {
        var rows=status?.contentList;
        for(int i=0;rows!=null && i<rows.Count;i++)
        {
            int id=rows[i]?.CharacterData?.characterStatusId??0;
            if(id==statusId || cecilAsOne && Cecil(id) && Cecil(statusId))return i;
        }
        return -1;
    }
    internal static int Rows(StatusWindowController? status)=>status?.contentList?.Count??0;
    // The character the party-list cursor is on, read from where the cursor
    // sits on screen (the row whose cursor anchor is nearest), not from its
    // index: live, the row RowOf found for Cecil opened Cid's magic, so the
    // list order and the cursor order differ. 0 when unreadable.
    internal static int UnderCursor(StatusWindowController? status)
    {
        var rows=status?.contentList;var cursor=status?.selectCursor;
        if(rows==null || cursor==null || !cursor)return 0;
        Vector3 at=cursor.transform.position;float best=float.MaxValue;int id=0;
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];int who=row?.CharacterData?.characterStatusId??0;if(who==0)continue;
            GameObject? anchor=null;
            try {anchor=row!.FocusCursorParent()??row.DefaultCursorParent();} catch {anchor=null;}
            if(anchor==null)continue;
            float d=(anchor.transform.position-at).sqrMagnitude;
            if(d<best) {best=d;id=who;}
        }
        return id;
    }
    private static bool Cecil(int id)=>id is 1 or 13;
}
