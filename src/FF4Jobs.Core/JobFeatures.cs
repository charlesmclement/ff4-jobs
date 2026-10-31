using System;

namespace FF4Jobs.Core;

public static class JobFeatures
{
    public const int RapidFire = 9001;
    public const int Dualcast = 9002;
    public static int PassiveAbp(string job,bool sub) => job switch { "ninja"=>(sub?150:70), "monk"=>(sub?70:0), _=>int.MaxValue };
    public static string? PassiveName(string job) => job switch { "ninja"=>"Dual Wield", "monk"=>"Barehand", _=>null };
    public static bool HasPassive(CharacterProgress p,string job,bool test=false) =>
        (p.Main==job && (test || p.Points(job)>=PassiveAbp(job,false)))
        || (p.Sub==job && (test || p.Points(job)>=PassiveAbp(job,true)))
        || (p.Main=="original" && PassiveName(job)!=null && p.Points(job)>=310);
    public static bool HasBarehand(CharacterProgress p,int characterStatus,bool test=false) =>
        characterStatus==8 || HasPassive(p,"monk",test); // Yang retains his innate training.
    public static int UnarmedPower(int level) => 2*Math.Clamp(level,1,99)+3;
    public static bool MonkHands(int first,int second) =>
        first is HandEquipment.Empty or HandEquipment.Claw && second is HandEquipment.Empty or HandEquipment.Claw;
    public static int MonkStrikes(bool trained,int first,int second,bool normalAttack) =>
        trained && MonkHands(first,second) && normalAttack ? 2 : 1;
    // Equal trained accuracy keeps claws' native item accuracy from overwhelming
    // the small barehand damage edge. Temporary accuracy/status modifiers remain native.
    public static int MonkAccuracy(int level) => 50+Math.Clamp(level,1,99)/4;
    public static int BarehandDamage(int damage) => damage<=0 ? damage
        : (int)Math.Min(int.MaxValue,(long)damage*105/100);
    public static bool DualWieldWeapon(int category,bool twoHanded) =>
        !twoHanded && category is not (12 or 13 or 43);
    public const int RapidFireHits = 4;
    public static int CommandAbp(int id, bool sub, int fallback) =>
        id == RapidFire ? (sub ? 310 : 150) : id == Dualcast ? 310 : fallback;
    public static bool StoryAllows(Campaign campaign, int command) =>
        command != RapidFire || campaign.StoryFlags.Contains(43);
    public static int RapidFireDamage(int damage) => damage <= 0 ? damage : Math.Max(1, damage / 2);
    public static int PhysicalAttackDamage(int damage,bool rapidFire,bool barehand) =>
        rapidFire ? RapidFireDamage(damage) : barehand ? BarehandDamage(damage) : damage;
    public static float DualcastCharge(float first,float second) => Math.Max(0,Math.Max(first,second));
    public static bool CanPayPair(int mp,int reserved,int cost) =>
        (long)Math.Max(0,reserved)+Math.Max(0,cost)<=mp;
}
