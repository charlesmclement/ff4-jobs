using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Data;
using Last.Data.Master;
using Last.Data.User;
using Last.Defaine;
using NativeActs=Il2CppSystem.Collections.Generic.List<Last.Battle.BattleActData>;

namespace FF4Jobs;

internal static class Monk
{
    internal static bool Trained(OwnedCharacterData c) => State.Applies(c,true)
        && (c.characterStatusId==8 || State.Campaign.IsAvailable(c.characterStatusId,"monk",Plugin.TestAllJobs.Value))
        && JobFeatures.HasBarehand(State.Campaign.Character(c.characterStatusId),c.characterStatusId,Plugin.TestAllSkills.Value);
    internal static bool EmptyHands(OwnedCharacterData c) =>
        HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot1))==HandEquipment.Empty
        && HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot2))==HandEquipment.Empty;
    internal static bool MartialHands(OwnedCharacterData c) => JobFeatures.MonkHands(
        HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot1)),
        HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot2)));

    // Keep native wrappers alive until battle cleanup so native addresses cannot be
    // reused while they identify the two separate fist actions.
    internal static readonly Dictionary<IntPtr,(BattleActData Act,bool Barehand)> Fists=new();
    internal static void Clear() { Fists.Clear();PhysicalAttackBalance.CalculatingAct=null; }
    internal static bool IsFist(BattleActData? act) => act!=null && Fists.ContainsKey(act.Pointer);
    internal static int Strikes(BattleActData act)
    {
        var c=act.AttackUnitData?.GetOwnedCharacterData();
        if(c==null) return 1;
        bool normal=act.Command?.Id==1 && act.CurrentActType==BattleActData.ActType.Normal
            && act.CurrentUseType==BattleActData.UseType.Ability
            && act.abilityList?.Count==1 && act.abilityList[0]?.Id==1;
        return JobFeatures.MonkStrikes(Trained(c),
            HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot1)),
            HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot2)),normal);
    }
}

// FFV-style base power per fist. Native physical damage processing supplies the
// accuracy, defense, strength-based modifiers and damage variance for EACH strike.
[HarmonyPatch(typeof(Serial.FF4.Management.ParameterProvider),"GetOffensivePower")]
internal static class MonkAttackPower
{
    static bool Prefix(OwnedCharacterData data,ref int __result)
    {
        if(!Monk.Trained(data) || !Monk.MartialHands(data)) return true;
        __result=JobFeatures.UnarmedPower(data.Parameter.ConfirmedLevel());
        return false;
    }
}

// Both loadouts use the same trained base accuracy; native temporary accuracy,
// blindness and final limits still run in ConfirmedAccuracyRate.
[HarmonyPatch(typeof(Serial.FF4.Management.ParameterProvider),"GetAccuracyRate")]
internal static class MonkAccuracy
{
    static bool Prefix(OwnedCharacterData data,ref int __result)
    {
        if(!Monk.Trained(data) || !Monk.MartialHands(data)) return true;
        __result=JobFeatures.MonkAccuracy(data.Parameter.ConfirmedLevel());
        return false;
    }
}

// The native confirmed getter adds a separate equipment-accuracy cache. Exclude
// that cache only during this synchronous calculation; restore even on exceptions.
// Never alter equipped items, their elemental/status data, or saved base stats.
[HarmonyPatch(typeof(PlayerCharacterParameter),nameof(PlayerCharacterParameter.ConfirmedAccuracyRate))]
internal static class MonkEquipmentAccuracy
{
    static void Prefix(PlayerCharacterParameter __instance,ref int? __state)
    {
        var c=__instance.parent;
        if(c==null || !Monk.Trained(c) || !Monk.MartialHands(c)) return;
        __state=__instance.EquipAccuracyRate;
        __instance.EquipAccuracyRate=0;
    }
    static void Finalizer(PlayerCharacterParameter __instance,int? __state)
    {
        if(__state.HasValue) __instance.EquipAccuracyRate=__state.Value;
    }
}

// Two native calculations, not a doubled damage number. Both belong to the same
// ATB turn. The native queue handles target death, counters and incapacitation.
// Do not expand Jump, Kick, Focus, counters or Rapid Fire.
[HarmonyPatch(typeof(BattleActExection),nameof(BattleActExection.SetBattleActData),new[]{typeof(BattleActData)})]
internal static class MonkAttackQueue
{
    [ThreadStatic] private static bool expanding;
    static bool Prefix(BattleActExection __instance,BattleActData battleActData)
    {
        if(expanding || Monk.IsFist(battleActData) || Monk.Strikes(battleActData)!=2) return true;
        var abilities=new Il2CppSystem.Collections.Generic.List<Ability>();
        abilities.Add(MasterManager.Instance.GetData<Ability>(1));
        var second=new BattleActData(battleActData.AttackUnitData,battleActData.TargetUnitDatas,
            battleActData.Command,abilities,battleActData.CurrentActType,
            battleActData.CurrentTargetType,battleActData.UseArmType);
        bool barehand=Monk.EmptyHands(battleActData.AttackUnitData.GetOwnedCharacterData());
        Monk.Fists[battleActData.Pointer]=(battleActData,barehand);
        Monk.Fists[second.Pointer]=(second,barehand);
        var pair=new NativeActs();pair.Add(battleActData);pair.Add(second);
        expanding=true;
        try { __instance.SetBattleActData(pair); }
        finally { expanding=false; }
        return false;
    }
}

// FF4 can repeat each weapon's animation within an action. Our two independent
// actions already supply the two strikes, so don't repeat both claws per action.
[HarmonyPatch(typeof(FightActFunctionFF4),"IsRepertEffect")]
internal static class MonkStrikeAnimation
{
    static bool Prefix(FightActFunctionFF4 __instance,ref bool __result)
    {
        if(!Monk.IsFist(__instance.BattleActData)) return true;
        __result=false;return false;
    }
}

// Native fists normally show two swings for a single calculation. Each queued
// fist now owns one calculation and one swing, avoiding four cosmetic punches.
[HarmonyPatch(typeof(FightActFunctionFF4),"GetAttackCount")]
internal static class MonkFistAnimation
{
    static void Postfix(FightActFunctionFF4 __instance,ref int __result)
    {
        if(Monk.IsFist(__instance.BattleActData)) __result=1;
    }
}

[HarmonyPatch(typeof(BattleProgressATB),nameof(BattleProgressATB.Clear))]
internal static class MonkBattleCleanup
{
    static void Postfix() => Monk.Clear();
}
