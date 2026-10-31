using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.User;
using Last.Defaine;

namespace FF4Jobs;

internal static class Ninja
{
    internal static bool Trained(OwnedCharacterData c) => State.Applies(c)
        && State.Campaign.IsAvailable(c.characterStatusId,"ninja",Plugin.TestAllJobs.Value)
        && JobFeatures.HasPassive(State.Campaign.Character(c.characterStatusId),"ninja",Plugin.TestAllSkills.Value);
    internal static OwnedItemData? Hand(OwnedCharacterData c,EquipSlotType slot) =>
        c.EquipmentData!=null && c.EquipmentData.ContainsKey(slot)?c.EquipmentData[slot]:null;
    internal static bool IsWeapon(OwnedItemData? item) => item!=null && !item.IsRemoveItem
        && item.Weapon!=null && JobFeatures.DualWieldWeapon(item.Weapon.CategoryType,item.IsTwoHanded);
    internal static bool TwoWeapons(OwnedCharacterData c) =>
        IsWeapon(Hand(c,EquipSlotType.Slot1)) && IsWeapon(Hand(c,EquipSlotType.Slot2));
}

// This getter is a unique native LINQ routine, not a shared IL2CPP field getter.
// Native equip/Optimize windows already consult it to allow an offhand weapon.
[HarmonyPatch(typeof(OwnedCharacterData),nameof(OwnedCharacterData.HasTwoSwordStyle),MethodType.Getter)]
internal static class NinjaEquipmentPassive
{
    static void Postfix(OwnedCharacterData __instance,ref bool __result)
    {
        if(Ninja.Trained(__instance)) __result=true;
        // Changing main jobs must not strip Edge's innate native passive.
        else if(State.Applies(__instance))
        {
            var original=Projection.OriginalAbilities(__instance);
            for(int i=0;i<original.Count;i++)
                if(original[i]?.Ability?.TypeId==12) { __result=true;break; }
        }
    }
}

// FF4's normal attack code already animates both equipped weapons. Its attack
// formula, however, hard-codes native Ninja identity. Route trained characters
// through that same helper without changing their character/job/story identity.
[HarmonyPatch(typeof(Serial.FF4.Management.ParameterProvider),"GetOffensivePower")]
internal static class NinjaAttackPower
{
    static bool Prefix(Serial.FF4.Management.ParameterProvider __instance,OwnedCharacterData data,ref int __result)
    {
        if(!Ninja.Trained(data) || !Ninja.TwoWeapons(data)) return true;
        if(Monk.Trained(data) && Monk.MartialHands(data)) return true;
        __result=__instance.GetTwoHandNinjaOffensivePower(data);
        return false;
    }
}
