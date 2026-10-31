using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.Master;
using Last.Data.User;
using Last.Defaine;
using Last.Management;
using Last.Systems;
using System.Reflection;
using NativeItems=Il2CppSystem.Collections.Generic.IEnumerable<Last.Data.User.OwnedItemData>;

namespace FF4Jobs;

internal static class HandEquipmentRules
{
    internal static IEnumerable<OwnedItemData> Items(NativeItems source)
    {
        // Keep enumeration inside IL2CPP. Casting a generic enumerator to the
        // non-generic interface hard-crashes while reading native item lists.
        var list=new Il2CppSystem.Collections.Generic.List<OwnedItemData>(source);
        for(int i=0;i<list.Count;i++) yield return list[i];
    }
    internal static bool IsHand(EquipSlotType slot) => slot is EquipSlotType.Slot1 or EquipSlotType.Slot2;
    internal static int Kind(OwnedItemData? item) => item==null || item.IsRemoveItem ? HandEquipment.Empty
        : item.Weapon!=null ? (item.Weapon.CategoryType==43?HandEquipment.Empty:item.Weapon.CategoryType)
        : item.Armor?.PartsGroupId==2 ? HandEquipment.Shield : HandEquipment.Empty;
    internal static bool Shields(OwnedCharacterData c) =>
        HandEquipment.AllowsShields(State.Campaign.Character(c.characterStatusId).Main);
    internal static bool PairAllowed(OwnedCharacterData c,OwnedItemData? first,OwnedItemData? second) =>
        HandEquipment.PairAllowed(Kind(first),Kind(second),first?.IsTwoHanded??false,
            second?.IsTwoHanded??false,c.HasTwoSwordStyle || (Monk.Trained(c) && JobFeatures.MonkHands(Kind(first),Kind(second))),Shields(c));
    internal static bool SlotAllowed(OwnedCharacterData c,OwnedItemData item,EquipSlotType slot) =>
        (IsHand(slot) && Kind(item)==HandEquipment.Shield && Shields(c))
        || (IsHand(slot) && Kind(item)==HandEquipment.Claw && Monk.Trained(c))
        || item.PartsGroup==null || EquipUtility.IsEquipSlot(item.PartsGroup,slot,c.GetDominantArm(),c.HasTwoSwordStyle);
    internal static bool Candidate(OwnedCharacterData c,EquipSlotType slot,OwnedItemData item)
    {
        if(item.IsRemoveItem) return true;
        if(!SlotAllowed(c,item,slot)) return false;
        if(!IsHand(slot)) return true;
        var other=Ninja.Hand(c,slot==EquipSlotType.Slot1?EquipSlotType.Slot2:EquipSlotType.Slot1);
        return PairAllowed(c,item,other);
    }
    internal static OwnedCharacterData? ById(int id)
    {
        var data=UserDataManager.Instance();if(data==null)return null;
        foreach(int index in data.GetMemberIndexList())
        { var c=data.GetMemberData(index);if(c?.Id==id)return c; }
        return null;
    }
}

// The native item filter is shared by manual equipment and Optimize. Materialize
// its result before filtering: returning a managed lazy iterator across IL2CPP is unsafe.
[HarmonyPatch(typeof(ItemFilters),nameof(ItemFilters.GetEquipItems))]
internal static class HandEquipmentChoices
{
    static void Postfix(NativeItems sourceList,EquipSlotType slot,OwnedCharacterData target,ref NativeItems __result)
    {
        if(!State.Applies(target,true) || !HandEquipmentRules.IsHand(slot)) return;
        var result=new Il2CppSystem.Collections.Generic.List<OwnedItemData>();
        var seen=new HashSet<int>();
        foreach(var item in HandEquipmentRules.Items(__result))
        {
            if(item!=null && HandEquipmentRules.Candidate(target,slot,item) && seen.Add(item.Id)) result.Add(item);
        }
        if(HandEquipmentRules.Shields(target) || Monk.Trained(target))
            foreach(var item in HandEquipmentRules.Items(sourceList))
            {
                if(item!=null && ((HandEquipmentRules.Kind(item)==HandEquipment.Shield && HandEquipmentRules.Shields(target))
                        || (HandEquipmentRules.Kind(item)==HandEquipment.Claw && Monk.Trained(target)))
                    && item.Count>item.EquipCount && EquipUtility.CanEquipped(item,target.JobId)
                    && HandEquipmentRules.Candidate(target,slot,item) && seen.Add(item.Id)) result.Add(item);
            }
        __result=result.Cast<NativeItems>();
    }
}

// Validate at the PUBLIC SetEquip boundary, before native inventory bookkeeping.
// Covers direct, preview and Optimize callers without patching shared setters.
[HarmonyPatch]
internal static class HandEquipmentCommit
{
    static IEnumerable<MethodBase> TargetMethods() => typeof(OwnedCharacterClient).GetMethods()
        .Where(m=>m.Name=="SetEquip");
    static bool Prefix(MethodBase __originalMethod,object[] __args)
    {
        OwnedCharacterData? character=null;OwnedItemData? item=null;Content? content=null;
        EquipSlotType slot=EquipSlotType.Non;int id=-1;
        var parameters=__originalMethod.GetParameters();
        for(int i=0;i<__args.Length;i++)
        {
            if(__args[i] is OwnedCharacterData c) character=c;
            else if(__args[i] is OwnedItemData data) item=data;
            else if(__args[i] is Content master) content=master;
            else if(__args[i] is EquipSlotType s) slot=s;
            else if(parameters[i].Name=="id" && __args[i] is int n) id=n;
        }
        character??=id>=0?HandEquipmentRules.ById(id):null;
        if(character==null || !State.Applies(character,true)) return true;
        item??=content==null?null:UserDataManager.Instance()?.SearchOwnedItem(content.Id);
        return item==null || HandEquipmentRules.Candidate(character,slot,item);
    }
}
