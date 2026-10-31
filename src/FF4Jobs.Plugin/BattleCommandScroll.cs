using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.Master;
using Last.UI.KeyInput;
using NativeList=Il2CppSystem.Collections.Generic.List<Last.Data.Master.Command>;

namespace FF4Jobs;

// Virtualize the native rows; retain native rendering, command validation,
// click/confirm handling, target selection and the Row/Defend side windows.
internal static class BattleCommandScroll
{
    internal sealed class Window
    {
        internal List<Command> All=new();
        internal int Start;
        internal int Rows;
        internal CommandScrollArrows Arrows=new();
    }
    private static readonly Dictionary<IntPtr,Window> windows=new();
    internal static BattleCommandSelectController? InputOwner;
    internal static void Reset(BattleCommandSelectController c)
    {
        if(windows.Remove(c.Pointer,out var w)) w.Arrows.Destroy();
    }
    internal static void Clear()
    {
        foreach(var w in windows.Values) w.Arrows.Destroy();
        windows.Clear(); InputOwner=null;
    }
    internal static void UpdateArrows(BattleCommandSelectController c)
    {
        if(!windows.TryGetValue(c.Pointer,out var w)) return;
        if(c.stateMachine!=null && (int)c.stateMachine.Current==2)
            w.Arrows.Update(c,w.Start>0,w.Start+w.Rows<w.All.Count);
        else w.Arrows.Hide();
    }
    internal static void Prepare(BattleCommandSelectController c)
    {
        if(!Plugin.Enabled || !State.Applies(c.targetData)
            || c.normalList==null || c.contentList==null || c.selectCursor==null) return;
        if(!windows.TryGetValue(c.Pointer,out var w))
        {
            w=new Window();
            // SetCommandData assigns CommandUtility.GetReleaseCommand's native
            // List<Command>. Avoid generic interface enumerator invocation across
            // the IL2CPP boundary; read that concrete list by index instead.
            var commands=c.normalList.TryCast<NativeList>();
            if(commands==null)
            {
                Plugin.Logger.LogWarning("Command scrolling skipped: unexpected native list type.");
                return;
            }
            for(int i=0;i<commands.Count;i++) w.All.Add(commands[i]);
            w.Rows=Math.Min(5,c.contentList.Count);
            if(w.All.Count<=w.Rows || w.Rows==0) return;
            windows[c.Pointer]=w;
            int selected=Math.Clamp(c.selectCursor.Index,0,w.All.Count-1);
            w.Start=Math.Max(0,selected-w.Rows+1);
            c.selectCursor.Index=selected-w.Start;
            Plugin.Logger.LogInfo($"Native command scrolling: {w.All.Count} commands, {w.Rows} visible rows.");
        }
        var page=new NativeList();
        foreach(var command in w.All.Skip(w.Start).Take(w.Rows)) page.Add(command);
        c.normalList=page.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Command>>();
    }
    internal static bool Move(Last.UI.Cursor cursor, Il2CppSystem.Action<int> action, int direction, bool loop)
    {
        var c=InputOwner;
        if(c==null || c.selectCursor==null || c.selectCursor.Pointer!=cursor.Pointer
            || !windows.TryGetValue(c.Pointer,out var w)) return true;
        var next=CommandScroll.Move(w.Start,cursor.Index,w.All.Count,w.Rows,direction,loop);
        bool shifted=next.Start!=w.Start;
        w.Start=next.Start; cursor.Index=next.Row;
        if(shifted) c.NormalInit(); // Native row rendering and availability checks.
        action?.Invoke(next.Row); // Native cursor placement callback.
        return false;
    }

}

[HarmonyPatch(typeof(BattleCommandSelectController),"SetCommandData")]
internal static class BattleCommandScrollReset
{
    static void Prefix(BattleCommandSelectController __instance) => BattleCommandScroll.Reset(__instance);
}
[HarmonyPatch(typeof(BattleCommandSelectController),"NormalInit")]
internal static class BattleCommandScrollPrepare
{
    static void Prefix(BattleCommandSelectController __instance)
    {
        try { BattleCommandScroll.Prepare(__instance); }
        catch(Exception e) { Plugin.Logger.LogError("Native command scroll setup failed: "+e); }
    }
}
[HarmonyPatch(typeof(BattleCommandSelectController),"UpdateController")]
internal static class BattleCommandScrollInputScope
{
    static void Prefix(BattleCommandSelectController __instance, out BattleCommandSelectController? __state)
    {
        __state=BattleCommandScroll.InputOwner;
        BattleCommandScroll.InputOwner=__instance.stateMachine!=null && (int)__instance.stateMachine.Current==2 ? __instance : null;
    }
    static void Finalizer(BattleCommandSelectController? __state) => BattleCommandScroll.InputOwner=__state;
    static void Postfix(BattleCommandSelectController __instance) => BattleCommandScroll.UpdateArrows(__instance);
}
[HarmonyPatch(typeof(Last.UI.Cursor),"NextIndex")]
internal static class BattleCommandScrollDown
{
    static bool Prefix(Last.UI.Cursor __instance, Il2CppSystem.Action<int> action, bool isLoop)
        => BattleCommandScroll.Move(__instance,action,1,isLoop);
}
[HarmonyPatch(typeof(Last.UI.Cursor),"PrevIndex")]
internal static class BattleCommandScrollUp
{
    static bool Prefix(Last.UI.Cursor __instance, Il2CppSystem.Action<int> action, bool isLoop)
        => BattleCommandScroll.Move(__instance,action,-1,isLoop);
}
