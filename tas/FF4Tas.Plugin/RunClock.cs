using UnityEngine;

namespace FF4Tas;

// TAS-owned speed boost (F8 toggles, default 3x via FF4JOBS_TAS_SPEED) and the
// route timer. Like Jobs fast-forward, it owns only the timeScale it applied:
// game pauses (0) and speed changes by the game or Jobs are left alone.
internal static class RunClock
{
    internal static bool Boost;
    internal static readonly float Multiplier=ParseMultiplier();
    // Real wall-clock run time, and the same run measured in game time, i.e.
    // what it would take at normal speed. They differ only while boosted.
    internal static double RealSeconds,GameSeconds;
    private static float original,applied;
    private static bool owns;

    private static float ParseMultiplier()
    {
        var text=Environment.GetEnvironmentVariable("FF4JOBS_TAS_SPEED");
        return float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var v)
            ? Math.Clamp(v,1,8) : 3;
    }
    internal static void Reset() {RealSeconds=GameSeconds=0;}
    // Called once per Unity frame after the route engine ticked.
    internal static void Update(bool counting)
    {
        if(Input.GetKeyDown(KeyCode.F8))Boost=!Boost;
        float current=Time.timeScale;
        if(owns && current!=applied)owns=false;
        if(!Boost || current<=0 || !float.IsFinite(current))
        {
            if(owns)Time.timeScale=original;
            owns=false;
        }
        else
        {
            if(!owns)original=current;
            applied=original*Multiplier;owns=true;
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
