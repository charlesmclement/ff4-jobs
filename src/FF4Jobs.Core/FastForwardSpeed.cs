namespace FF4Jobs.Core;

// Own only the speed we set. Respect pauses and speed changes made by the game
// or another plugin instead of restoring a stale value over them.
public sealed class FastForwardSpeed
{
    private float original, applied;
    private bool owns;

    public float Update(float current, bool held, float multiplier)
    {
        if (owns && current != applied) owns = false;
        if (!held || current <= 0 || !float.IsFinite(current))
        {
            float restored = owns ? original : current;
            owns = false;
            return restored;
        }
        if (!owns) original = current;
        applied = original * System.Math.Clamp(multiplier, 1, 16);
        owns = true;
        return applied;
    }
}
