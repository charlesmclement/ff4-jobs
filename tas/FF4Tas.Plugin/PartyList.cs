using Last.UI.KeyInput;

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
    private static bool Cecil(int id)=>id is 1 or 13;
}
