using System;

namespace FF4Jobs.Core;

public static class JobFeatures
{
    public const int RapidFire = 9001;
    public const int Dualcast = 9002;
    public const int RapidFireHits = 4;
    public static int CommandAbp(int id, bool sub, int fallback) =>
        id == RapidFire ? (sub ? 310 : 150) : id == Dualcast ? 310 : fallback;
    public static bool StoryAllows(Campaign campaign, int command) =>
        command != RapidFire || campaign.StoryFlags.Contains(43);
    public static int RapidFireDamage(int damage) => damage <= 0 ? damage : Math.Max(1, damage / 2);
    public static float DualcastCharge(float first,float second) => Math.Max(0,Math.Max(first,second));
    public static bool CanPayPair(int mp,int reserved,int cost) =>
        (long)Math.Max(0,reserved)+Math.Max(0,cost)<=mp;
}
