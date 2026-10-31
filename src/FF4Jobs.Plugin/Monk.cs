using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.User;
using Last.Defaine;

namespace FF4Jobs;

internal static class Monk
{
    internal static bool Trained(OwnedCharacterData c) => State.Applies(c)
        && State.Campaign.IsAvailable(c.characterStatusId,"monk",Plugin.TestAllJobs.Value)
        && JobFeatures.HasPassive(State.Campaign.Character(c.characterStatusId),"monk",Plugin.TestAllSkills.Value);
    internal static bool EmptyHands(OwnedCharacterData c) =>
        HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot1))==HandEquipment.Empty
        && HandEquipmentRules.Kind(Ninja.Hand(c,EquipSlotType.Slot2))==HandEquipment.Empty;
}

// FF4's native Monk helper is 2*own level + own strength/4 + 2. Extend it with
// personal Monk training, only with BOTH hands empty. Final native attack status
// modifiers, defense, accuracy, Kick/Focus and damage processing still apply.
[HarmonyPatch(typeof(Serial.FF4.Management.ParameterProvider),"GetOffensivePower")]
internal static class MonkAttackPower
{
    static bool Prefix(OwnedCharacterData data,ref int __result)
    {
        if(!Monk.Trained(data) || !Monk.EmptyHands(data)) return true;
        int points=Plugin.TestAllSkills.Value?310:State.Campaign.Character(data.characterStatusId).Points("monk");
        __result=JobFeatures.UnarmedPower(data.Parameter.ConfirmedLevel(),data.Parameter.ConfirmedPower(),points);
        return false;
    }
}
