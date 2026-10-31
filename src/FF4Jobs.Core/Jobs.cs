using System;
using System.Collections.Generic;
using System.Linq;

namespace FF4Jobs.Core;

public sealed class Job
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int[] Commands { get; }
    public string MagicSchool { get; }
    public int Strength { get; }
    public int Vitality { get; }
    public int Agility { get; }
    public int Intelligence { get; }
    public int Spirit { get; }
    public bool IsCaster => MagicSchool.Length > 0;
    public int CommandRequirement(int index, bool sub) => JobFeatures.CommandAbp(Commands[index],sub,
        sub ? Jobs.Thresholds[index] : index==0 ? 0 : Jobs.Thresholds[index-1]);
    public int[] LearnedCommands(int points, bool sub) => Commands.Where((_,i)=>points>=CommandRequirement(i,sub)).ToArray();

    public Job(string id, string name, string description, int[] commands,
        string magicSchool = "", int str = 0, int vit = 0, int agi = 0, int intel = 0, int spr = 0)
    {
        Id = id; Name = name; Description = description; Commands = commands;
        MagicSchool = magicSchool; Strength = str; Vitality = vit;
        Agility = agi; Intelligence = intel; Spirit = spr;
    }
}

public static class Jobs
{
    // Verified against the installed game's event scripts (ScenarioFlag1).
    public static readonly Dictionary<string,int> UnlockFlags = new()
    {
        ["dark_knight"]=7, ["dragoon"]=43, ["thief"]=7,
        ["black_mage"]=13, ["bard"]=16, ["ranger"]=17, ["monk"]=20,
        ["paladin"]=28, ["white_mage"]=107, ["red_mage"]=43,
        ["time_mage"]=43, ["summoner"]=46, ["engineer"]=49,
        ["ninja"]=53, ["sage"]=68
    };
    public static string UnlockHint(string job) => job switch
    {
        "dark_knight" or "thief"=>"Kain leaves at Mist",
        "dragoon"=>"Kain returns; escape the Tower of Zot",
        "black_mage"=>"Defeat Octomammoth", "bard"=>"Defeat Antlion",
        "ranger"=>"Cure Rosa", "monk"=>"Finish the defense of Fabul",
        "paladin"=>"Cecil becomes a Paladin", "white_mage"=>"Escape Baron after the twins' sacrifice",
        "red_mage" or "time_mage"=>"Escape the Tower of Zot",
        "summoner"=>"Rydia returns; defeat Golbez", "engineer"=>"Cid seals the underworld passage",
        "ninja"=>"Defeat Rubicante", "sage"=>"Defeat the Giant of Babil", _=>"Original class"
    };
    // Original keeps native commands/equipment, with earned mastery stats and innate passives.
    public static readonly Job[] All = {
        new("original", "Original", "Native FF4 commands; inherits mastered job stats and passives.", Array.Empty<int>()),
        new("dark_knight", "Dark Knight", "Darkness; trades vitality for physical power.", new[]{10}, str:8, vit:-3, spr:-3),
        new("paladin", "Paladin", "Cover and white magic; resilient protector.", new[]{14,7}, "paladin", str:3, vit:6, agi:-3, spr:6),
        new("dragoon", "Dragoon", "Jump; strong physical attacks.", new[]{11}, str:6, vit:3, agi:-3),
        new("monk", "Monk", "Focus, Kick and Gird. Barehand grows with your level and Monk ABP; both hands empty.", new[]{25,26,27}, str:8, vit:6, intel:-5, spr:-3),
        new("white_mage", "White Mage", "White magic and Pray.", new[]{7,12}, "white", str:-4, intel:3, spr:12),
        new("black_mage", "Black Mage", "Black magic and Bluff.", new[]{8,20}, "black", str:-4, vit:-2, intel:12),
        new("red_mage", "Red Mage", "Intermediate white/black magic. At 310 ABP, Dualcast selects two learned spells and targets.", new[]{7,8,JobFeatures.Dualcast}, "red", str:2, agi:3, intel:6, spr:6),
        new("time_mage", "Time Mage", "Haste, Slow, Stop and related magic drawn from FF4.", new[]{7,8}, "time", agi:5, intel:9, spr:3),
        new("thief", "Thief", "Steal; fast, lightly built adventurer.", new[]{17}, str:1, agi:12, vit:-2),
        new("ninja", "Ninja", "Ninjutsu and Throw. Dual Wield: 70 main / 150 sub ABP.", new[]{15,16}, "ninja", str:4, agi:8, intel:4),
        new("bard", "Bard", "Sing and Heal using FF4's bard actions.", new[]{22,23}, agi:4, spr:6),
        new("ranger", "Ranger", "Aim. Rapid Fire: four half-damage attacks after Zot; 150 main / 310 sub ABP.", new[]{13,JobFeatures.RapidFire}, str:3, agi:7),
        new("summoner", "Summoner", "Story-earned summons; your Summoner ABP unlocks stronger ones.", new[]{9}, "summon", str:-5, intel:10, spr:2),
        new("engineer", "Engineer", "Study enemies; sturdy combat specialist.", new[]{31}, str:4, vit:8, agi:-4),
        new("sage", "Sage", "White and black magic, with slower movement in battle.", new[]{7,8}, "sage", str:-5, agi:-5, intel:10, spr:10)
    };
    public static Job Get(string id) => All.FirstOrDefault(j => j.Id == id)
        ?? throw new ArgumentException("Unknown job: " + id, nameof(id));
    public static bool Exists(string id) => All.Any(j => j.Id == id);

