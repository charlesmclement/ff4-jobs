using FF4Jobs.Core;
using HarmonyLib;
using Last.Battle;
using Last.Data.Master;
using NativeActs=Il2CppSystem.Collections.Generic.List<Last.Battle.BattleActData>;

namespace FF4Jobs;

internal static class RapidFire
{
    internal static bool Applies(BattleActData? act) => act?.Command?.Id==JobFeatures.RapidFire
        && State.Applies(act.AttackUnitData?.GetOwnedCharacterData());

    internal static void ChooseLivingEnemy(BattleActData act)
    {
        var units=BattlePlugManager.Instance()?.GetEnemyUnits();
        if(units==null) return;
        var alive=new List<BattleUnitData>();
        for(int i=0;i<units.Length;i++)
            if(units[i]!=null && !units[i].IsRemove && units[i].BattleUnitDataInfo?.Parameter?.CurrentHP>0)
                alive.Add(units[i]);
        if(alive.Count>0) act.ChangeTargetUnits(alive[UnityEngine.Random.Range(0,alive.Count)]);
    }
}

// The game's own queue handles animations, death, counters and completion. Four
// independent actions belong to one command; do not recursively expand them.
[HarmonyPatch(typeof(BattleActExection),nameof(BattleActExection.SetBattleActData),new[]{typeof(BattleActData)})]
internal static class RapidFireQueue
{
    [ThreadStatic] private static bool expanding;
    static bool Prefix(BattleActExection __instance,BattleActData battleActData)
    {
        if(expanding || !RapidFire.Applies(battleActData)) return true;
        var list=new NativeActs();
        for(int i=0;i<JobFeatures.RapidFireHits;i++)
        {
            // Fresh native action constructor: copy construction can share flags.
            var abilities=new Il2CppSystem.Collections.Generic.List<Ability>();
            abilities.Add(MasterManager.Instance.GetData<Ability>(1));
            list.Add(new BattleActData(battleActData.AttackUnitData,battleActData.TargetUnitDatas,
                battleActData.Command,abilities,battleActData.CurrentActType,
                battleActData.CurrentTargetType,battleActData.UseArmType));
        }
        expanding=true;
        try { __instance.SetBattleActData(list); }
        finally { expanding=false; }
        return false;
    }
}

[HarmonyPatch(typeof(Serial.FF4.Calc.FunctionProvider),nameof(Serial.FF4.Calc.FunctionProvider.CreateActFunction))]
internal static class RapidFireTarget
{
    static void Prefix(BattleActData battleActData)
    {
        if(RapidFire.Applies(battleActData)) RapidFire.ChooseLivingEnemy(battleActData);
    }
}

