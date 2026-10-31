using HarmonyLib;
using Last.Data;
using Last.Defaine;
using Last.UI.KeyInput;

namespace FF4Jobs;

// Add to the real command data so native layout, pointer and keyboard navigation
// treat Jobs just like Items/Magic/Equip. Never detour the shared field setters.
[HarmonyPatch(typeof(Last.UI.CommandMenuController),nameof(Last.UI.CommandMenuController.Initalize))]
internal static class AddJobMenu
{
    static void Prefix(ref Il2CppSystem.Collections.Generic.List<CommandMenuContentData> data)
    {
        if(!Plugin.Enabled) return;
        // Clone only the rendered command list. FF4's submenu factory must retain
        // its original data: it has no built-in job-window prefab to instantiate.
        var entries=new Il2CppSystem.Collections.Generic.List<CommandMenuContentData>();
        for(int i=0;i<data.Count;i++)
        {
            if(data[i].Id==MenuCommandId.Job) return;
            entries.Add(data[i]);
        }
        int position=entries.Count;
        for(int i=0;i<entries.Count;i++) if(entries[i].Id==MenuCommandId.Equipment) { position=i+1; break; }
        entries.Insert(position,new CommandMenuContentData(MenuCommandId.Job,"Jobs"));
        data=entries;
    }
}

[HarmonyPatch(typeof(MainMenuController),nameof(MainMenuController.InitJob))]
internal static class EnterJobs
{
    static bool Prefix()
    {
        if(!Plugin.Enabled) return true;
        JobWindow.RequestOpen=true;
        return false;
    }
}

[HarmonyPatch(typeof(MainMenuController),nameof(MainMenuController.UpdateJob))]
internal static class UpdateJobs
{
    static bool Prefix() => !Plugin.Enabled;
}

[HarmonyPatch(typeof(Last.UI.CommandMenuContentView),nameof(Last.UI.CommandMenuContentView.SetEnable))]
internal static class EnableJobs
{
    static void Prefix(Last.UI.CommandMenuContentView __instance,ref bool isEnable)
    {
        if(Plugin.Enabled && !State.BrokenSave && __instance.Data?.Id==MenuCommandId.Job) isEnable=true;
    }
}
