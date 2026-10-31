using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Battle.Function;

namespace FF4Jobs;

// Native SetUpFunctions calls Calc synchronously. The small GetSumCalcResult
// helper is inlined at callers in this build and cannot reliably scale damage.
internal static class PhysicalAttackBalance
{
    [ThreadStatic] internal static BattleActData? CalculatingAct;
}

[HarmonyPatch(typeof(BattleBasicFunction),"SetUpFunctions")]
internal static class PhysicalAttackScope
{
    static void Prefix(BattleBasicFunction __instance,ref BattleActData? __state)
    { __state=PhysicalAttackBalance.CalculatingAct;PhysicalAttackBalance.CalculatingAct=__instance.battleActData; }
    static void Finalizer(BattleActData? __state) => PhysicalAttackBalance.CalculatingAct=__state;
}

// Runs once on the newly calculated physical result, before display and HP
// application. Reading the result repeatedly never compounds either modifier.
[HarmonyPatch(typeof(FuncitonNormalAttack),"Calc")]
internal static class PhysicalAttackDamage
{
    static void Postfix(FuncitonNormalAttack __instance)
    {
        var act=PhysicalAttackBalance.CalculatingAct;
        if(act==null) return;
        bool rapid=RapidFire.Applies(act);
        bool barehand=Monk.Fists.TryGetValue(act.Pointer,out var strike) && strike.Barehand;
        if(!rapid && !barehand) return;
        var result=__instance.GetCalcResult();
        if(result!=null) result.SetValue(JobFeatures.PhysicalAttackDamage(result.GetValue(false),rapid,barehand));
    }
}
