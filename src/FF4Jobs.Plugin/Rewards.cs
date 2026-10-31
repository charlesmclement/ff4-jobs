using HarmonyLib;
using Last.Battle;
using Last.Data;
using Last.Systems;

namespace FF4Jobs;

[HarmonyPatch(typeof(BattlePlugManager),nameof(BattlePlugManager.Start))]
internal static class BattleStartPatch
{
    static void Prefix() { State.RewardGiven=false; Projection.RestoreAll(); }
    static void Postfix() => Projection.Sync();
}
[HarmonyPatch(typeof(BattleResultProvider),nameof(BattleResultProvider.Genelate))]
internal static class RewardPatch
{
    static void Postfix(BattleResultData __result)
    {
        if(!Plugin.Enabled || State.RewardGiven || State.BrokenSave || __result==null) return;
        State.RewardGiven=true;
        // Prototype reward curve. No rewards from escapes or scripted no-result battles.
        int reward=Core.Balance.BattleAbp(__result.GetExp);
        var rewarded=new HashSet<string>(); int count=0;
        for(int i=0;i<__result.CharacterList.Count;i++)
        {
            var c=__result.CharacterList[i].AfterData;
            if(!State.Applies(c) || c.Parameter.CurrentHP<=0) continue;
            string key=Core.Jobs.CharacterKey(c.characterStatusId);
            if(rewarded.Add(key) && State.Campaign.Award(c.characterStatusId,reward)>0) count++;
        }
        if(count>0) { State.Changed(); State.Notice=$"Battle won: +{reward} ABP to {count} main job(s)."; Plugin.Logger.LogInfo(State.Notice); }
    }
}
