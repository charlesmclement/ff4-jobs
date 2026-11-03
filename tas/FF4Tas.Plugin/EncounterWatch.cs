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
    internal static BattleDb Db=new();
    // Player world position (Navigation sets it) for the encounter table lookup.
    internal static Vector3? PlayerPos;
    // Monsters the route calls really dangerous (Route.Rng.AvoidMonsters).
    internal static HashSet<int> Avoid=new();
    // The next battle as the field stands: walking draws nothing, so the
    // group roll is the next Random.Range(1,1001) from the mirrored generator,
    // against the monster set where the player stands now.
    internal struct Forecast
    {
        internal bool Known,HasEncounters,Dangerous;
        internal int StepsLeft,Roll,Set,Party;
        internal int[] Monsters;
    }
    internal static Forecast Next()
    {
        var f=new Forecast{Monsters=Array.Empty<int>()};
        var lot=Lot;
        try
        {
            if(lot==null || lot.Pointer==IntPtr.Zero)return f;
            var range=lot.stepsRange;f.Known=true;f.HasEncounters=range.Max>0 && lot.Enable;
            f.StepsLeft=Math.Max(0,(lot.subtractSteps-lot.moveCounter+StepValue-1)/StepValue);
            if(!f.HasEncounters || !FF4Tas.Rng.Synced)return f;
            f.Roll=FF4Tas.Rng.Peek(1,1001);f.Set=CurrentSet(lot);
            f.Party=Db.Group(f.Set,f.Roll);
            if(f.Party!=0 && Db.Parties.TryGetValue(f.Party,out var ms))f.Monsters=ms;
            foreach(int m in f.Monsters)if(Avoid.Contains(m))f.Dangerous=true;
        }
        catch {}
        return f;
    }
    // The game's own table for the player's cell (overworld areas differ),
    // else the map's monster set (dungeons).
    private static int CurrentSet(EncounterLot lot)
    {
        try
        {
            if(PlayerPos is Vector3 pos && lot.mapHander!=null)
            {
                int id=lot.mapHander.GetEncountInfo(pos,false)?.Table?.MonsterSetId??0;
                if(id!=0)return id;
            }
        }
        catch {}
        return Db.MapSets.TryGetValue(Controller.Current.MapId,out int set)?set:0;
    }
    // "battle in N steps (counter c/threshold t, range a-b); next: group" for the overlay and status.
    internal static string Status()
    {
        var lot=Lot;
        try
        {
            if(lot==null || lot.Pointer==IntPtr.Zero)return "";
            int threshold=lot.subtractSteps,counter=lot.moveCounter;var range=lot.stepsRange;
            if(range.Max==0)return "no random encounters here";
            var f=Next();
            string group=!FF4Tas.Rng.Synced?"next: unknown (RNG not synced)":f.Party==0?$"next: roll {f.Roll}":
                "next: "+string.Join(", ",f.Monsters.Select(Db.MonsterName))+(f.Dangerous?" [AVOID]":"");
            return $"battle in {f.StepsLeft} steps ({counter}/{threshold}, range {range.Min}-{range.Max}){(lot.Enable?"":" [off]")}; {group}";
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
    internal static void LogRng(string kind,string body)
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
        // The only float draw in this build (no Random.value): keeps the mirror in step.
        try {harmony.Patch(AccessTools.Method(typeof(UnityEngine.Random),nameof(UnityEngine.Random.Range),new[]{typeof(float),typeof(float)}),postfix:new HarmonyMethod(AccessTools.Method(typeof(WatchFloat),"Postfix")));}
        catch(Exception e) {log.LogWarning($"Encounter watch: Random.Range(float) not hooked: {e.Message}");}
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
    static void Postfix(int seed) {FF4Tas.Rng.Seed(seed);EncounterWatch.LogRng("seed",$"\"seed\":{seed}");}
}
internal static class WatchRange
{
    static void Postfix(int min,int max,int __result) {FF4Tas.Rng.Int(min,max,__result);EncounterWatch.LogRng("range",$"\"min\":{min},\"max\":{max},\"result\":{__result}");}
}
internal static class WatchFloat
{
    static void Postfix(float min,float max,float __result) {FF4Tas.Rng.Float();EncounterWatch.LogRng("float",$"\"result\":{__result.ToString(System.Globalization.CultureInfo.InvariantCulture)}");}
}
