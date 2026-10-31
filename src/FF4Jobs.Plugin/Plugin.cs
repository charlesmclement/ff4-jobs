using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using FF4Jobs.Core;
using Last.Data.User;
using Last.Management;
using Last.Battle;
using Last.Defaine.Master;
using UnityEngine;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FF4Jobs;

[BepInPlugin("ff4jobs.mainandsub", "FF4 Jobs — Main & Sub", "0.2.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static bool Enabled;
    internal static BepInEx.Configuration.ConfigEntry<bool> TestAllJobs = null!;
    internal static BepInEx.Configuration.ConfigEntry<bool> TestAllSkills = null!;
    internal static BepInEx.Configuration.ConfigEntry<bool> FastForwardEnabled = null!;
    internal static BepInEx.Configuration.ConfigEntry<float> FastForwardMultiplier = null!;
    internal static BepInEx.Configuration.ConfigEntry<bool> FastForwardLeftTrigger = null!;
    public override void Load()
    {
        Logger = Log;
        FastForwardEnabled = Config.Bind("Controls", "HoldTriggerFastForward", true,
            "Hold the selected controller trigger or F6 to fast-forward gameplay and confirm dialogue. Trigger+Start (F6+F7) opens native scene-skip confirmation where available.");
        FastForwardLeftTrigger = Config.Bind("Controls", "UseLeftTrigger", true,
            "Use LT/L2 for fast-forward. Set false to use RT/R2 instead. Y remains the menu button.");
        FastForwardMultiplier = Config.Bind("Controls", "FastForwardMultiplier", 4f,
            new BepInEx.Configuration.ConfigDescription("Gameplay speed while held. Menus and choices stay at normal speed.",
                new BepInEx.Configuration.AcceptableValueRange<float>(1f,16f)));
        TestAllJobs = Config.Bind("Testing", "AllJobsAvailable", false,
            "Temporarily expose every job without changing saved story unlocks. ABP requirements still apply.");
        TestAllSkills = Config.Bind("Testing", "AllSkillsAvailable", false,
            "Temporarily expose trained job commands/spells and subjobs without awarding ABP. Story summons remain gated.");
        // A changed executable requires re-validation; do not silently patch a new build.
        string game = Path.Combine(Paths.GameRootPath, "GameAssembly.dll");
        string expected = File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "gameassembly.sha256")).Trim();
        string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(game))).ToLowerInvariant();
        if (actual != expected) { Log.LogError("FF4 Jobs disabled: this game build has not been verified."); return; }
        var harmony = new Harmony("ff4jobs.mainandsub");
        try
        {
            harmony.PatchAll();
            AddComponent<JobWindow>();
            AddComponent<FastForward>();
            if(Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE")=="1") Application.runInBackground=true;
            Enabled = true;
            Log.LogInfo("FF4 Jobs prototype loaded. Select Jobs from the main menu.");
        }
        catch(Exception e)
        {
            harmony.UnpatchSelf();
            Enabled = false;
            Log.LogError("FF4 Jobs initialization failed; patches removed: " + e);
        }
    }
}

internal static class State
{
    internal static Campaign Campaign = new();
    internal static int Bypass;
    internal static int JobGroupScope;
    internal static bool RewardGiven;
    internal static int Generation;
    internal static string Notice = "Main jobs earn full ABP; sub jobs earn 25%.";
    internal static bool BrokenSave;
    internal const string SaveKey = "FF4Jobs_MainSub_v1";
    internal static SubSceneManagerMainGame.State? GamePhase => SceneManager.Instance?
        .GetCurrentSubSceneManager()?.TryCast<SubSceneManagerMainGame>()?.GetCurrentState();

    internal static void Reset()
    {
        CombatMenuTest.Reset();
        BattleCommandScroll.Clear();
        UnlockNotifications.Reset();
        Projection.Clear();
        Campaign = new Campaign(); Generation++; RewardGiven = false; BrokenSave = false;
    }
    internal static bool Applies(OwnedCharacterData? character)
    {
        if (!Plugin.Enabled || BrokenSave || Bypass > 0 || character == null) return false;
        int status = character.characterStatusId;
        if (status < 1 || status > 13) return false;
        var phase=GamePhase;
        if(phase is not (SubSceneManagerMainGame.State.Player or SubSceneManagerMainGame.State.Menu or SubSceneManagerMainGame.State.Battle)) return false;
        var battle = BattlePlugManager.Instance();
        if (battle != null && battle.IsBattle())
        {
            // Explicit story flags retain vanilla commands/stats. Campaign testing must
            // still audit story battles that aren't marked with these flags.
            foreach (var flag in new[]{BattleFlagType.EventBattle, BattleFlagType.EventBattleNoBackTitle,
                BattleFlagType.Auto, BattleFlagType.LoseEvent, BattleFlagType.NoResultNoSave, BattleFlagType.FinalBattle})
                if (battle.IsMatchBattleFlagTypes(flag)) return false;
        }
        if(CombatMenuTest.Applies(character)) return true;
        var p = Campaign.Character(status);
        if(!Campaign.IsAvailable(status,p.Main,Plugin.TestAllJobs.Value)
            || (p.Sub!=null && !Campaign.IsAvailable(status,p.Sub,Plugin.TestAllJobs.Value))) return false;
        return p.Main != "original" || p.Sub != null;
    }
    internal static void Changed() { Generation++; Projection.Clear(); }
    internal static string Serialize() { Campaign.Validate(); return JsonSerializer.Serialize(Campaign); }
    internal static void Load(string json)
    {
        Reset();
        try
        {
            var root = JsonNode.Parse(json);
            var node = root?[SaveKey];
            if (node == null) return;
            var candidate = node.Deserialize<Campaign>() ?? throw new InvalidDataException("Empty job save.");
            candidate.Validate(); Campaign = candidate;
            Plugin.Logger.LogInfo("Restored main/subjob progress from this save.");
        }
        catch(Exception e)
        {
            BrokenSave = true;
            Notice = "Job data could not be loaded. Reload a valid save before using jobs.";
            Plugin.Logger.LogError(Notice + " " + e);
        }
    }
}

[HarmonyPatch(typeof(UserDataManager), nameof(UserDataManager.Initialize))]
internal static class NewGamePatch { static void Postfix() => State.Reset(); }

[HarmonyPatch(typeof(UserDataManager), nameof(UserDataManager.FromJsonAsync))]
internal static class LoadPatch
{
    // Capture JSON before the asynchronous loader runs. The loader may initialize
    // its own data; suppress the reset hook for the synchronous entry only.
    static void Postfix(string json) => State.Load(json);
}

[HarmonyPatch(typeof(UserDataManager), nameof(UserDataManager.ToJSON))]
internal static class SavePatch
{
    static void Prefix(out int __state) { __state = State.Bypass; State.Bypass++; Projection.RestoreAll(); }
    static void Postfix(ref string __result)
    {
        if (State.BrokenSave) throw new InvalidOperationException("Refusing to overwrite unreadable job progress.");
        var root = JsonNode.Parse(__result) ?? throw new InvalidDataException("No save object.");
        root[State.SaveKey] = JsonNode.Parse(State.Serialize());
        __result = root.ToJsonString();
    }
    static void Finalizer(int __state) => State.Bypass = __state;
}
