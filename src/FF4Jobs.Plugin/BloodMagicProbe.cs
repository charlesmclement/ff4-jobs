using BepInEx;
using FF4Jobs.Core;
using Last.Battle;
using Last.Data.Master;
using Last.Data.User;
using Last.Management;
using Last.Systems;

namespace FF4Jobs;

// Runs native hooks on a copy with a fresh parameter object. Native Clone alone
// shares resources with the source. Never writes a save; roll back on failure.
internal static class BloodMagicProbe
{
    internal static void Poll()
    {
        if (Environment.GetEnvironmentVariable("FF4JOBS_TEST_MODE") != "1"
            || State.GamePhase != SubSceneManagerMainGame.State.Player) return;
        string path = Path.Combine(Paths.ConfigPath, "ff4jobs-blood-test.request");
        if (!File.Exists(path)) return;
        File.Delete(path);
        var saved = State.Campaign;
        var result = new Dictionary<string,object>();
        OwnedCharacterData? source = null;
        int realHp=0, realMp=0;
        try
        {
            var data = UserDataManager.Instance();
            foreach(var i in data.GetMemberIndexList()) { var c=data.GetMemberData(i); if(c.characterStatusId==1) { source=c; break; } }
            if(source==null) throw new Exception("Pre-Paladin Cecil must be in the party for this probe.");
            realHp=source.Parameter.CurrentHP; realMp=source.Parameter.CurrentMP;
            Projection.RestoreAll();
            var clone=source.Clone(); Projection.Restore(clone);
            clone.SetParameter(new Last.Data.PlayerCharacterParameter(clone, baseMaxMp:0));
            if(clone.Parameter.Pointer==source.Parameter.Pointer)
                throw new Exception("Probe parameter is not detached.");
            State.Campaign=new Campaign(); State.Campaign.StoryFlags.UnionWith(new[]{7,13,18});
            State.Campaign.Select(1,"black_mage",null); State.Campaign.Award(1,30);
            Projection.Apply(clone);
            OwnedAbility? spell=null;
            foreach(var a in clone.OwnedAbilityList) if(a.Ability.Id==33) {spell=a;break;}
            if(spell==null)throw new Exception("Probe did not project Fire.");
            var command=MasterManager.Instance.GetData<Command>(8);
            clone.Parameter.CurrentHP=100; clone.Parameter.CurrentMP=0;
            if (!AbilityUtility.CanUseBattleAbility(clone,command,spell,false)) throw new Exception("Fire unavailable at zero MP.");
            if (clone.Parameter.ConfirmedMaxMp()!=0) throw new Exception("Cecil still receives a mod MP pool.");
            result["fireAvailableAtZeroMp"]=true;
            var unit=new BattlePlayerData(); unit.ownedCharacterData=clone;
            unit.BattleUnitDataInfo.Parameter=clone.Parameter;
            var abilities=new Il2CppSystem.Collections.Generic.List<Ability>(); abilities.Add(spell.Ability);
            var act=new BattleActData(unit,new BattleUnitData[]{unit},command,abilities,(BattleActData.ActType)0);
            // Regression: exercise the real action factory, not only menu
            // availability and a direct call to the payment hook.
            var factory=new Serial.FF4.Calc.FunctionProvider();
            State.Bypass++;
            try
            {
                if(factory.CreateActFunction(act).TryCast<UnexplodedActFunction>()==null)
                    throw new Exception("Native zero-MP control did not reproduce No effect.");
            }
            finally { State.Bypass--; }
            foreach(int id in new[]{33,41}) // Reported failures: Fire and Thundaga.
            {
                var testAbilities=new Il2CppSystem.Collections.Generic.List<Ability>();
                testAbilities.Add(MasterManager.Instance.GetData<Ability>(id));
                var testAct=new BattleActData(unit,new BattleUnitData[]{unit},command,testAbilities,(BattleActData.ActType)0);
                if(factory.CreateActFunction(testAct).TryCast<ChantingActFunction>()==null)
                    throw new Exception($"Spell {id} did not create its native chanting action at zero MP.");
            }
            if(clone.Parameter.CurrentHP!=100 || clone.Parameter.CurrentMP!=0)
                throw new Exception("Action validation consumed resources.");
            result["nativeFireAndThundagaActionsAtZeroMp"]=true;
            act.IsExecuteMp=true;
            var execution=new BattleActExection(); execution.inStagingBattleActData=act;
            execution.UseMp();
            if(clone.Parameter.CurrentHP!=80 || clone.Parameter.CurrentMP!=0) throw new Exception("Battle payment was not 20 HP / zero MP.");
            execution.UseMp();
            if(clone.Parameter.CurrentHP!=80) throw new Exception("Same cast paid twice.");
            result["battlePaymentAndDuplicateGuard"]=true;
            clone.Parameter.CurrentHP=10; act.IsExecuteMp=true; execution.UseMp();
            if(clone.Parameter.CurrentHP!=1) throw new Exception("Overkill failed to stop at 1 HP.");
            act.IsExecuteMp=true; execution.UseMp();
            if(clone.Parameter.CurrentHP!=1) throw new Exception("Casting at 1 HP killed Cecil.");
            result["oneHpFloor"]=true;
            clone.Parameter.CurrentHP=100;
            new OwnedCharacterClient().ConsumptionExecution(ref clone,spell);
            if(clone.Parameter.CurrentHP!=80 || clone.Parameter.CurrentMP!=0) throw new Exception("Menu consumption did not use HP.");
            result["menuPayment"]=true;
            State.Campaign.StoryFlags.Add(28);
            if(factory.CreateActFunction(act).TryCast<UnexplodedActFunction>()==null)
                throw new Exception("Paladin milestone still bypasses the native MP check.");
            result["paladinExecutionRequiresMp"]=true;
            clone.Parameter.CurrentMP=10;
            new OwnedCharacterClient().ConsumptionExecution(ref clone,spell);
            if(clone.Parameter.CurrentHP!=80 || clone.Parameter.CurrentMP!=5) throw new Exception("Paladin milestone did not restore MP consumption.");
            result["paladinRestoresMp"]=true;
            if(source.Parameter.CurrentHP!=realHp || source.Parameter.CurrentMP!=realMp) throw new Exception("Probe changed real character resources.");
            result["realCharacterUnchanged"]=true;
            result["passed"]=true;
        }
        catch(Exception e) { result["passed"]=false; result["error"]=e.ToString(); Plugin.Logger.LogError(e); }
        finally
        {
            if(source!=null) { source.Parameter.CurrentHP=realHp; source.Parameter.CurrentMP=realMp; }
            Projection.RestoreAll(); State.Campaign=saved; Projection.Sync();
        }
        File.WriteAllText(Path.Combine(Paths.ConfigPath,"ff4jobs-blood-test.json"),System.Text.Json.JsonSerializer.Serialize(result));
    }
}
