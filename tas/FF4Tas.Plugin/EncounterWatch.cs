using HarmonyLib;
using Last.Map;
using UnityEngine;

namespace FF4Tas;

// Read-only random-encounter observer (research; changes no game value).
// Verified in this build's GameAssembly.dll (docs/TAS-ENCOUNTERS.md):
//   EncounterLot.Shuffle: subtractSteps = Random.Range(min, max+1) (the map's
//     required_steps_min/max), moveCounter = 0. Called on map setup and by
//     FieldController.EncountShuffle.
//   EncounterLot.CheckEncount (before each step): moveCounter += the terrain's
//     SubtrackStep (6 everywhere in FF4's foot_information, 1 without terrain
//     info); when subtractSteps - moveCounter <= 0 the party is drawn with
//     Random.Range(1, 1001) against the monster set's per-mille rates.
//   UnityEngine.Random is reseeded with DateTime.Now.Millisecond on field
//     setup, by EncounterLot's constructor and by every camera shake.
//   SaveMapData stores moveCount/subtractSteps; SetRestoreData puts them back.
// Logs encounters.jsonl (shuffle/restore/encounter/seed events) and rng.jsonl
// (every Random.InitState and Random.RandomRangeInt call) in the TAS session
// folder, and shows "next battle in N steps" on the overlay.
internal static class EncounterWatch
{
    private const int StepValue=6,RngLogCap=200000;
    internal static EncounterLot? Lot;
    private static int rngLines;
    // "battle in N steps (counter c/threshold t, range a-b)" for the overlay and status.
    internal static string Status()
    {
        var lot=Lot;
        try
        {
            if(lot==null || lot.Pointer==IntPtr.Zero)return "";
            int threshold=lot.subtractSteps,counter=lot.moveCounter;var range=lot.stepsRange;
            if(range.Max==0)return "no random encounters here";
            int left=Math.Max(0,(threshold-counter+StepValue-1)/StepValue);
            return $"battle in {left} steps ({counter}/{threshold}, range {range.Min}-{range.Max}){(lot.Enable?"":" [off]")}";
        }
        catch {return "";}
    }
    internal static void Event(string kind,EncounterLot? lot,string extra="")
    {
        try
        {
            var s=Controller.Current;
            string lotPart="";
            if(lot!=null && lot.Pointer!=IntPtr.Zero)
            {var r=lot.stepsRange;lotPart=$",\"threshold\":{lot.subtractSteps},\"counter\":{lot.moveCounter},\"min\":{r.Min},\"max\":{r.Max}";}
            Append("encounters.jsonl",$"{{\"kind\":\"{kind}\",\"frame\":{Time.frameCount},\"ms\":{DateTime.Now.Millisecond},\"utc\":\"{DateTime.UtcNow:O}\",\"map\":{s.MapId},\"x\":{Num(s.X)},\"y\":{Num(s.Y)},\"phase\":\"{s.Phase}\"{lotPart}{extra}}}");
        }
        catch {}
    }
    internal static void Rng(string kind,string body)
    {
        if(rngLines>=RngLogCap)return;
        rngLines++;
        try {var s=Controller.Current;Append("rng.jsonl",$"{{\"kind\":\"{kind}\",\"frame\":{Time.frameCount},\"map\":{s.MapId},\"phase\":\"{s.Phase}\",{body}}}");} catch {}
    }
    // Installed one by one, outside PatchAll: a hook that fails to install is
    // logged and skipped, never taking the scripted run down with it.
    internal static void Install(Harmony harmony,BepInEx.Logging.ManualLogSource log)
    {
        void Hook(Type type,string method,Type patch)
        {
            try {harmony.Patch(AccessTools.Method(type,method),postfix:new HarmonyMethod(AccessTools.Method(patch,"Postfix")));}
            catch(Exception e) {log.LogWarning($"Encounter watch: {type.Name}.{method} not hooked: {e.Message}");}
        }
        Hook(typeof(EncounterLot),nameof(EncounterLot.Shuffle),typeof(WatchShuffle));
        Hook(typeof(EncounterLot),nameof(EncounterLot.SetRestoreData),typeof(WatchRestore));
        Hook(typeof(EncounterLot),nameof(EncounterLot.CheckEncount),typeof(WatchCheck));
        Hook(typeof(UnityEngine.Random),nameof(UnityEngine.Random.InitState),typeof(WatchSeed));
        Hook(typeof(UnityEngine.Random),nameof(UnityEngine.Random.RandomRangeInt),typeof(WatchRange));
    }
    private static string Num(float? v)=>v.HasValue?v.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture):"null";
    private static void Append(string file,string line)=>File.AppendAllText(Path.Combine(Plugin.DirectoryPath,file),line+"\n");
}

internal static class WatchShuffle
{
    static void Postfix(EncounterLot __instance) {EncounterWatch.Lot=__instance;EncounterWatch.Event("shuffle",__instance);}
}
internal static class WatchRestore
{
    static void Postfix(EncounterLot __instance) {EncounterWatch.Lot=__instance;EncounterWatch.Event("restore",__instance);}
}
internal static class WatchCheck
{
    static void Postfix(EncounterLot __instance,bool __result)
    {
        EncounterWatch.Lot=__instance;
        if(!__result)return;
        int party=-1;
        try {party=__instance.encountInfo?.Table?.MonsterSetId??-1;} catch {}
        EncounterWatch.Event("encounter",__instance,$",\"monsterSet\":{party}");
    }
}
internal static class WatchSeed
{
    static void Postfix(int seed) {EncounterWatch.Rng("seed",$"\"seed\":{seed}");}
}
internal static class WatchRange
{
    static void Postfix(int min,int max,int __result) {EncounterWatch.Rng("range",$"\"min\":{min},\"max\":{max},\"result\":{__result}");}
}
