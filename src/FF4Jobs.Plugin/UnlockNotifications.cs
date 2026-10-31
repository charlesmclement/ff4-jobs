using FF4Jobs.Core;
using HarmonyLib;
using Last.Management;
using Last.UI;
using Last.UI.KeyInput;
using UnityEngine;
using NativeInput=Il2CppSystem.Input.InputSystemManager;
using ActionType=Il2CppSystem.Input.InputSystemManager.InputActionType;

namespace FF4Jobs;

// Use FF4's own modal popup and block field input while it owns the controls.
internal static class UnlockNotifications
{
    private static CommonPopup? popup;
    private static string[] pending=Array.Empty<string>();
    private static Campaign? owner;
    private static LearningNotice? pendingLearning;
    private static bool failed;
    private static float readyAt, pollAt;
    private static readonly Il2CppSystem.Action acknowledge=(System.Action)Acknowledge;

    internal static void Reset()
    {
        Close(); failed=false; pollAt=0;
    }
    private static void Close()
    {
        var previous=popup; popup=null; pending=Array.Empty<string>(); pendingLearning=null; owner=null;
        if(previous!=null && previous.IsOpen) previous.Close();
    }
    private static void Acknowledge()
    {
        if(owner==State.Campaign)
        {
            owner.AnnouncedJobs.UnionWith(pending);
            if(pendingLearning!=null) owner.Learning.Pending.Remove(pendingLearning);
        }
        Close(); pollAt=Time.unscaledTime+.3f;
    }
    internal static bool UpdateField()
    {
        if(!Plugin.Enabled || State.BrokenSave || failed) return true;
        try
        {
            if(popup!=null)
            {
                if(owner!=State.Campaign || State.GamePhase!=SubSceneManagerMainGame.State.Player) { Close(); return true; }
                if(!popup.IsOpen) { Close(); return false; }
                if(Time.unscaledTime>=readyAt)
                {
                    // B dismisses the single-button dialog as well as A.
                    if(MenuInput.Down(ActionType.Cancel) || MenuInput.Down(ActionType.Enter)) Acknowledge();
                    else popup.UpdateCommand();
                }
                return false;
            }
            if(State.GamePhase!=SubSceneManagerMainGame.State.Player || JobWindow.IsOpen || Time.unscaledTime<pollAt) return true;
            pollAt=Time.unscaledTime+.5f;
            StoryProgression.Sync();
            SkillUnlockNotifications.Collect();
            var ids=State.Campaign.PendingAnnouncements();
            if(ids.Length==0 && State.Campaign.Learning.Pending.Count==0) return true;
            var manager=PopupManager.Instance; if(manager==null) return true;
            var window=manager.GetOrCreate<CommonPopup>();
            if(window==null || window.IsOpen) return true;
            // One dialog per milestone, including catch-up when loading an older mod save.
            owner=State.Campaign; popup=window;
            if(ids.Length>0)
            {
                int flag=Jobs.UnlockFlags[ids[0]];
                pending=ids.Where(id=>Jobs.UnlockFlags[id]==flag).ToArray();
                window.Title=pending.Length==1?"Job unlocked!":"Jobs unlocked!";
                window.Message=string.Join(" / ",pending.Select(id=>Jobs.Get(id).Name))
                    +"\nAvailable as main jobs and sub jobs.\nMain jobs earn full ABP; sub jobs earn 25%.";
            }
            else
            {
                pendingLearning=owner.Learning.Pending[0];
                window.Title=pendingLearning.Title;
                window.Message=pendingLearning.Message;
            }
            window.SetEnableImageText(false);
            window.OnNegativeAction=acknowledge;
            window.SetPositiveCommand("OK",acknowledge,true);
            window.SetEnablePostiveButton(true);
            window.Open();
            window.SetEnableNegativeButton(false);
            window.ResetCursor(); window.UpdateSelect();
            readyAt=Time.unscaledTime+.25f;
            Plugin.Logger.LogInfo("Unlock dialog: "+string.Join(", ",pending));
            return false;
        }
        catch(Exception e)
        {
            failed=true;
            try { Close(); } catch { }
            Plugin.Logger.LogError("Unlock dialog disabled for this load; field controls restored: "+e);
            return true;
        }
    }
}

[HarmonyPatch(typeof(FieldMap),nameof(FieldMap.UpdatePlayerStatePlay))]
internal static class UnlockDialogFieldPatch
{
    static bool Prefix() => UnlockNotifications.UpdateField();
}
