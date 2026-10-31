namespace FF4Jobs.Core;

public static class SpellProgression
{
    // Explicit player spell IDs avoid granting enemy/event duplicates (including
    // zero-MP versions) merely because they share a spell's display name.
    public static bool IsPlayerSpell(int id) => id is >=6 and <=29 or >=33 and <=56 or >=61 and <=75 or >=85 and <=90;
    public static int RequiredFlag(int id) => id switch
    {
        12 or 23 or 54 or 55 => 68, // Full-Life, Holy, Meteor, Flare: after the Giant.
        9 or 22 or 35 or 38 or 41 or 49 or 52 or 53 => 43,
        8 or 10 or 20 or 46 or 47 or 48 or 50 or 56 => 28,
        34 or 37 or 40 => 20,
        33 => 18, // Rydia's fire lesson must happen first.
        _ => -1
    };
    public static int RequiredAbp(int id) => id switch
    {
        12 or 23 or 54 or 55 => 310,
        9 or 22 or 35 or 38 or 41 or 49 or 52 or 53 => 150,
        8 or 10 or 20 or 46 or 47 or 48 or 50 or 56 => 70,
        34 or 37 or 40 or 42 => 30,
        7 or 11 or 14 or 15 or 16 or 17 or 18 or 19 or 21 or 24 or 27 or 29 => 10,
        _ => 0
    };
    public static bool CanLearn(Campaign campaign,int id,int abp,bool test=false)
    {
        if(!IsPlayerSpell(id)) return false;
        if(test) return true;
        int flag=RequiredFlag(id);
        // All high-tier unlocks require Tellah's Meteo scene even if an unusual
        // save has a later flag without the earlier one.
        if(RequiredAbp(id)>=70 && !campaign.StoryFlags.Contains(28)) return false;
        return abp>=RequiredAbp(id) && (flag<0 || campaign.StoryFlags.Contains(flag));
    }
}
