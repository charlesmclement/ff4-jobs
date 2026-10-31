using BepInEx;
using Last.Data.Master;
using Last.Data.User;
using Last.Defaine;
using Last.Management;
using Last.Systems;
using UnityEngine;
using NativeItems=Il2CppSystem.Collections.Generic.List<Last.Data.User.OwnedItemData>;

namespace FF4Jobs;

// Explicit developer run only: disposable native character/items, no save load.
public sealed class EquipmentCrashProbe : MonoBehaviour
{
    public EquipmentCrashProbe(IntPtr p):base(p) { }
    internal static IntPtr Character;
    private bool ran;
    internal static void Step(string s)
    {
        Plugin.Logger.LogInfo("Equipment probe: "+s);
        File.AppendAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-equipment-probe.log"),s+"\n");
    }
    private void Update()
    {
        if(ran) return;
        var master=MasterManager.Instance;
        if(master==null) return;
        CharacterStatus status;
        try { status=master.GetData<CharacterStatus>(8);if(status==null) return; }
        catch { return; }
        ran=true;
        try
        {
            Step("create Yang");
            var c=new OwnedCharacterData();c.SetCharacterStatus(status,true);
            c.SetParameter(new Last.Data.PlayerCharacterParameter(c,baseLevel:10,basePower:12));
            c.JobId=11;
            c.OwnedJobDataList.Clear();c.OwnedJobDataList.Add(new OwnedJobData {Id=11,Level=10});
            c.Parameter.AddtionalLevel=10;
            c.OwnedAbilityList=new Il2CppSystem.Collections.Generic.List<OwnedAbility>();
            foreach(var pair in master.GetList<Content>())
            {
                if(pair.Value.TypeId!=4)continue;
                var ability=master.GetData<Ability>(pair.Value.TypeValue);
                if(ability!=null && ability.TypeId==12)c.OwnedAbilityList.Add(new OwnedAbility(ability,pair.Value,0));
            }
            var items=new NativeItems();
            OwnedItemData Item(int id)
            {
                var content=master.GetData<Content>(id);
                var item=content.TypeId==2
                    ?new OwnedItemData(content,master.GetData<Weapon>(content.TypeValue),2,null)
                    :new OwnedItemData(content,master.GetData<Armor>(content.TypeValue),2,null);
                items.Add(item);Step($"item requested={id} actual={item.Id} kind={HandEquipmentRules.Kind(item)} count={item.Count}");return item;
            }
            var claw=Item(162);var empty=Item(127);
            c.SetEquip(EquipSlotType.Slot1,claw);c.SetEquip(EquipSlotType.Slot2,empty);
            c.SetEquip(EquipSlotType.Slot3,Item(264));c.SetEquip(EquipSlotType.Slot4,Item(226));c.SetEquip(EquipSlotType.Slot5,Item(227));
            Character=c.Pointer;
            Step($"character={c.characterStatusId} level={c.Parameter.ConfirmedLevel()} strength={c.Parameter.ConfirmedPower()}");
            Step("update equipped stats");c.UpdateParameter();
            Step("preview accuracy");Step("accuracy="+c.Parameter.ConfirmedAccuracyRate());
            Step("native choices");
            Step("native dual="+c.HasTwoSwordStyle+" dominant="+c.GetDominantArm());
            var list=ItemFilters.GetEquipItems(items.Cast<Il2CppSystem.Collections.Generic.IEnumerable<OwnedItemData>>(),EquipSlotType.Slot1,c);
            Step("enumerate choices");int count=0;foreach(var item in HandEquipmentRules.Items(list))count++;
            Step("choices="+count);
            Step("native model remove claw");
            // Detached fixtures exercise the native data setter. The UI's public
            // client also requires campaign inventory/character registration;
            // that part remains a live-menu acceptance check.
            c.SetEquip(EquipSlotType.Slot1,empty);
            Step("update barehand stats");c.UpdateParameter();
            if(!Monk.EmptyHands(c))throw new Exception("Initial claw was not removed");
            Step("accuracy="+c.Parameter.ConfirmedAccuracyRate());
            var provider=new Serial.FF4.Management.ParameterProvider();
            int bare=provider.GetOffensivePower(c);
            Step("barehand power="+bare);
            for(int round=0;round<3;round++)
            {
                foreach(var slot in new[]{EquipSlotType.Slot1,EquipSlotType.Slot2})
                {
                    Step("equip claw round="+round+" slot="+slot);
                    c.SetEquip(slot,claw);c.UpdateParameter();
                    Step($"hands={Ninja.Hand(c,EquipSlotType.Slot1)?.Id}/{Ninja.Hand(c,EquipSlotType.Slot2)?.Id} kinds={HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot1))}/{HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot2))}");
                    int power=provider.GetOffensivePower(c),accuracy=c.Parameter.ConfirmedAccuracyRate();
                    int nativePower,nativeAccuracy;
                    State.Bypass++;
                    try { nativePower=provider.GetOffensivePower(c);nativeAccuracy=c.Parameter.ConfirmedAccuracyRate(); }
                    finally { State.Bypass--; }
                    Step($"claw power={power}/{nativePower} accuracy={accuracy}/{nativeAccuracy}");
                    if(power!=nativePower || accuracy!=nativeAccuracy)throw new Exception("Claw stats differ from native");
                    if(Ninja.Hand(c,slot)?.Id!=claw.Id)throw new Exception("Claw was not equipped");
                    if(bare!=power+2)throw new Exception("Barehand must add exactly 2 attack");
                    var choices=ItemFilters.GetEquipItems(items.Cast<Il2CppSystem.Collections.Generic.IEnumerable<OwnedItemData>>(),slot,c);
                    foreach(var item in HandEquipmentRules.Items(choices))count++;
                    Step("remove claw round="+round+" slot="+slot);
                    c.SetEquip(slot,empty);c.UpdateParameter();
                    if(!Monk.EmptyHands(c))throw new Exception("Claw was not removed");
                    if(provider.GetOffensivePower(c)!=bare)throw new Exception("Barehand power changed");
                }
            }
            Step("PASS: native equipment lists, model hand changes and stats; UI inventory commit remains a live check");
        }
        catch(Exception e) { Step("FAIL "+e); }
        finally { Character=IntPtr.Zero;Application.Quit(); }
    }
}
