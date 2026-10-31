using BepInEx;
using FF4Jobs.Core;
using Last.Data.User;
using Last.Management;

namespace FF4Jobs;

// Explicit local playtest only. Projection restores native lists for saving;
// this switch and its extra skills never enter the campaign/save data.
internal static class CombatMenuTest
{
    private static bool enabled;
    internal static bool Applies(OwnedCharacterData c) => enabled && c.characterStatusId is 1 or 13;
    internal static void Reset() => enabled = false;
    internal static IEnumerable<int> Commands => Jobs.All.SelectMany(j=>j.Commands).Where(id=>id!=9).Distinct();
    internal static void Poll()
    {
        if(Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE")!="1"
            || State.GamePhase!=SubSceneManagerMainGame.State.Player) return;
        string path=Path.Combine(Paths.ConfigPath,"ff4jobs-combat-menu-test.request");
        if(!File.Exists(path)) return;
        bool activate=File.ReadAllText(path).Trim()!="off";
        File.Delete(path);
        enabled=activate;
        State.Changed(); Projection.Sync();
        State.Notice=enabled ? "TEST: Cecil has expanded commands and spells until reload." : "Combat menu test disabled.";
        Plugin.Logger.LogInfo(State.Notice);
        File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-combat-menu-test.txt"),State.Notice);
    }
}
