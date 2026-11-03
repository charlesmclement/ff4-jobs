using FF4Tas.Core;

namespace FF4Tas;

// Out-of-battle healing: spells first (FieldMagic: Life, Cure), then items
// (FieldMenu: Phoenix Down, Potion) for whatever magic couldn't cover.
internal sealed class FieldCare
{
    private readonly FieldMagic magic=new();
    private readonly FieldMenu items=new();
    private bool magicDone;
    private float topUp=.7f;
    internal void Reset(float topUpBelow=.7f,float? magicTo=null)
    {
        topUp=topUpBelow;magicDone=false;
        if(magicTo is float full)magic.Reset(full,allowTellah:false);else magic.Reset(topUp);
        items.Reset(topUp);
    }
    internal bool Wanted()=>magic.Wanted() || items.Wanted();
    // After each fight: every member's condition ids, to status.log beside
    // the session files (field status cures missed a Mini live; this shows
    // what the field sees).
    internal static void LogParty(string dir,int frame)
    {
        try
        {
            var user=Last.Management.UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
            if(corps==null || owned==null)return;
            var sb=new System.Text.StringBuilder().Append(frame).Append(':');
            for(int c=0;c<corps.Count;c++)
                for(int o=0;o<owned.Count;o++)
                    if(owned[o]!=null && owned[o].Id==(corps[c]?.CharacterId??0))sb.Append(' ').Append(FieldMagic.Describe(owned[o]));
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir,"status.log"),sb.Append('\n').ToString());
        }
        catch(System.Exception) {}
    }
    internal void Read(Snapshot s,int frame)
    {
        if(!magicDone)
        {
            magic.Read(s,frame);
            if(!s.MenuDone)return;
            magicDone=true;s.MenuDone=false;items.Reset(topUp);
        }
        items.Read(s,frame);
    }
}
