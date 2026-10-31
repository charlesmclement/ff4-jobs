using BepInEx;
using FF4Jobs.Core;
using Last.Data;
using Last.Data.Master;
using Last.Data.User;
using Last.Management;
using Last.Systems;
using Last.Battle;
using System.Text.Json;

namespace FF4Jobs;

// Explicit, local development request only. Does not write a game save or add items.
internal static class ReleaseProbe
{
    internal static void Poll()
    {
        if(Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE")!="1" || State.GamePhase!=SubSceneManagerMainGame.State.Player)return;
        string path=Path.Combine(Paths.ConfigPath,"ff4jobs-selftest.request");
        var popupPath=Path.Combine(Paths.ConfigPath,"ff4jobs-popup-test.request");
        if(File.Exists(popupPath)) {File.Delete(popupPath);if(State.Campaign.IsUnlocked("thief"))State.Campaign.AnnouncedJobs.Remove("thief");}
        if(!File.Exists(path))return;
        File.Delete(path);
        var result=new Dictionary<string,object?>();
        var saved=State.Campaign;var rng=UnityEngine.Random.state;
        try
        {
            var data=UserDataManager.Instance();var ids=data.GetMemberIndexList();
            var character=data.GetMemberData(ids[0]);
            Projection.RestoreAll();
            var clone=character.Clone();Projection.Restore(clone);
            State.Campaign=new Campaign();
            State.Campaign.StoryFlags.UnionWith(Jobs.UnlockFlags.Values);State.Campaign.StoryFlags.Add(18);
            var inventory=new Dictionary<int,int>();
            foreach(var pair in data.normalOwnedItems)inventory[pair.Key]=pair.Value.Count;
            var nativeResults=new List<object>();int checkedEquipment=0;
            var contents=MasterManager.Instance.GetList<Content>();
            foreach(var job in Jobs.All)
            {
                if(!State.Campaign.IsAvailable(character.characterStatusId,job.Id))continue;
                State.Campaign.Select(character.characterStatusId,job.Id,null);
                foreach(var pair in contents)
                {
                    var c=pair.Value;OwnedItemData? item=null;
                    if(c.TypeId==2)item=new OwnedItemData(c,MasterManager.Instance.GetData<Weapon>(c.TypeValue),0,null);
                    if(c.TypeId==3)item=new OwnedItemData(c,MasterManager.Instance.GetData<Armor>(c.TypeValue),0,null);
                    if(item==null)continue;
                    bool native;
                    State.Bypass++;try {native=EquipUtility.CanEquipped(item,character.JobId);}finally{State.Bypass--;}
                    bool expected=EquipmentRules.Allowed(character,item,native);
                    bool actual=EquipUtility.CanEquipped(item,character.JobId);
                    if(actual!=expected)throw new InvalidOperationException($"Equipment hook mismatch: {job.Id}/{c.Id}");
                    checkedEquipment++;
                }
            }
            result["equipmentChecks"]=checkedEquipment;
            State.Campaign.Select(character.characterStatusId,"original",null);
            int nativeMp=clone.Parameter.ConfirmedMaxMp();
            State.Campaign.Select(character.characterStatusId,"original","white_mage");
            int untrainedMp=clone.Parameter.ConfirmedMaxMp();
            if(nativeMp!=untrainedMp)throw new InvalidOperationException("Untrained subjob granted MP.");
            result["untrainedSubjobMpUnchanged"]=true;
            bool same=true;
            foreach(var pair in data.normalOwnedItems)if(!inventory.TryGetValue(pair.Key,out int n) || pair.Value.Count!=n)same=false;
            if(!same || data.normalOwnedItems.Count!=inventory.Count)throw new InvalidOperationException("Probe changed inventory.");
            result["inventoryUnchanged"]=true;result["passed"]=true;
        }
        catch(Exception e) {result["passed"]=false;result["error"]=e.ToString();Plugin.Logger.LogError("Release probe: "+e);}
        finally {Projection.RestoreAll();State.Campaign=saved;UnityEngine.Random.state=rng;Projection.Sync();}
        File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-selftest.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
}
