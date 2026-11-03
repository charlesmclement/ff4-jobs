namespace FF4Tas;

// Mirror of UnityEngine.Random (Xorshift128; InitState: s0=seed,
// s(n+1)=s(n)*1812433253+1), kept in step by EncounterWatch's hooks on every
// InitState and draw. Verified live: 2681 of 2685 traced RandomRangeInt
// results matched (docs/TAS-ENCOUNTERS.md). Read-only: it never touches the
// game's generator, it only predicts it. Synced turns false when a traced
// int draw disagrees (an untraced consumer) until the next InitState.
internal static class Rng
{
    private static uint s0,s1,s2,s3;
    internal static bool Synced {get;private set;}
    internal static int Draws {get;private set;}
    internal static void Seed(int seed)
    {
        s0=(uint)seed;s1=s0*1812433253u+1;s2=s1*1812433253u+1;s3=s2*1812433253u+1;
        Synced=true;Draws=0;
    }
    private static uint Next(ref uint a,ref uint b,ref uint c,ref uint d)
    {
        uint t=a^(a<<11);t^=t>>8;
        a=b;b=c;c=d;d=d^(d>>19)^t;return d;
    }
    private static int Range(uint raw,int lo,int hi)=>hi>lo?lo+(int)(raw%(uint)(hi-lo)):hi<lo?lo-(int)(raw%(uint)(lo-hi)):lo;
    // A traced RandomRangeInt: advance and check the game's result.
    internal static void Int(int lo,int hi,int result)
    {
        if(!Synced)return;
        uint raw=Next(ref s0,ref s1,ref s2,ref s3);Draws++;
        if(Range(raw,lo,hi)!=result)Synced=false;
    }
    // A traced float draw (Random.value / Range(float)): one Xorshift step.
    internal static void Float() {if(Synced) {Next(ref s0,ref s1,ref s2,ref s3);Draws++;}}
    // The result RandomRangeInt(lo,hi) would give after skipping `skip` draws.
    internal static int Peek(int lo,int hi,int skip=0)
    {
        uint a=s0,b=s1,c=s2,d=s3,raw=0;
        for(int i=0;i<=skip;i++)raw=Next(ref a,ref b,ref c,ref d);
        return Range(raw,lo,hi);
    }
}
