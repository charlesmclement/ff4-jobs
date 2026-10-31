using BepInEx;
using System.Text.Json;
using UnityEngine;

namespace FF4Jobs;

// Opt-in automated loader check. Exits before opening/loading any campaign.
// Normal desktop/Steam launches never set this variable.
internal static class StartupSmoke
{
    internal static void Report(bool passed,int patchedMethods,string? error)
    {
        if(Environment.GetEnvironmentVariable("FF4JOBS_STARTUP_SMOKE")!="1") return;
        string file=Path.Combine(Paths.ConfigPath,"ff4jobs-startup-smoke.json");
        File.WriteAllText(file,JsonSerializer.Serialize(new {
            version=typeof(Plugin).Assembly.GetName().Version?.ToString(),
            passed,patchedMethods,error,allJobs=Plugin.TestAllJobs.Value,allSkills=Plugin.TestAllSkills.Value,
            scope="Loader, Harmony patches and component registration only; no save loaded or battle run."
        },new JsonSerializerOptions{WriteIndented=true}));
        Application.Quit();
    }
}
