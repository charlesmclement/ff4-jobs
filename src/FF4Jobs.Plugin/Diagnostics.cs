using BepInEx;
using Last.Battle;
using Last.Management;
using Last.Systems;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FF4Jobs;

// Read-only diagnostics for the isolated playtest. No progression/scene cheats.
internal static class Diagnostics
{
    private static readonly bool enabled = Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE") == "1";
    internal static void Poll()
    {
        if(!enabled) return;
        string request=Path.Combine(Paths.ConfigPath,"ff4jobs-inspect.request");
        if(!File.Exists(request)) return;
        File.Delete(request);
        var output=new Dictionary<string,object?>();
        try
        {
            var tracker=GameStateTracker.Instance;
            output["enabled"]=Plugin.Enabled;
            output["lastSteal"]=StealBalance.LastAttempt;
            output["testAllJobs"]=Plugin.TestAllJobs.Value;
            output["testAllSkills"]=Plugin.TestAllSkills.Value;
            output["state"]=tracker?.CurrentState.ToString();
            output["substate"]=tracker?.CurrentSubState.ToString();
            output["phase"]=State.GamePhase?.ToString();
            var menu=UnityEngine.Object.FindObjectOfType<Last.UI.KeyInput.MainMenuController>();
            output["menuOpen"]=menu?.IsOpne;
            output["menuState"]=menu?.stateMachine?.Current.ToString();
            output["notice"]=State.Notice;
            output["jobs"]=State.Campaign;
            var members=new List<object>(); var data=UserDataManager.Instance();
            if(data!=null)
            {
                var ids=data.GetMemberIndexList();
                for(int i=0;i<ids.Count;i++)
                {
                    var c=data.GetMemberData(ids[i]); if(c==null) continue;
                    var commands=c.CommandList; var abilities=c.OwnedAbilityList;
                    var equipment=new List<object>();
                    foreach(var e in c.EquipmentData) equipment.Add(new{slot=e.Key.ToString(),content=e.Value?.ContentId,name=e.Value?.Name,allowed=e.Value==null || EquipUtility.CanEquipped(e.Value,c.JobId)});
                    members.Add(new { index=ids[i],id=c.Id,status=c.characterStatusId,name=c.Name,nativeJob=c.JobId,
                        equipment,
                        active=State.Applies(c),hp=c.Parameter.CurrentHP,mp=c.Parameter.CurrentMP,maxMp=c.Parameter.ConfirmedMaxMp(),
                        commands=Enumerable.Range(0,commands.Count).Select(j=>new{id=commands[j].Id,available=AbilityUtility.CanUseCommand(commands[j],c,false)}).ToArray(),
                        abilities=Enumerable.Range(0,abilities.Count).Select(j=>abilities[j].Ability.Id).ToArray() });
                }
                if(tracker?.CurrentState==GameStates.InGame)
                {
                    string save;
                    try { save=data.ToJSON(false); }
                    finally { Projection.Sync(); }
                    output["serializedJobData"]=JsonNode.Parse(save)?[State.SaveKey];
                    // Keep the real serialized snapshot local to the test installation.
                    File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-inspect-save.json"),save);
                }
            }
            output["party"]=members;
            var battle=BattlePlugManager.Instance();
            output["inBattle"]=battle?.IsBattle();
            if(battle?.IsBattle()==true)
            {
                output["battleFlags"]=battle.BattleFlagTypes.ToArray().Select(f=>f.ToString()).ToArray();
                var units=battle.GetPlayerUnits();
                output["battleParty"]=units==null?null:units.Where(u=>u!=null).Select(u=>u.GetOwnedCharacterData()).Where(c=>c!=null)
                    .Select(c=>new {c.Name,status=c.characterStatusId,hp=c.Parameter.CurrentHP,
                        commands=Enumerable.Range(0,c.CommandList.Count).Select(i=>c.CommandList[i].Id).ToArray()}).ToArray();
            }
        }
        catch(Exception e) { output["error"]=e.ToString(); }
        File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-inspect.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
    }
}
