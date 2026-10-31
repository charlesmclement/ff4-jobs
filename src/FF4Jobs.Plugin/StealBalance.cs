using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Data;

namespace FF4Jobs;

// Replace only the first detection roll inside this verified FF4 routine.
// Native loot selection, empty inventories and failure consequences remain intact.
[HarmonyPatch(typeof(Last.Battle.Function.StealItemFunction),"Calc")]
internal static class StealBalance
{
    [ThreadStatic] internal static int? PendingChance;
    internal static string LastAttempt="No modded Steal attempted this session.";
    static void Prefix(CharacterParameterBase attackParameter,CharacterParameterBase targetParameter,out int? __state)
    {
        __state=PendingChance;PendingChance=null;
        var user=attackParameter;var target=targetParameter;
        var c=user?.TryCast<PlayerCharacterParameter>()?.parent;
        if(c==null || !State.Applies(c) || target?.TryCast<EnemyCharacterParameter>()==null)return;
        var p=State.Campaign.Character(c.characterStatusId);
        if(p.Main!="thief" && (p.Sub!="thief" || (!Plugin.TestAllSkills.Value && p.Points("thief")<10)))return;
        PendingChance=Balance.StealChance(user!.ConfirmedLevel(),target!.ConfirmedLevel(),Plugin.TestAllSkills.Value?310:p.Points("thief"));
        LastAttempt=$"{c.Name}: {PendingChance}% detection-check success (Thief ABP {p.Points("thief")}).";
    }
    static void Postfix(Last.Battle.Function.StealItemFunction __instance)
    {
        var result=__instance.GetCalcResult();
        if(result!=null)Plugin.Logger.LogInfo($"Steal native result: hit={result.GetHitType()}, value={result.GetValue()}, item={result.TryCast<Last.Systems.CalcResult>()?.GetStealContentId()}");
    }
    static void Finalizer(int? __state) => PendingChance=__state;
}

[HarmonyPatch(typeof(CommonUtility),nameof(CommonUtility.GetRandPlus),new[]{typeof(int)})]
internal static class StealDetectionRoll
{
    static bool Prefix(int max,ref int __result)
    {
        if(max!=100 || StealBalance.PendingChance is not int chance)return true;
        StealBalance.PendingChance=null; // Further native rolls are never replaced.
        bool success=UnityEngine.Random.Range(0,100)<chance;
        __result=success?0:100; // Native threshold always lies in [0,95].
        StealBalance.LastAttempt+=" Roll: "+(success?"passed":"failed");
        Plugin.Logger.LogInfo(StealBalance.LastAttempt);
        return false;
    }
}
