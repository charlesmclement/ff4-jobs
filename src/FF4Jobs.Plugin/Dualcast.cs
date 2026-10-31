using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Data.Master;
using Last.Data.User;
using Last.Management;
using Last.Systems;
using Last.UI.KeyInput;
using NativeAbilities=Il2CppSystem.Collections.Generic.List<Last.Data.User.OwnedAbility>;

namespace FF4Jobs;

internal static class Dualcast
{
    internal static BattleInfomationController? Selecting;
    internal static readonly Dictionary<IntPtr,BattleActData> SecondCasts=new();
    internal static bool Applies(BattleActData? act) => act?.Command?.Id==JobFeatures.Dualcast
        && State.Applies(act.AttackUnitData?.GetOwnedCharacterData());
    internal static bool IsSpell(Ability? a) => a!=null && a.AbilityGroupId is 1 or 2
        && SpellProgression.IsPlayerSpell(a.Id);
    internal static int ReservedMp(OwnedCharacterData c)
    {
        var ui=Selecting;
        if(ui?.commandSelectController?.SelectedCommand?.Id!=JobFeatures.Dualcast
            || ui.selectedTargetPlayer?.GetOwnedCharacterData()?.Pointer!=c.Pointer) return 0;
        var selected=ui.selectedBattleActData;
        long total=0;
        if(selected!=null) for(int i=0;i<selected.Count;i++)
        {
            var list=selected[i]?.abilityList;
            if(list!=null && list.Count>0) total+=Math.Max(0,list[0].UseValue);
        }
        return (int)Math.Min(int.MaxValue,total);
    }
}

// Supply the union of already projected/learned white and black spells. No new
// spells, school levels, or story flags are granted by mastering Dualcast.
[HarmonyPatch(typeof(AbilityFilter),nameof(AbilityFilter.GetAbilityList))]
internal static class DualcastSpellList
{
    static bool Prefix(Command command,NativeAbilities abilityList,ref NativeAbilities __result)
    {
        if(command?.Id!=JobFeatures.Dualcast) return true;
        __result=new NativeAbilities();
        for(int i=0;i<abilityList.Count;i++)
            if(Dualcast.IsSpell(abilityList[i]?.Ability)) __result.Add(abilityList[i]);
        return false;
    }
}

[HarmonyPatch(typeof(AbilityUtility),nameof(AbilityUtility.CanUseCommand))]
internal static class DualcastCommandCheck
{
    static bool Prefix(Command command,OwnedCharacterData target,ref bool __result)
    {
        if(command?.Id!=JobFeatures.Dualcast) return true;
        __result=false;
        if(!State.Applies(target)) return false;
        var spells=target.OwnedAbilityList;
        for(int i=0;i<spells.Count;i++)
            if(Dualcast.IsSpell(spells[i]?.Ability)
                && AbilityUtility.CanUseBattleAbility(target,command,spells[i],false))
            { __result=true;break; }
        return false;
    }
}

[HarmonyPatch(typeof(AbilityUtility),nameof(AbilityUtility.CanUseBattleAbility))]
internal static class DualcastSpellCheck
{
    // Keep the native school, Silence, content, targeting and MP checks.
    static void Prefix(ref Command selectedCommand,OwnedAbility selectAbility,out bool __state)
    {
        __state=selectedCommand?.Id==JobFeatures.Dualcast;
        if(__state && Dualcast.IsSpell(selectAbility?.Ability))
            selectedCommand=MasterManager.Instance.GetData<Command>(selectAbility!.Ability.AbilityGroupId==1?7:8);
    }
    static void Postfix(OwnedCharacterData target,OwnedAbility selectAbility,bool __state,ref bool __result)
    {
        if(!__state) return;
        if(!Dualcast.IsSpell(selectAbility?.Ability)) { __result=false;return; }
        if(__result && !BloodMagic.UsesHp(target))
            __result=JobFeatures.CanPayPair(target.Parameter.CurrentMP,Dualcast.ReservedMp(target),selectAbility!.Ability.UseValue);
    }
}

// Native NumberSelect=2 loops spell -> target twice. Reset on command entry so
// cancelling the second selection cannot carry its first cast into a new turn.
[HarmonyPatch(typeof(BattleInfomationController),"OnCommandSelect")]
internal static class DualcastSelection
{
    static void Prefix(BattleInfomationController __instance,Command command)
    {
        if(command?.Id==JobFeatures.Dualcast || Dualcast.Selecting?.Pointer==__instance.Pointer)
        {
            __instance.selectCount=0;
            __instance.selectedBattleActData?.Clear();
        }
        Dualcast.Selecting=command?.Id==JobFeatures.Dualcast?__instance:null;
    }
}

[HarmonyPatch(typeof(BattleInfomationController),nameof(BattleInfomationController.ClearCommandSelected))]
internal static class DualcastClear
{
    static void Postfix() => Dualcast.Selecting=null;
}

// FF4 normally gives each selected spell an independent ATB chant timer. Align
// the pair to the slower spell before either enters the execution queue. Native
// list order then releases both together, without a second ATB/charging phase.
[HarmonyPatch(typeof(BattleProgressATB),nameof(BattleProgressATB.SetActData))]
internal static class DualcastCharge
{
    static void Postfix(BattleProgressATB __instance,BattleActData battleActData)
    {
        if(!Dualcast.Applies(battleActData)) return;
        var chants=__instance.chantingInfo;
        if(chants==null) return;
        var indices=new List<int>();
        for(int i=0;i<chants.Count;i++)
        {
            var act=chants[i].actData;
            if(Dualcast.Applies(act) && act.AttackUnitData.Pointer==battleActData.AttackUnitData.Pointer)
                indices.Add(i);
        }
        if(indices.Count!=2) return;
        var first=chants[indices[0]]; var second=chants[indices[1]];
        float wait=JobFeatures.DualcastCharge(first.time,second.time);
        first.time=wait;second.time=wait;
        chants[indices[0]]=first;chants[indices[1]]=second;
        Dualcast.SecondCasts[second.actData.Pointer]=second.actData;
    }
}

// The follow-up goes straight to its spell animation; native effect completion,
// reflection, damage, MP payment and action cleanup remain intact.
[HarmonyPatch(typeof(ActEffectData),nameof(ActEffectData.PlayChanting))]
internal static class DualcastFollowupAnimation
{
    static bool Prefix(ActEffectData __instance) => __instance.BattleActData==null
        || !Dualcast.SecondCasts.ContainsKey(__instance.BattleActData.Pointer);
}

[HarmonyPatch(typeof(BattleProgressATB),nameof(BattleProgressATB.Clear))]
internal static class DualcastBattleClear
{
    static void Postfix() { Dualcast.SecondCasts.Clear();Dualcast.Selecting=null; }
}
