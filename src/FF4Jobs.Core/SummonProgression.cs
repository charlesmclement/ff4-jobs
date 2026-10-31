namespace FF4Jobs.Core;

public static class SummonProgression
{
    public static int RequiredAbp(int id) => id switch
    {
        61 or 65 => 0, // Goblin, Chocobo
        62 => 10, // Bomb
        63 or 64 or 70 => 30, // Cockatrice, Mind Flayer, Mist Dragon
        66 or 67 or 68 or 71 => 70, // Shiva, Ramuh, Ifrit, Sylph
        69 or 72 => 150, // Titan, Odin
        73 or 74 or 75 => 310, // Leviathan, Asura, Bahamut
        _ => int.MaxValue
    };
    public static bool CanUse(Campaign campaign,int id,int points,bool sub)
        => campaign.LearnedSummons.Contains(id) && points>=RequiredAbp(id)
            && (!sub || points>=10)
            && (RequiredAbp(id)<70 || campaign.StoryFlags.Contains(28));
}
