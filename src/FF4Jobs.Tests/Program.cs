using FF4Jobs.Core;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception(label); }
var campaign = new Campaign();
Check(campaign.IsUnlocked("original") && !campaign.IsUnlocked("dark_knight") && !campaign.IsUnlocked("dragoon") && !campaign.IsUnlocked("paladin"),"New game has only starting jobs");
Reject(()=>campaign.Select(1,"dragoon",null),"Story-locked main jobs rejected");
campaign.Recruit(2); campaign.Recruit(3);
Check(!campaign.IsUnlocked("dragoon") && !campaign.IsUnlocked("thief"),"Kain joining does not unlock jobs before Mist");
campaign.StoryFlags.UnionWith(new[]{7,17,43,107});
Check(campaign.IsUnlocked("dragoon") && campaign.IsUnlocked("ranger"),"Story milestones unlock jobs party-wide");
Check(campaign.IsUnlocked("thief") && !campaign.IsUnlocked("bard"),"Mist unlocks Thief independently of Antlion");
Check(campaign.Character(1).Main == "original", "New characters retain original job");
campaign.Select(1,"dragoon","white_mage");
Check(!campaign.Commands(1,new[]{1,3}).Contains(7), "Unlocked untrained subjob selectable without granting its skills");
campaign.Select(1,"white_mage",null);
campaign.Award(1,10);
campaign.Select(1,"dragoon","white_mage");
Check(campaign.Commands(1,new[]{1,10,3}).SequenceEqual(new[]{1,11,7,3}), "Main/sub commands compose correctly");
campaign.Award(1,30);
Check(campaign.Character(1).Points("white_mage") == 10, "Only main job earns ABP");
Check(campaign.Character(13).Main == "dragoon", "Cecil's Paladin transformation preserves chosen job");
Check(campaign.Character(13).Points("dragoon") == 30, "Cecil's ABP survives transformation");
Check(campaign.Character(2).Points("dragoon") == 0, "Characters have independent progress");
campaign.Select(2,"dragoon",null); campaign.Award(2,int.MaxValue);
Check(campaign.Character(2).Points("dragoon") == 310, "ABP saturates without overflow");
Reject(() => campaign.Award(2,-1), "Negative rewards rejected");
Reject(() => campaign.Select(2,"dragoon","dragoon"), "Duplicate job slots rejected");
Reject(() => campaign.Select(2,"missing",null), "Unknown jobs rejected");
campaign.Validate(); checks++;
var loaded = JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(campaign))!;
loaded.Validate();
Check(loaded.Character(13).Sub == "white_mage" && loaded.Character(1).Points("dragoon")==30, "Save/load round trip");
var corrupt = new Campaign{Schema=2}; Reject(corrupt.Validate,"Unknown save schema rejected");
var otherSave = new Campaign(); Check(otherSave.Character(1).Points("dragoon")==0,"Separate campaigns don't share progress");
Check(Jobs.Rank(9)==0 && Jobs.Rank(10)==1 && Jobs.Rank(309)==4 && Jobs.Rank(310)==5,"Rank boundaries");
Check(loaded.RecruitedCharacters.SetEquals(new[]{2,3}) && loaded.IsUnlocked("white_mage"),"Recruitment persists in the save");
Check(!loaded.Recruit(2),"Returning characters do not repeat unlocks");
loaded.Recruit(13); loaded.StoryFlags.Add(28);
Check(loaded.IsUnlocked("paladin") && loaded.Character(13).Points("dragoon")==30,"Paladin milestone unlocks job without resetting Cecil");
otherSave.Select(1,"sage",null,true);
Check(!otherSave.IsUnlocked("sage") && otherSave.RecruitedCharacters.Count==0,"Test access does not grant story progress");
Reject(()=>otherSave.Select(1,"sage",null),"Disabling test access restores locks");
var legacy=JsonSerializer.Deserialize<Campaign>("{\"Schema\":1,\"Characters\":{}}")!;
legacy.Validate(); Check(legacy.RecruitedCharacters.Count==0,"Earlier prototype saves load without granting all jobs");
var badRecruit=new Campaign(); badRecruit.RecruitedCharacters.Add(99);
Reject(badRecruit.Validate,"Invalid recruitment data rejected");
Check(campaign.Commands(1,new[]{1,10,3,2,4,5}).SequenceEqual(new[]{1,11,7,3,2,4,5}),"Main job preserves universal battle controls");
Check(Jobs.All.Where(j=>j.Id!="original").All(j=>Jobs.UnlockFlags.ContainsKey(j.Id)),"Every job has a story unlock");
var training=new Campaign(); training.Recruit(8); training.Recruit(2); training.StoryFlags.UnionWith(new[]{7,20,43});
training.Select(1,"monk",null);
Check(training.Commands(1,new[]{1,3}).SequenceEqual(new[]{1,25,3}),"New Monk starts with Focus only");
training.Award(1,9); Check(!training.Commands(1,new[]{1,3}).Contains(26),"Kick locked below 10 ABP");
training.Award(1,1); Check(training.Commands(1,new[]{1,3}).Contains(26) && !training.Commands(1,new[]{1,3}).Contains(27),"Kick unlocks at 10 ABP, Gird stays locked");
training.Award(1,20); Check(training.Commands(1,new[]{1,3}).Contains(27),"Gird unlocks at 30 ABP");
training.Select(1,"dragoon","monk");
Check(training.Commands(1,new[]{1,3}).Contains(26) && !training.Commands(1,new[]{1,3}).Contains(27),"Subjob training unlocks commands progressively");
var preview=new Campaign(); preview.Select(1,"monk","white_mage",true,true); preview.Validate();
Check(preview.Commands(1,new[]{1,3},true).Contains(12) && preview.Character(1).Abp.Count==0,"Test skills are usable without awarding ABP");
var restoredPreview=JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(preview))!; restoredPreview.Validate();
Check(!restoredPreview.Commands(1,new[]{1,3}).Contains(7) && !restoredPreview.Commands(1,new[]{1,3}).Contains(26),"Test-only learned skills are inactive after loading without override");
var magic=new Campaign();
Check(!SpellProgression.CanLearn(magic,47,310),"Zero-MP Osmose cannot bypass Meteo gate");
Check(!SpellProgression.CanLearn(magic,35,310),"Grinding cannot unlock Firaga before story gates");
magic.StoryFlags.Add(28);
Check(SpellProgression.CanLearn(magic,48,70) && !SpellProgression.CanLearn(magic,48,30),"Tellah gate plus ABP required for Bio");
Check(!SpellProgression.CanLearn(magic,55,310),"Tellah does not unlock endgame Flare immediately");
magic.StoryFlags.Add(43); Check(SpellProgression.CanLearn(magic,35,150),"Zot unlocks Firaga for trained jobs");
magic.StoryFlags.Add(68); Check(SpellProgression.CanLearn(magic,55,310),"Flare requires late story and mastery");
Check(!SpellProgression.CanLearn(magic,481,310,true),"Enemy zero-MP Meteor excluded even in testing");
Check(SpellProgression.CanLearn(new Campaign(),54,0,true),"Testing bypasses magic gates without changing story flags");
var schedule=new Campaign();
foreach(var gate in Jobs.UnlockFlags) { Check(!schedule.IsUnlocked(gate.Key),gate.Key+" initially locked"); schedule.StoryFlags.Add(gate.Value); Check(schedule.IsUnlocked(gate.Key),gate.Key+" unlocks at its story flag"); schedule.StoryFlags.Clear(); }
var mist=new Campaign();
Check(mist.IsAvailable(1,"dark_knight") && mist.IsAvailable(13,"dark_knight"),"Cecil retains Dark Knight before and after transformation");
Check(!mist.IsAvailable(2,"dark_knight"),"Kain Dark Knight waits for Mist");
mist.StoryFlags.Add(7);
Check(mist.PendingAnnouncements().SequenceEqual(new[]{"thief"}),"Mist announces only Thief");
Check(mist.IsAvailable(2,"dark_knight") && !mist.IsAvailable(4,"dark_knight"),"Dark Knight restricted to Cecil and Kain");
Reject(()=>mist.Select(4,"dark_knight",null,true,true),"Test mode cannot make Rydia a Dark Knight");
Reject(()=>mist.Select(4,"original","dark_knight",true,true),"Rydia cannot use Dark Knight subjob either");
Reject(()=>mist.Select(4,"original","dragoon"),"Dragoon subjob remains locked after Mist");
Check(mist.Commands(2,new[]{1,11,3}).Contains(11),"Kain keeps native Jump before Zot");
mist.StoryFlags.Add(43);
foreach(int character in new[]{1,2,4})
{
    mist.Select(character,"dragoon","thief");
    Check(mist.Character(character).Sub=="thief" && !mist.Commands(character,new[]{1,3}).Contains(17),"Thief subjob available but Steal needs training");
    mist.Select(character,"thief","dragoon");
    Check(!mist.Commands(character,new[]{1,3}).Contains(11),"Untrained Dragoon subjob does not grant Jump");
    mist.Award(character,10); mist.Select(character,"dragoon","thief");
    Check(mist.Commands(character,new[]{1,3}).Contains(17),"10 Thief ABP enables subjob Steal");
}
mist.AnnouncedJobs.UnionWith(mist.PendingAnnouncements());
var announced=JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(mist))!;
announced.Validate(); Check(announced.PendingAnnouncements().Length==0,"Acknowledged dialogs persist across save/load");
announced.StoryFlags.Add(13);
Check(announced.PendingAnnouncements().SequenceEqual(new[]{"black_mage"}),"New milestone still announces after earlier acknowledgment");
Check(legacy.AnnouncedJobs.Count==0,"Legacy saves get an empty announcement history");
var revised=new Campaign(); revised.StoryFlags.Add(7);
revised.Select(1,"dragoon","thief",true); revised.Award(1,14);
revised.Select(4,"original","dragoon",true);
revised.AnnouncedJobs.UnionWith(new[]{"dragoon","thief"});
Check(revised.ReconcileSelections(),"Older early-Dragoon selections reconciled");
Check(revised.Character(1).Main=="original" && revised.Character(1).Sub=="thief" && revised.Character(1).Points("dragoon")==14,"Cecil retains Thief and earned ABP while Dragoon is locked");
Check(revised.Character(4).Sub==null && !revised.AnnouncedJobs.Contains("dragoon"),"Rydia loses early Dragoon and its announcement is rearmed");
Check(!revised.ReconcileSelections(),"Reconciliation is idempotent");
revised.StoryFlags.Add(43);
Check(revised.PendingAnnouncements().Contains("dragoon"),"Dragoon now announced at Zot");
revised.Select(4,"original","dragoon");
Check(!revised.Commands(4,new[]{1,7,8,9,3}).Contains(11),"Rydia cannot borrow Cecil's Dragoon ABP");
revised.Select(4,"dragoon",null); revised.Award(4,9); revised.Select(4,"original","dragoon");
Check(!revised.Commands(4,new[]{1,7,8,9,3}).Contains(11),"Rydia subjob Jump locked at 9 ABP");
revised.Select(4,"dragoon",null); revised.Award(4,1); revised.Select(4,"original","dragoon");
Check(revised.Commands(4,new[]{1,7,8,9,3}).Contains(11),"Rydia subjob Jump unlocks at her own 10 Dragoon ABP");
// Release balance, equipment, migration and story/skill boundary checks.
Check(Balance.StealChance(4,4,0)==35 && Balance.StealChance(4,8,0)==27,"Steal is useful early but tougher enemies resist");
Check(Balance.StealChance(4,4,310)==60 && Balance.StealChance(99,1,310)==75,"Steal grows with training and caps below certainty");
Check(Balance.StealChance(1,99,0)==15,"Steal has a modest failure-heavy floor");
Check(Balance.BattleAbp(0)==1 && Balance.BattleAbp(199)==1 && Balance.BattleAbp(200)==2 && Balance.BattleAbp(int.MaxValue)==10,"ABP reward curve boundaries");
Check(Balance.WeaponAllowed("dragoon",6,4) && !Balance.WeaponAllowed("dragoon",11,4),"Rydia Dragoon gets spears, not rods");
Check(!Balance.WeaponAllowed("thief",2,1) && Balance.WeaponAllowed("thief",5,1),"Cecil Thief loses dark sword access and gains daggers");
Check(!Balance.ArmorAllowed("white_mage",12,true) && Balance.ArmorAllowed("white_mage",9,false),"Caster main rejects heavy armor and gains mage gear");
Check(!Balance.ArmorAllowed("thief",25,false) && Balance.ArmorAllowed("thief",25,true),"Character-specific armor retains native restriction");
Check(Balance.WeaponAllowed("paladin",3,13) && !Balance.WeaponAllowed("paladin",3,4),"Holy swords retain Cecil identity restriction");
foreach(var job in Jobs.All.Where(j=>j.Id!="original"))
{
    Check(Balance.WeaponAllowed(job.Id,43,4),job.Name+" permits empty hands");
    Check(Balance.ArmorAllowed(job.Id,4,true),job.Name+" permits universal armor");
    Check(!string.IsNullOrWhiteSpace(Balance.EquipmentHint(job.Id)),job.Name+" documents equipment");
    for(int command=0;command<job.Commands.Length;command++)
        foreach(bool sub in new[]{true,false})
        {
            int need=job.CommandRequirement(command,sub);
            Check(job.LearnedCommands(need,sub).Contains(job.Commands[command]),job.Name+" exact command threshold");
            if(need>0)Check(!job.LearnedCommands(need-1,sub).Contains(job.Commands[command]),job.Name+" command stays locked below threshold");
        }
}
foreach(int status in Enumerable.Range(1,13))
    Check(mist.IsAvailable(status,"dark_knight",true)==(status is 1 or 2 or 13),"Dark Knight identity restriction for "+status);