    // Prototype balance values, deliberately not claimed to be FF5's exact ABP tables.
    public static readonly int[] Thresholds = {10,30,70,150,310};
    public static int Rank(int abp) => Thresholds.Count(t => abp >= t);
    public static int NextThreshold(int abp) => Thresholds.FirstOrDefault(t => t > abp);
    public static string CharacterKey(int statusId) => statusId == 13 ? "1" : statusId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class CharacterProgress
{
    public string Main { get; set; } = "original";
    public string? Sub { get; set; }
    public Dictionary<string,int> Abp { get; set; } = new();
    // Quarter-ABP carry belongs to the job, not the slot. Optional in old saves.
    public Dictionary<string,int> AbpQuarters { get; set; } = new();
    public int Points(string job) => Abp.TryGetValue(job, out int n) ? n : 0;
    public int Quarters(string job) => AbpQuarters.TryGetValue(job, out int n) ? n : 0;
    public decimal ExactPoints(string job) => Points(job) + Quarters(job) / 4m;
}

public sealed class Campaign
{
    public int Schema { get; set; } = 1;
    public Dictionary<string,CharacterProgress> Characters { get; set; } = new();
    public HashSet<int> RecruitedCharacters { get; set; } = new();

    public HashSet<int> StoryFlags { get; set; } = new();
    public HashSet<string> AnnouncedJobs { get; set; } = new();
    public LearningJournal Learning { get; set; } = new();
    public HashSet<int> LearnedSummons { get; set; } = new();
    public bool Recruit(int statusId) => statusId>=1 && statusId<=13
        && RecruitedCharacters.Add(statusId);
    public bool IsUnlocked(string job) => job == "original"
        || (Jobs.UnlockFlags.TryGetValue(job,out int flag) && StoryFlags.Contains(flag));

    // Dark Knight is part of Cecil/Kain's identity, never a party-wide class.
    public bool IsAvailable(int statusId, string job, bool testAllJobs = false)
    {
        if(job == "dark_knight" && statusId is not (1 or 2 or 13)) return false;
        return testAllJobs || IsUnlocked(job) || (job == "dark_knight" && statusId is 1 or 13);
    }
    public string[] PendingAnnouncements() => Jobs.All
        .Where(j=>j.Id is not ("original" or "dark_knight") && IsUnlocked(j.Id) && !AnnouncedJobs.Contains(j.Id))
        .Select(j=>j.Id).ToArray();

    // Reconcile older/test saves after native story flags have been synchronized.
    // Job choices may become invalid as the schedule changes; earned ABP stays.
    public bool ReconcileSelections(bool testAllJobs = false)
    {
        bool changed=AnnouncedJobs.RemoveWhere(id=>!IsUnlocked(id))>0;
        foreach(var entry in Characters)
        {
            int status=int.Parse(entry.Key); var p=entry.Value;
            if(!IsAvailable(status,p.Main,testAllJobs)) { p.Main="original"; changed=true; }
            if(p.Sub!=null && !IsAvailable(status,p.Sub,testAllJobs)) { p.Sub=null; changed=true; }
        }
        return changed;
    }

    public CharacterProgress Character(int statusId)
    {
        string key = Jobs.CharacterKey(statusId);
        if (!Characters.TryGetValue(key, out var p)) Characters[key] = p = new CharacterProgress();
        return p;
    }

    public void Select(int statusId, string main, string? sub, bool testAllJobs = false, bool testAllSkills = false)
    {
        Jobs.Get(main);
        if (sub != null) Jobs.Get(sub);
        if (main == sub) throw new ArgumentException("Main job and subjob must differ.");
        if (sub == "original") throw new ArgumentException("Original is only available as a main job.");
        if (!IsAvailable(statusId,main,testAllJobs) || (sub != null && !IsAvailable(statusId,sub,testAllJobs)))
            throw new InvalidOperationException("This job is not available to this character yet.");
        var p = Character(statusId);
        // Story unlocks permit both slots. ABP gates the subjob's skills, not its selection.
        p.Main = main; p.Sub = sub;
    }

