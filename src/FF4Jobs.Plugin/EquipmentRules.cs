using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.User;
using Last.Management;
using Last.Systems;

namespace FF4Jobs;

internal static class EquipmentRules
{
    internal static bool Allowed(OwnedCharacterData c,OwnedItemData item,bool nativeAllowed)
    {
        var job=State.Campaign.Character(c.characterStatusId).Main;
        if(job=="original" || item.IsRemoveItem)return nativeAllowed;
        if(item.Weapon!=null)return Balance.WeaponAllowed(job,item.Weapon.CategoryType,c.characterStatusId);
        if(item.Armor!=null)return Balance.ArmorAllowed(job,item.Armor.EquipJobGroupId,nativeAllowed);
        return nativeAllowed;
    }

    internal static OwnedCharacterData? CharacterForNativeJob(int jobId)
    {
        var data=UserDataManager.Instance();if(data==null)return null;
        var ids=data.GetMemberIndexList();
        for(int i=0;i<ids.Count;i++)
        {
            var c=data.GetMemberData(ids[i]);
            if(c!=null && c.JobId==jobId && State.Applies(c))return c;
        }
        return null;
    }

    internal static int Reconcile(OwnedCharacterData c)
    {
        if(!State.Applies(c) || State.GamePhase is not (SubSceneManagerMainGame.State.Player or SubSceneManagerMainGame.State.Menu))return 0;
        var remove=new List<Last.Defaine.EquipSlotType>();
        foreach(var entry in c.EquipmentData)
        {
            var item=entry.Value;
            if(item!=null && !item.IsRemoveItem && !EquipUtility.CanEquipped(item,c.JobId))remove.Add(entry.Key);
        }
        if(remove.Count==0)return 0;
        // Native removal updates inventory/equip counts; never destroy or duplicate items.
        var client=new OwnedCharacterClient();
        foreach(var slot in remove)client.RemoveEquipOfTargetSlot(c.Id,slot);
        Plugin.Logger.LogInfo($"{c.Name}: returned {remove.Count} incompatible equipment slot(s) to inventory.");
        return remove.Count;
    }
}

[HarmonyPatch(typeof(EquipUtility),nameof(EquipUtility.CanEquipped),new[]{typeof(OwnedItemData),typeof(int)})]
internal static class EquipmentEligibility
{
    static void Postfix(OwnedItemData target,int jobid,ref bool __result)
    {
        if(!Plugin.Enabled || State.Bypass>0 || target==null)return;
        var c=EquipmentRules.CharacterForNativeJob(jobid);
        if(c!=null)__result=EquipmentRules.Allowed(c,target,__result);
    }
}
