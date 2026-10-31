using System;
using System.Linq;

namespace FF4Jobs.Core;

public static class Balance
{
    public static int StealChance(int userLevel, int enemyLevel, int thiefAbp) =>
        (int)Math.Clamp(35L + 5 * Jobs.Rank(Math.Clamp(thiefAbp,0,310)) + 2L * (userLevel-enemyLevel),15,75);

    public static int BattleAbp(int experience) => 1 + Math.Max(0,experience)/200;

    public static string EquipmentHint(string job) => job switch
    {
        "original"=>"Original equipment rules.",
        "dark_knight"=>"Dark swords, swords; heavy armor.",
        "paladin"=>"Swords, staves; heavy armor. Holy swords: Cecil only.",
        "dragoon"=>"Spears; heavy armor.",
        "monk"=>"Claws; light armor. Native barehand rules.",
        "thief"=>"Daggers, boomerangs; light armor.",
        "ninja"=>"Katana, daggers, boomerangs; light armor.",
        "ranger"=>"Bows, arrows, daggers; light armor.",
        "bard"=>"Harps, daggers; light armor.",
        "engineer"=>"Hammers, axes; heavy armor.",
        "white_mage"=>"Staves; mage armor.",
        "black_mage"=>"Rods, daggers; mage armor.",
        "summoner"=>"Rods, whips; mage armor. Native summons only.",
        "red_mage"=>"Swords, daggers, rods, staves; light/mage armor.",
        "time_mage" or "sage"=>"Rods, staves; mage armor.",
        _=>throw new ArgumentException("Unknown job")
    };

    // Categories/groups verified in FF4 PR build 20265023. No copied game assets.
    public static bool WeaponAllowed(string job,int category,int statusId)
    {
        if(category==43)return true; // Empty hand.
        return job switch
        {
            "dark_knight"=>category is 1 or 2,
            "paladin"=>category is 1 or 10 || (category==3 && statusId is 1 or 13),
            "dragoon"=>category==6,
            "monk"=>category==18,
            "thief"=>category is 5 or 14,
            "ninja"=>category is 4 or 5 or 14,
            "ranger"=>category is 5 or 12 or 13,
            "bard"=>category is 5 or 16,
            "engineer"=>category is 8 or 9,
            "white_mage"=>category==10,
            "black_mage"=>category is 5 or 11,
            "summoner"=>category is 11 or 19,
            "red_mage"=>category is 1 or 5 or 10 or 11,
            "time_mage" or "sage"=>category is 10 or 11,
            _=>false
        };
    }

    public static bool ArmorAllowed(string job,int group,bool nativeAllowed)
    {
        if(group is 4 or 20 or 27 or 15 or 29)return true; // Universal/light gear and armlets.
        if(group==25)return nativeAllowed; // Preserve native character restrictions.
        bool heavy=job is "dark_knight" or "paladin" or "dragoon" or "engineer";
        bool mage=job is "white_mage" or "black_mage" or "time_mage" or "summoner" or "sage" or "red_mage";
        return group switch
        {
            1=>job=="dark_knight",5=>job=="paladin",12 or 14=>heavy,
            16=>heavy || job=="monk",9=>mage,
            7=>job is "white_mage" or "sage",30=>job is "black_mage" or "sage",
            36=>job is "thief" or "ninja",_=>false
        };
    }

    public static string StatHint(Job job)
    {
        var values=new[]{("STR",job.Strength),("VIT",job.Vitality),("AGI",job.Agility),("INT",job.Intelligence),("SPR",job.Spirit)};
        return string.Join("  ",values.Where(v=>v.Item2!=0).Select(v=>v.Item1+" "+(v.Item2>0?"+":"")+v.Item2));
    }
}