    public int Award(int statusId, int points)
    {
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        var p = Character(statusId);
        if (p.Main == "original") return 0;
        int before = p.Points(p.Main);
        p.Abp[p.Main] = (int)Math.Min(Jobs.Thresholds[Jobs.Thresholds.Length-1], (long)before + points);
        if (p.Points(p.Main) == Jobs.Thresholds[Jobs.Thresholds.Length-1]) p.AbpQuarters.Remove(p.Main);
        return p.Points(p.Main) - before;
    }

    public (int Main, int SubQuarters) AwardBattle(int statusId, int points)
    {
        int main = Award(statusId, points);
        var p = Character(statusId);
        if (p.Sub == null || points == 0) return (main, 0);
        string sub = p.Sub;
        int before = p.Points(sub) * 4 + p.Quarters(sub);
        int after = (int)Math.Min(Jobs.Thresholds[Jobs.Thresholds.Length-1] * 4L, (long)before + points);
        p.Abp[sub] = after / 4;
        if (after % 4 == 0) p.AbpQuarters.Remove(sub);
        else p.AbpQuarters[sub] = after % 4;
        return (main, after - before);
    }

    public int[] Commands(int statusId, IEnumerable<int> original, bool testAllSkills = false)
    {
        var p = Character(statusId);
        var result = p.Main == "original" ? original.ToList() : new List<int>{1};
        if (p.Main != "original") result.AddRange(Jobs.Get(p.Main).LearnedCommands(testAllSkills?310:p.Points(p.Main),false));
        if (p.Sub != null)
        {
            var sub = Jobs.Get(p.Sub);
            result.AddRange(sub.LearnedCommands(testAllSkills?310:p.Points(p.Sub),true));
        }
        result.Add(3);
        if(p.Main != "original") result.AddRange(original.Where(id => id is 2 or 4 or 5 or 6 or 59 or 60 or 61 or 62));
        return result.Distinct().Where(id=>testAllSkills || JobFeatures.StoryAllows(this,id)).ToArray();
    }

    public void Validate()
    {
        if(Learning==null) throw new InvalidOperationException("Missing skill announcement data.");
        Learning.Validate();
        if(LearnedSummons==null || LearnedSummons.Any(id=>id<61 || id>75))
            throw new InvalidOperationException("Invalid learned summons.");
        if (Schema != 1 || Characters == null) throw new InvalidOperationException("Unsupported or invalid job save data.");
        if (RecruitedCharacters == null || RecruitedCharacters.Any(id => id<1 || id>13))
            throw new InvalidOperationException("Invalid recruitment history.");
        if (StoryFlags == null || StoryFlags.Any(id=>id<0 || id>255)) throw new InvalidOperationException("Invalid story flags.");
        if (AnnouncedJobs == null || AnnouncedJobs.Any(id=>!Jobs.Exists(id))) throw new InvalidOperationException("Invalid unlock announcements.");
        foreach (var entry in Characters)
        {
            if (!int.TryParse(entry.Key, out int id) || id < 1 || id > 12)
                throw new InvalidOperationException("Invalid character key.");
            var p = entry.Value ?? throw new InvalidOperationException("Missing character progress.");
            Jobs.Get(p.Main);
            if (p.Abp == null) throw new InvalidOperationException("Missing ABP data.");
            foreach(var abp in p.Abp)
                if (!Jobs.Exists(abp.Key) || abp.Value < 0 || abp.Value > Jobs.Thresholds[Jobs.Thresholds.Length-1])
                    throw new InvalidOperationException("Invalid ABP data.");
            if (p.AbpQuarters == null) throw new InvalidOperationException("Missing fractional ABP data.");
            foreach (var abp in p.AbpQuarters)
                if (!Jobs.Exists(abp.Key) || abp.Key == "original" || abp.Value < 0 || abp.Value > 3
                    || (abp.Value > 0 && p.Points(abp.Key) >= Jobs.Thresholds[Jobs.Thresholds.Length-1]))
                    throw new InvalidOperationException("Invalid fractional ABP data.");
            // A test-selected subjob may be saved before training; it contributes
            // no unearned commands/spells when the test override is disabled.
            if (p.Sub != null && (!Jobs.Exists(p.Sub) || p.Sub == "original" || p.Sub == p.Main))
                throw new InvalidOperationException("Invalid subjob.");
        }
    }
}
