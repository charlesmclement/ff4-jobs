using FF4Jobs.Core;
using HarmonyLib;
using Last.Data;
using Last.Data.Master;
using Last.Data.User;
using Last.Systems;
using System.Reflection;
using NativeCommands = Il2CppSystem.Collections.Generic.List<Last.Data.Master.Command>;
using NativeAbilities = Il2CppSystem.Collections.Generic.List<Last.Data.User.OwnedAbility>;
using NativeDictionary = Il2CppSystem.Collections.Generic.Dictionary<Last.Data.Master.Command, Il2CppSystem.Collections.Generic.List<Last.Data.User.OwnedAbility>>;

namespace FF4Jobs;

internal static class Projection
{
    // IL2CPP shares trivial getters between unrelated types. Do not detour those
    // accessors. Install reversible command/ability views at safe boundaries instead.
    private sealed class View
    {
        internal OwnedCharacterData Character = null!;
        internal NativeCommands Commands = null!;
        internal NativeAbilities Abilities = null!;
        internal NativeDictionary Dictionary = null!;
    }
    private static readonly Dictionary<IntPtr,View> views = new();
    internal static NativeAbilities OriginalAbilities(OwnedCharacterData c)
        => views.TryGetValue(c.Pointer,out var view) ? view.Abilities : c.OwnedAbilityList;
    internal static OwnedCharacterData? AbilityOwner(OwnedAbility ability)
    {
        foreach (var v in views.Values)
        {
            var list = v.Character.OwnedAbilityList;
            for (int i=0;i<list.Count;i++) if (list[i].Pointer == ability.Pointer) return v.Character;
        }
        return null;
    }
    internal static void Clear() => RestoreAll();
    internal static void Restore(OwnedCharacterData c)
    {
        if(!views.Remove(c.Pointer,out var view)) return;
        c.CommandList=view.Commands; c.OwnedAbilityList=view.Abilities; c.AbilityDictionary=view.Dictionary;
    }
    internal static void RestoreAll()
    {
        foreach(var v in views.Values.ToArray()) Restore(v.Character);
    }
    internal static void Apply(OwnedCharacterData c)
    {
        if(!State.Applies(c)) { Restore(c); return; }
        if(views.ContainsKey(c.Pointer)) return;
        var view=new View { Character=c, Commands=c.CommandList, Abilities=c.OwnedAbilityList, Dictionary=c.AbilityDictionary };
        views.Add(c.Pointer,view);
        try
        {
            c.CommandList=Commands(c,view.Commands);
            c.OwnedAbilityList=Abilities(c,view.Abilities);
            var dictionary=new NativeDictionary();
            for(int i=0;i<c.CommandList.Count;i++)
            {
                var command=c.CommandList[i]; var list=new NativeAbilities();
                for(int j=0;j<c.OwnedAbilityList.Count;j++)
                    if(command.Id==JobFeatures.Dualcast
                        ? Dualcast.IsSpell(c.OwnedAbilityList[j].Ability)
                        : c.OwnedAbilityList[j].Ability.AbilityGroupId==command.AbilityGroupId) list.Add(c.OwnedAbilityList[j]);
                dictionary[command]=list;
            }
            c.AbilityDictionary=dictionary;
        }
        catch { Restore(c); throw; }
    }
    internal static void Sync()
    {
        StoryProgression.Sync();
        foreach(var v in views.Values.ToArray()) if(!State.Applies(v.Character)) Restore(v.Character);
        var data=Last.Management.UserDataManager.Instance();
        if(data==null) return;
        var members=data.GetMemberIndexList();
        for(int i=0;i<members.Count;i++)
        {
            var c=data.GetMemberData(members[i]); if(c==null) continue;
            State.Campaign.Recruit(c.characterStatusId);
            Apply(c);
            EquipmentRules.Reconcile(c);
        }
        var battle=Last.Battle.BattlePlugManager.Instance();
        if(battle!=null && battle.IsBattle())
        {
            var units=battle.GetPlayerUnits();
            if(units!=null) for(int i=0;i<units.Length;i++)
            {
                var c=units[i]?.GetOwnedCharacterData(); if(c!=null) Apply(c);
            }
        }
    }
    internal static NativeCommands Commands(OwnedCharacterData c, NativeCommands original)
    {
        var ids = new List<int>();
        for (int i=0;i<original.Count;i++) ids.Add(original[i].Id);
        var result = new NativeCommands();
        var selected = State.Campaign.Commands(c.characterStatusId,ids,Plugin.TestAllSkills.Value).AsEnumerable();
        if(CombatMenuTest.Applies(c)) selected=selected.Concat(CombatMenuTest.Commands).Distinct();
        foreach(int id in selected)
        {
            var command = CustomCommands.Get(id);
            if (command != null) result.Add(command);
        }
        return result;
    }
    internal static NativeAbilities Abilities(OwnedCharacterData c, NativeAbilities original)
    {
        var progress = State.Campaign.Character(c.characterStatusId);
        var output = new NativeAbilities();
        var seen = new HashSet<int>();
        if(progress.Main=="original")
            for(int i=0;i<original.Count;i++) { output.Add(original[i]); seen.Add(original[i].Ability.Id); }
        var master = MasterManager.Instance;
        var contents = master.GetList<Content>();
        foreach (var pair in contents)
        {
            var content = pair.Value;
            // Verified against build 20265023. Exclude item/enemy copies of spells.
            if(content.TypeId != 4 || !content.MesIdName.StartsWith("MSG_MAGIC_NAME_",StringComparison.Ordinal)) continue;
            var ability = master.GetData<Ability>(content.TypeValue);
            if(ability == null || ability.AbilityGroupId < 1 || ability.AbilityGroupId > 4) continue;
            bool allowed = Allows(Jobs.Get(progress.Main), ability, Plugin.TestAllSkills.Value?310:progress.Points(progress.Main), false, original);
            if(progress.Sub != null) allowed |= Allows(Jobs.Get(progress.Sub), ability, Plugin.TestAllSkills.Value?310:progress.Points(progress.Sub), true, original);
            if(CombatMenuTest.Applies(c) && SpellProgression.IsPlayerSpell(ability.Id)
                && ability.AbilityGroupId is 1 or 2 or 4) allowed=true;
            if(allowed && seen.Add(ability.Id)) output.Add(new OwnedAbility(ability,content,0));
        }
        return output;
    }
    internal static bool Allows(Core.Job job, Ability ability, int points, bool sub, NativeAbilities native)
    {
        if(!SpellProgression.CanLearn(State.Campaign,ability.Id,points,Plugin.TestAllSkills.Value)) return false;
        int rank = Jobs.Rank(points);
        if(sub && rank == 0) return false;
        int group = ability.AbilityGroupId;
        string school = job.MagicSchool;
        if(school == "summon")
        {
            return group==3 && SummonProgression.CanUse(State.Campaign,ability.Id,points,sub);
        }
        if(school.Length==0 || school=="original") return false;
        bool groupOk = school switch {
            "white" or "paladin" => group==1,
            "black" => group==2,
            "red" or "sage" => group==1 || group==2,
            "ninja" => group==4,
            "time" => TimeSpells.Contains(ability.Id), _ => false
        };
        if(!groupOk) return false;
        int cap = new[]{5,10,18,30,45,99}[rank];
        if(school=="red") cap=Math.Min(cap,18);
        if(school=="paladin") cap=Math.Min(cap,10);
        // Meteor is tied to FF4 story scenes; native acquisition remains authoritative.
        if(ability.Id==MeteorId && school!="black" && school!="sage") return false;
        return ability.UseValue <= cap;
    }
    internal static readonly HashSet<int> TimeSpells = new(){19,20,27,29,46};
    internal static int MeteorId = 54;
}

