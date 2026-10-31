using System;

namespace FF4Jobs.Core;

public static class JobFeatures
{
    public const int RapidFire = 9001;
    public const int RapidFireHits = 4;
    public static int CommandAbp(int id, bool sub, int fallback) =>
        id == RapidFire ? (sub ? 310 : 150) : fallback;
    public static bool StoryAllows(Campaign campaign, int command) =>
        command != RapidFire || campaign.StoryFlags.Contains(43);
    public static int RapidFireDamage(int damage) => damage <= 0 ? damage : Math.Max(1, damage / 2);
}