foreach(int spell in Enumerable.Range(1,500))
{
    if(!SpellProgression.IsPlayerSpell(spell)) {Check(!SpellProgression.CanLearn(magic,spell,310,true),"Non-player spell excluded "+spell);continue;}
    int need=SpellProgression.RequiredAbp(spell),flag=SpellProgression.RequiredFlag(spell);
    var gates=new Campaign();gates.StoryFlags.Add(28);if(flag>=0)gates.StoryFlags.Add(flag);
    Check(SpellProgression.CanLearn(gates,spell,need),"Spell threshold "+spell);
    if(need>0)Check(!SpellProgression.CanLearn(gates,spell,need-1),"Spell ABP boundary "+spell);
    if(flag>=0) {gates.StoryFlags.Remove(flag);Check(!SpellProgression.CanLearn(gates,spell,310),"Spell story lock "+spell);}
}
var invalidKey=new Campaign();invalidKey.Characters["14"]=new();Reject(invalidKey.Validate,"Nonexistent character rejected");
var returning=new Campaign();returning.StoryFlags.UnionWith(Jobs.UnlockFlags.Values);
foreach(int status in Enumerable.Range(1,13)) {returning.Select(status,"thief",null);returning.Award(status,30);}
var copy=JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(returning))!;copy.Validate();
foreach(int status in Enumerable.Range(1,13))Check(copy.Character(status).Points("thief")==returning.Character(status).Points("thief"),"Return/save preserves character "+status);
Check(copy.Character(13)==copy.Character(1),"Cecil has one canonical progress record");
Console.WriteLine($"PASS: {checks} progression and save checks.");