[HarmonyPatch(typeof(OwnedCharacterData),nameof(OwnedCharacterData.Clone))]
internal static class ClonePatch
{
    static void Prefix(OwnedCharacterData __instance) => Projection.Restore(__instance);
    static void Postfix(OwnedCharacterData __instance, OwnedCharacterData __result)
    {
        Projection.Apply(__instance); if(__result!=null) Projection.Apply(__result);
    }
}

[HarmonyPatch(typeof(Last.Management.SubSceneManagerMainGame), nameof(Last.Management.SubSceneManagerMainGame.ChangeState))]
internal static class StateTransitionPatch
{
    static void Prefix() => Projection.RestoreAll();
}

[HarmonyPatch(typeof(Last.UI.MenuManager),nameof(Last.UI.MenuManager.UpdateController))]
internal static class MenuInputPatch
{
    static bool Prefix() => !JobWindow.IsOpen;
}

[HarmonyPatch]
internal static class StoryMutationPatch
{
    static IEnumerable<MethodBase> TargetMethods() => typeof(Last.Management.OwnedCharacterClient).GetMethods()
        .Where(m=>m.Name is "AddAbility" or "RemoveAbility" or "LearningAbility" or "AddCommand" or "RemoveCommand"
            or "ChangeChracterStatusId" or "SetOwnedCharacter" or "AddFromMaster" or "AddFromJson" or "AddExp");
    static void Prefix() => Projection.RestoreAll();
    // Reapply on the next Update, once the entire script/mutation is finished.
}

