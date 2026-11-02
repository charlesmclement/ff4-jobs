using UnityEngine;

namespace FF4Tas;

// TAS-owned speed: F8 +1x, F7 -1x, 1x..30x (starts at 6x, or FF4JOBS_TAS_SPEED;
// FF4JOBS_TAS_BOOST=0 starts at 1x), and the route timer. While the route walks
// the field the applied speed is capped at FieldCap so movement can still stop
// on its path cells. Like Jobs fast-forward, it owns only the timeScale it
// applied: game pauses (0) and speed changes by the game or Jobs are left alone.
internal static class RunClock
{
    internal const int MaxSpeed=30,FieldCap=8;
    internal static int Speed=Environment.GetEnvironmentVariable("FF4JOBS_TAS_BOOST")=="0"?1:ParseSpeed();
    // Speed actually applied this frame (Speed, or FieldCap while walking).
    internal static int Applied=1;
    internal static bool Boost=>Speed>1;
    internal static float Multiplier=>Speed;
    // Real wall-clock run time, and the same run measured in game time, i.e.
    // what it would take at normal speed. They differ only while boosted.
    internal static double RealSeconds,GameSeconds;
    private static float original,applied;
    private static bool owns;

    private static int ParseSpeed()
    {
        var text=Environment.GetEnvironmentVariable("FF4JOBS_TAS_SPEED");
        return int.TryParse(text,out var v)?Math.Clamp(v,1,MaxSpeed):6;
    }
    internal static void Reset() {RealSeconds=GameSeconds=0;}
    // Called once per Unity frame after the route engine ticked.
    internal static void Update(bool counting,bool walking)
    {
        if(Input.GetKeyDown(KeyCode.F8))Speed=Math.Min(MaxSpeed,Speed+1);
        // F6+F7 is the Jobs fast-forward skip chord; only F7 alone slows down.
        if(Input.GetKeyDown(KeyCode.F7) && !Input.GetKey(KeyCode.F6))Speed=Math.Max(1,Speed-1);
        Applied=walking?Math.Min(Speed,FieldCap):Speed;
        float current=Time.timeScale;
        if(owns && current!=applied)owns=false;
        if(Applied<=1 || current<=0 || !float.IsFinite(current))
        {
            if(owns)Time.timeScale=original;
            owns=false;
        }
        else
        {
            if(!owns)original=current;
            applied=original*Applied;owns=true;
            Time.timeScale=applied;
        }
        if(counting) {RealSeconds+=Time.unscaledDeltaTime;GameSeconds+=Time.deltaTime;}
    }
    internal static void Restore() {if(owns)Time.timeScale=original;owns=false;}
    internal static string Format(double seconds)
    {
        var t=TimeSpan.FromSeconds(seconds);
        return t.TotalHours>=1?$"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds/10:00}":$"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds/10:00}";
    }
}
