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
        foreach(int flag in Jobs.UnlockFlags.Values.Concat(new[]{18,28,43,68}).Distinct())
            if(storage.Get(DataStorage.Category.kScenarioFlag1,flag)!=0) changed|=State.Campaign.StoryFlags.Add(flag);
        changed|=State.Campaign.ReconcileSelections(Plugin.TestAllJobs.Value);
        if(!changed) return;
        State.Changed();
        var added=Jobs.All.Where(j=>j.Id!="dark_knight" && !before.Contains(j.Id) && State.Campaign.IsUnlocked(j.Id)).Select(j=>j.Name).ToArray();
        if(added.Length>0) { State.Notice="Jobs unlocked: "+string.Join(", ",added); Plugin.Logger.LogInfo(State.Notice); }
    }
}
