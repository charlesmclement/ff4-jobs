using System;

namespace FF4Jobs.Core;

public static class BloodCasting
{
    public static bool Applies(int status, bool paladinMilestone) => status == 1 && !paladinMilestone;
    public static int Cost(int mp) => (int)Math.Min(int.MaxValue, Math.Max(0L, mp) * 4);
    public static int RemainingHp(int hp, int mp) => hp <= 0 ? hp : (int)Math.Max(1L, (long)hp - Cost(mp));
}
