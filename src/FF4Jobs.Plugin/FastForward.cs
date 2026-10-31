using FF4Jobs.Core;
using HarmonyLib;
using Last.Management;
using Last.Message;
using Last.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using SkipIcon = Last.UI.KeyInput.OpEdSkipIcon;
using Phase = Last.Management.SubSceneManagerMainGame.State;

namespace FF4Jobs;

// Selected trigger (LT/L2 by default), independent of
// menu remapping. F6/F7 provide the same controls for keyboard playtesting.
public sealed class FastForward : MonoBehaviour
{
    public FastForward(IntPtr pointer) : base(pointer) { }
    private static readonly FastForwardSpeed speed = new();
    private static bool failed, blockedUntilRelease, chordWasHeld;
    private static int sampledFrame = -1, advancedFrame = -1;
    private float probeAt;
    internal static bool Held, SkipPressed;
    internal static bool Enabled => Plugin.Enabled && Plugin.FastForwardEnabled.Value && !failed;

    internal static void SampleInput()
    {
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        var pad = Gamepad.current;
        var trigger = Plugin.FastForwardLeftTrigger.Value ? pad?.leftTrigger : pad?.rightTrigger;
        Held = Application.isFocused && (trigger?.isPressed == true || Input.GetKey(KeyCode.F6));
        bool chord = Held && (pad?.startButton?.isPressed == true || Input.GetKey(KeyCode.F7));
        SkipPressed = chord && !chordWasHeld;
        chordWasHeld = chord;
        if (!Held) blockedUntilRelease = false;
    }

    private static bool ModalOpen() => JobWindow.IsOpen
        || MessageSelectWindowManager.Instance?.IsOpen() == true
        || MessageChoiceWindowManager.Instance?.IsOpen() == true
        || MessageMultipleWindowManager.Instance?.IsOpen() == true
        || PopupManager.Instance?.Filter?.activeInHierarchy == true;

    internal static bool CanAccelerate()
    {
        SampleInput();
        if (!Enabled || !Held) return false;
        if (ModalOpen()) { blockedUntilRelease = true; return false; }
        if (blockedUntilRelease) return false;
        return State.GamePhase is Phase.Player or Phase.Message or Phase.Event or Phase.Battle;
    }

    public void Update()
    {
        try
        {
            bool active = CanAccelerate();
            Time.timeScale = speed.Update(Time.timeScale, active, Plugin.FastForwardMultiplier.Value);
            if (Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE") == "1" && Time.unscaledTime >= probeAt)
            {
                probeAt = Time.unscaledTime + .25f;
                string probe = Path.Combine(BepInEx.Paths.ConfigPath, "ff4jobs-speed-probe");
                if (File.Exists(probe + ".request"))
                {
                    var message = MessageWindowManager.Instance;
                    var bindings = new Dictionary<string,string>();
                    var input = Il2CppSystem.Input.InputSystemManager.Instance;
                    if (input != null) foreach (var entry in input.KeyConfigSettings) bindings[entry.Key] = entry.Value.ToString();
                    File.WriteAllText(probe + ".json", System.Text.Json.JsonSerializer.Serialize(new {
                        phase = State.GamePhase?.ToString(), scale = Time.timeScale, active, Held, blockedUntilRelease,
                        pad = Gamepad.current?.name, message = message?.stateMachine?.Current.ToString(),
                        field = message?.isFieldWindow, open = message?.IsOpen(), modal = ModalOpen(),
                        bindings, advancedFrame, frame = Time.frameCount
                    }));
                }
            }
        }
        catch (Exception e) { Fail(e); }
    }

    internal static void Restore() => Time.timeScale = speed.Update(Time.timeScale, false, 1);
    public void OnDisable() => Restore();
    public void OnDestroy() => Restore();
    public void OnApplicationFocus(bool focused) { if (!focused) Restore(); }

    internal static void Fail(Exception e)
    {
        failed = true;
        Restore();
        Plugin.Logger.LogError("Fast-forward disabled; normal controls restored: " + e);
    }

    internal static bool Advance(MessageWindowManager manager)
    {
        if (!CanAccelerate() || !manager.isFieldWindow || !manager.IsOpen()) return false;
        var state = manager.stateMachine?.Current;
        if (state is not (MessageWindowManager.State.Playing or MessageWindowManager.State.NewPage
            or MessageWindowManager.State.NewPageInputWait or MessageWindowManager.State.PressInputWait
            or MessageWindowManager.State.EndWait)) return false;
        // InputAction can be called twice in a frame. Only invoke the native
        // dialogue callback once; never synthesize the global confirm button.
        if (advancedFrame != Time.frameCount)
        {
            advancedFrame = Time.frameCount;
            // The final page has no onInputAction: native confirmation calls
            // onFinishAction instead. Omitting it strands dialogue at EndWait.
            if (state == MessageWindowManager.State.EndWait) manager.onFinishAction?.Invoke();
            else manager.ExecutionInputAction();
        }
        return true;
    }
}

[HarmonyPatch(typeof(MessageWindowManager), "Update")]
internal static class FastForwardDialogueUpdatePatch
{
    static void Postfix(MessageWindowManager __instance)
    {
        try { FastForward.Advance(__instance); }
        catch (Exception e) { FastForward.Fail(e); }
    }
}

[HarmonyPatch(typeof(MessageWindowManager), "InputAction")]
internal static class FastForwardDialoguePatch
{
    static bool Prefix(MessageWindowManager __instance)
    {
        try { return !FastForward.Advance(__instance); }
        catch (Exception e) { FastForward.Fail(e); return true; }
    }
}

// Restore before transitions into menus, loading, or other game phases.
[HarmonyPatch(typeof(SubSceneManagerMainGame), nameof(SubSceneManagerMainGame.ChangeState))]
internal static class FastForwardPhasePatch
{
    static void Prefix() => FastForward.Restore();
}

[HarmonyPatch(typeof(SkipIcon), "Update")]
internal static class FastForwardSkipPatch
{
    static bool Prefix(SkipIcon __instance)
    {
        try
        {
            if (!FastForward.Enabled) return true;
            FastForward.SampleInput();
            var machine = __instance.stateMachine;
            if (FastForward.SkipPressed && machine != null
                && machine.Current is SkipIcon.State.Hidden or SkipIcon.State.Showing or SkipIcon.State.FadeOut
                && Il2CppSystem.Fade.FadeManager.Instance?.IsStateFadeOut() != true)
            {
                FastForward.Restore();
                // Native confirmation owns pause/resume, BGM fading and the
                // script's authored skip destination. Never jump script flags.
                machine.Change(SkipIcon.State.ShowingPopup);
                return false;
            }
        }
        catch (Exception e) { FastForward.Fail(e); }
        return true;
    }
}

[HarmonyPatch(typeof(SkipIcon), "UpdateShowing")]
internal static class FastForwardSkipSingleButtonPatch
{
    static bool Prefix(SkipIcon __instance)
    {
        if (!FastForward.Enabled) return true;
        // Replace the original one-button skip shortcut while preserving its
        // five-second icon timeout. The chord above opens the same native UI.
        __instance.showingTimer += Time.unscaledDeltaTime;
        if (__instance.showingTimer >= 5) __instance.stateMachine?.Change(SkipIcon.State.FadeOut);
        return false;
    }
}
