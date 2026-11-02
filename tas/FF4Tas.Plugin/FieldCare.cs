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
    internal void Reset(float topUpBelow=.7f) {topUp=topUpBelow;magicDone=false;magic.Reset(topUp);items.Reset(topUp);}
    internal bool Wanted()=>magic.Wanted() || items.Wanted();
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
