using Last.Interpreter;
using Last.Management;
using FF4Jobs.Core;

namespace FF4Jobs;
internal static class StoryProgression
{
    internal static void Sync()
    {
        if(State.BrokenSave || State.GamePhase is not (SubSceneManagerMainGame.State.Player or SubSceneManagerMainGame.State.Menu)) return;
        var storage=DataStorage.Instance; if(storage==null) return;
        var before=Jobs.All.Where(j=>State.Campaign.IsUnlocked(j.Id)).Select(j=>j.Id).ToHashSet();
        bool changed=false;
        // Track native acquisitions, never projected/test spell lists. Summons
        // are story knowledge shared by the party; ABP remains per character.
        var data=UserDataManager.Instance();
        if(data?.OwnedCharacterList!=null)
            foreach(var c in data.OwnedCharacterList)
            {
                // Include temporarily absent recruits when upgrading an old save.
                if(c==null || c.characterStatusId is <1 or >13) continue;
                foreach(var ability in Projection.OriginalAbilities(c))
                    if(ability.Ability.Id is >=61 and <=75 && ability.Ability.AbilityGroupId==3)
                        changed|=State.Campaign.LearnedSummons.Add(ability.Ability.Id);
            }
        foreach(int flag in Jobs.UnlockFlags.Values.Concat(new[]{18,28,43,68}).Distinct())
            if(storage.Get(DataStorage.Category.kScenarioFlag1,flag)!=0) changed|=State.Campaign.StoryFlags.Add(flag);
        changed|=State.Campaign.ReconcileSelections(Plugin.TestAllJobs.Value);
        if(!changed) return;
        State.Changed();
        var added=Jobs.All.Where(j=>j.Id!="dark_knight" && !before.Contains(j.Id) && State.Campaign.IsUnlocked(j.Id)).Select(j=>j.Name).ToArray();
        if(added.Length>0) { State.Notice="Jobs unlocked: "+string.Join(", ",added); Plugin.Logger.LogInfo(State.Notice); }
    }
}