[HarmonyPatch]
internal static class AbilityAvailabilityScope
{
    static IEnumerable<MethodBase> TargetMethods() => typeof(AbilityUtility).GetMethods()
        .Where(m=>m.Name is "CanUseCommand" or "CanUseBattleAbility" or "CanUseMenuAbility");
    static void Prefix(object[] __args, out int __state)
    {
        __state=State.JobGroupScope;
        foreach(var arg in __args)
            if(arg is OwnedCharacterData c && State.Applies(c)) { State.JobGroupScope++; break; }
    }
    static void Finalizer(int __state) => State.JobGroupScope=__state;
}
[HarmonyPatch(typeof(AbilityUtility),nameof(AbilityUtility.CanEquipped))]
internal static class JobEligibilityPatch
{
    static void Postfix(ref bool __result) { if(State.JobGroupScope>0) __result=true; }
}

[HarmonyPatch]
internal static class StatsPatch
{
    static IEnumerable<MethodBase> TargetMethods() => typeof(PlayerCharacterParameter).GetMethods()
        .Where(m=>m.Name is "ConfirmedPower" or "ConfirmedVitality" or "ConfirmedAgility" or "ConfirmedIntelligence" or "ConfirmedSpirit" or "ConfirmedMaxMp");
    static void Postfix(PlayerCharacterParameter __instance, MethodBase __originalMethod, ref int __result)
    {
        var c=__instance.parent;
        if(!State.Applies(c)) return;
        var p=State.Campaign.Character(c.characterStatusId); var job=Jobs.Get(p.Main);
        if(__originalMethod.Name=="ConfirmedMaxMp")
        {
            if(BloodMagic.UsesHp(c)) return; // Pre-Paladin Cecil casts from HP.
            bool caster=job.IsCaster || (p.Main=="original" && Mastery.HasCaster(State.Campaign,c.characterStatusId,Plugin.TestAllJobs.Value)) || (p.Sub!=null && Jobs.Get(p.Sub).IsCaster && (Plugin.TestAllSkills.Value || p.Points(p.Sub)>=10));
            if(caster) __result=Math.Clamp(Math.Max(__result,10+__instance.BaseLevel*3),0,999);
            return;
        }
        var stat=__originalMethod.Name switch {
            "ConfirmedPower"=>JobStat.Strength,"ConfirmedVitality"=>JobStat.Vitality,"ConfirmedAgility"=>JobStat.Agility,
            "ConfirmedIntelligence"=>JobStat.Intelligence,"ConfirmedSpirit"=>JobStat.Spirit,_=>JobStat.Strength
        };
        int adjustment=Mastery.Bonus(State.Campaign,c.characterStatusId,stat,Plugin.TestAllJobs.Value);
        __result=Math.Clamp(__result+adjustment,1,99);
    }
}
