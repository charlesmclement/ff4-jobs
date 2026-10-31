using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Data.Master;
using Last.Data.User;
using Last.Management;
using Last.Systems;

namespace FF4Jobs;

internal static class BloodMagic
{
    internal static bool UsesHp(OwnedCharacterData? c) => c != null
        && BloodCasting.Applies(c.characterStatusId, State.Campaign.StoryFlags.Contains(28)) && State.Applies(c);
    internal static bool IsSpell(Ability? a) => a != null && SpellProgression.IsPlayerSpell(a.Id)
        && a.AbilityGroupId is >= 1 and <= 4;
    internal static bool Applies(OwnedCharacterData? c, Ability? a) => UsesHp(c) && IsSpell(a);
    internal static bool Applies(BattleActData? act) => act != null
        && act.itemList?.Count == 0 && act.abilityList?.Count > 0
        && Applies(act.AttackUnitData?.GetOwnedCharacterData(), act.abilityList[0]);
    internal static void Pay(OwnedCharacterData c, Ability a)
    {
        int before = c.Parameter.CurrentHP;
        c.Parameter.CurrentHP = BloodCasting.RemainingHp(before, a.UseValue);
        Plugin.Logger.LogInfo($"Cecil HP spell {a.Id}: {before} -> {c.Parameter.CurrentHP} HP; MP unchanged.");
    }
}

// Change only the resource test, leaving native targeting, Silence, conditions,
// equipment, and learned-ability checks in their original callers intact.
[HarmonyPatch(typeof(AbilityUtility), "IsConsumptionValue")]
internal static class BloodMagicResourceCheck
{
    static bool Prefix(OwnedCharacterData target, Ability ability, ref bool __result)
    {
        if (!BloodMagic.Applies(target, ability)) return true;
        __result = target.Parameter.CurrentHP > 0;
        return false;
    }
}

[HarmonyPatch(typeof(Serial.FF4.Management.SerialBattleUtility), "IsUsableAbility")]
internal static class BloodMagicExecutionCheck
{
    // FF4's action factory calls this resource-only provider directly. Patching
    // BattleUtility.IsAbilityAct covers auto-selection, but not manual casts:
    // the factory then creates UnexplodedActFunction ("No effect") at zero MP.
    // Conditions/targets remain checked separately by the native action factory.
    static bool Prefix(BattleUnitData battleUnitData, Ability ability, ref bool __result)
    {
        var character = battleUnitData?.GetOwnedCharacterData();
        if (!BloodMagic.Applies(character, ability)) return true;
        __result = character!.Parameter.CurrentHP > 0;
        return false;
    }
}

// Record the actual action chosen during local playtests, without changing it.
[HarmonyPatch(typeof(Serial.FF4.Calc.FunctionProvider), "CreateActFunction")]
internal static class BloodMagicActionTrace
{
    static void Postfix(BattleActData battleActData, ActFunctionBase __result)
    {
        if (Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE") != "1"
            || !BloodMagic.Applies(battleActData)) return;
        Plugin.Logger.LogInfo($"Cecil HP spell {battleActData.abilityList[0].Id}: native action {__result?.GetIl2CppType().Name}.");
    }
}

[HarmonyPatch(typeof(BattleActExection), "UseMp")]
internal static class BloodMagicBattlePayment
{
    static bool Prefix(BattleActExection __instance)
    {
        var act = __instance.InStagingBattleActData;
        if (!BloodMagic.Applies(act)) return true;
        if (act.IsExecuteMp)
        {
            BloodMagic.Pay(act.AttackUnitData.GetOwnedCharacterData(), act.abilityList[0]);
            act.IsExecuteMp = false; // Same cast (including reflection) pays once.
        }
        return false;
    }
}

[HarmonyPatch(typeof(OwnedCharacterClient), "ConsumptionExecution")]
internal static class BloodMagicMenuPayment
{
    static bool Prefix(ref OwnedCharacterData user, OwnedAbility useAbility)
    {
        if (useAbility == null || !BloodMagic.Applies(user, useAbility.Ability)) return true;
        BloodMagic.Pay(user, useAbility.Ability);
        return false;
    }
}

[HarmonyPatch(typeof(Last.UI.KeyInput.BattleAbilityInfomationContentController), "UpdateView",
    new[] { typeof(OwnedAbility), typeof(bool) })]
internal static class BloodMagicCostLabel
{
    static void Postfix(Last.UI.KeyInput.BattleAbilityInfomationContentController __instance,
        OwnedAbility ownedAbility, bool isEnableMpText)
    {
        if (ownedAbility == null || !BloodMagic.IsSpell(ownedAbility.Ability)) return;
        bool hp = BloodMagic.Applies(Projection.AbilityOwner(ownedAbility), ownedAbility.Ability);
        var view = __instance.view;
        if (view == null) return;
        if (view.fixText != null) view.fixText.text = hp ? "HP" : "MP";
        if (hp && isEnableMpText) view.SetValue(BloodCasting.Cost(ownedAbility.Ability.UseValue).ToString());
    }
}

[HarmonyPatch(typeof(Serial.FF4.UI.KeyInput.AbilityContentListController), "SetUseValue")]
internal static class BloodMagicMenuCostLabel
{
    static void Postfix(Serial.FF4.UI.KeyInput.AbilityContentListController __instance, int index)
    {
        var list = __instance.AbilityList;
        if (list == null || index < 0 || index >= list.Count) return;
        var ability = list[index]?.Ability;
        bool hp = BloodMagic.Applies(__instance.TargetData, ability);
        var view = __instance.view;
        if (view == null) return;
        if (view.fixedMpText != null) view.fixedMpText.text = hp ? "HP" : "MP";
        if (hp && view.mpValueText != null) view.mpValueText.text = BloodCasting.Cost(ability!.UseValue).ToString();
    }
}
