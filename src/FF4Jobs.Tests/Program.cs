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
Check(campaign.Character(1).Points("white_mage") == 10, "Direct main-job award leaves sub job unchanged");
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
Check(Balance.BattleAbp(-1)==1 && Balance.BattleAbp(0)==1 && Balance.BattleAbp(199)==1 && Balance.BattleAbp(200)==2,"ABP reward curve boundaries");
Check(Balance.BattleAbp(1999)==10 && Balance.BattleAbp(2000)==11 && Balance.BattleAbp(8000)==41,"Battle rewards continue beyond the former cap, including boosted EXP");
Check(Balance.BattleAbp(int.MaxValue)==10737419,"Uncapped reward calculation handles maximum EXP without overflow");
var uncappedReward=new Campaign();uncappedReward.StoryFlags.UnionWith(new[]{7,13});uncappedReward.Select(1,"black_mage","thief");
var uncappedGain=uncappedReward.AwardBattle(1,Balance.BattleAbp(8000));
Check(uncappedGain==(41,41) && uncappedReward.Character(1).Points("black_mage")==41 && uncappedReward.Character(1).ExactPoints("thief")==10.25m,"Uncapped battle rewards reach both slots at full and quarter rates");
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
// Sub-job training: exact quarter rewards, old saves, slot changes and caps.
var trickle = new Campaign(); trickle.StoryFlags.UnionWith(new[]{7,13});
trickle.Select(4,"original","thief");
var firstGain=trickle.AwardBattle(4,1);
Check(firstGain.Main==0 && firstGain.SubQuarters==1 && trickle.Character(4).ExactPoints("thief")==.25m,"Original main permits quarter-ABP sub training on the first battle");
Check(!trickle.Commands(4,new[]{1,9,3}).Contains(17),"Fractional training does not bypass Steal requirement");
for(int n=0;n<2;n++)trickle.AwardBattle(4,1);
var resumed=JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(trickle))!;resumed.Validate();
Check(resumed.Character(4).ExactPoints("thief")==.75m,"Fractional ABP survives save/load");
resumed.AwardBattle(4,1);
Check(resumed.Character(4).Points("thief")==1 && resumed.Character(4).Quarters("thief")==0,"Four early battles accumulate one whole sub ABP");
for(int n=0;n<35;n++)resumed.AwardBattle(4,1);
Check(resumed.Character(4).ExactPoints("thief")==9.75m && !resumed.Commands(4,new[]{1,9,3}).Contains(17),"Steal stays locked at 9.75 ABP");
resumed.AwardBattle(4,1);
Check(resumed.Character(4).Points("thief")==10 && resumed.Commands(4,new[]{1,9,3}).Contains(17),"Sub-only grinding unlocks Steal at 10 ABP");
trickle.Select(4,"original","black_mage");trickle.AwardBattle(4,1);
Check(trickle.Character(4).ExactPoints("thief")==.75m && trickle.Character(4).ExactPoints("black_mage")==.25m,"Changing sub jobs does not transfer or erase fractional progress");
trickle.Select(4,"thief","black_mage");var both=trickle.AwardBattle(4,3);
Check(both.Main==3 && both.SubQuarters==3 && trickle.Character(4).ExactPoints("thief")==3.75m && trickle.Character(4).ExactPoints("black_mage")==1,"Both slots train at their rates; promoting a sub preserves fractions");
trickle.Select(4,"thief",null);trickle.AwardBattle(4,2);
Check(trickle.Character(4).ExactPoints("black_mage")==1,"Unequipped sub jobs do not train");
trickle.Select(4,"thief","black_mage");trickle.Award(4,int.MaxValue);
Check(trickle.Character(4).ExactPoints("thief")==310,"Mastering a promoted job clears excess fractions");
Check(trickle.AwardBattle(4,10)==(0,10) && trickle.Character(4).ExactPoints("black_mage")==3.5m,"Mastered main still allows normal sub training");
trickle.AwardBattle(4,int.MaxValue);trickle.Validate();
Check(trickle.Character(4).ExactPoints("black_mage")==310 && trickle.AwardBattle(4,10)==(0,0),"Sub jobs can reach mastery, cap safely and stop earning");
var zeroBefore=JsonSerializer.Serialize(trickle);trickle.AwardBattle(4,0);
Check(JsonSerializer.Serialize(trickle)==zeroBefore,"Zero reward preserves trained progress");
Reject(()=>trickle.AwardBattle(4,-1),"Negative battle reward rejected");
var oldFractionSave=JsonSerializer.Deserialize<Campaign>("{\"Schema\":1,\"Characters\":{\"1\":{\"Main\":\"original\",\"Sub\":\"thief\",\"Abp\":{\"thief\":9}}}}")!;
oldFractionSave.Validate();oldFractionSave.AwardBattle(13,3);
Check(oldFractionSave.Character(1).ExactPoints("thief")==9.75m,"Old saves initialize carry and Cecil's identity shares fractions");
Check(oldFractionSave.Character(4).ExactPoints("thief")==0,"Fractional progress remains per character");
foreach(var bad in new[]{-1,4}) {
 var invalidFraction=new Campaign();invalidFraction.Character(1).AbpQuarters["thief"]=bad;
 Reject(invalidFraction.Validate,"Invalid fractional remainder rejected");
}
var invalidFractionJob=new Campaign();invalidFractionJob.Character(1).AbpQuarters["original"]=1;
Reject(invalidFractionJob.Validate,"Original fractional progress rejected");

Check(CommandScroll.Move(0,4,18,5,1)==(1,4),"Sixth command scrolls into the bottom native row");
Check(CommandScroll.Move(1,0,18,5,-1)==(0,0),"Scrolling up reveals the previous command");
Check(CommandScroll.Move(0,0,18,5,-1)==(0,0) && CommandScroll.Move(13,4,18,5,1)==(13,4),"Command scrolling stops at both ends");
Check(CommandScroll.Move(0,0,18,5,-1,true)==(13,4) && CommandScroll.Move(13,4,18,5,1,true)==(0,0),"Native loop navigation wraps the full command list");
foreach(int total in new[]{1,4,5,6,18,22})
{
    (int Start,int Row) pos=(0,0);
    for(int expected=1;expected<total;expected++)
    {
        pos=CommandScroll.Move(pos.Start,pos.Row,total,5,1);
        Check(pos.Start+pos.Row==expected && pos.Row>=0 && pos.Row<5,"Every command remains reachable in a visible native row");
    }
    for(int expected=total-2;expected>=0;expected--)
    {
        pos=CommandScroll.Move(pos.Start,pos.Row,total,5,-1);
        Check(pos.Start+pos.Row==expected && pos.Row>=0 && pos.Row<5,"Reverse navigation preserves command identity");
    }
}
var journal=new LearningJournal();
Check(journal.Observe(1,"black_mage",new[]{"m/spell/36"},true).Length==0,"Existing spells seed without a popup flood");
journal.FinishCharacter(1);
Check(journal.Observe(1,"black_mage",new[]{"m/spell/36","s/spell/36"},true).SequenceEqual(new[]{"s/spell/36"}),"New sub-job use gets its own notification");
Check(journal.Observe(13,"black_mage",new[]{"m/spell/36","s/spell/36"},true).Length==0,"Cecil transformation does not repeat learning notices");
Check(journal.Observe(4,"black_mage",new[]{"m/spell/36"},true).Length==0,"New character seeds an independent baseline");
journal.FinishCharacter(4);
Check(journal.Observe(4,"black_mage",new[]{"m/spell/36","s/spell/36"},true).Length==1,"Cecil's announcement never suppresses Rydia's unlock");
journal.Observe(1,"white_mage",new[]{"m/spell/6"},false);
Check(journal.Observe(1,"white_mage",new[]{"m/spell/6"},true).Length==0,"Switching to an already unlocked job does not repeat its base spells");
journal.Pending.Add(new LearningNotice{Title="Skills unlocked",Message="Cecil / Black Mage: Fira"});
var journalSave=JsonSerializer.Deserialize<LearningJournal>(JsonSerializer.Serialize(journal))!;journalSave.Validate();
Check(journalSave.Pending.Count==1 && journalSave.Observe(1,"black_mage",new[]{"m/spell/36","s/spell/36"},true).Length==0,"Pending notices and known skills survive save/load");
journalSave.Pending.Clear();
Check(JsonSerializer.Deserialize<LearningJournal>(JsonSerializer.Serialize(journalSave))!.Pending.Count==0,"Dismissed notices stay dismissed in saved progress");
var legacyLearning=JsonSerializer.Deserialize<Campaign>("{\"Schema\":1}")!;legacyLearning.Validate();
Check(legacyLearning.Learning.Known.Count==0 && legacyLearning.LearnedSummons.Count==0,"Legacy saves initialize new learning fields");
foreach(var job in Jobs.All.Where(j=>j.Id!="original"))
{
    var independent=new Campaign();
    independent.Select(2,job.Id,null,true); independent.Award(2,310);
    independent.Select(1,job.Id,null,true); independent.Award(1,10);
    Check(independent.Character(1).Points(job.Id)==10 && independent.Character(2).Points(job.Id)==310,"Independent ABP for every job: "+job.Id);
}
var ownMagic=new Campaign(); ownMagic.StoryFlags.UnionWith(new[]{13,20,28,43,68});
ownMagic.Select(4,"black_mage",null); ownMagic.Award(4,310); ownMagic.Select(1,"black_mage",null);
Check(!SpellProgression.CanLearn(ownMagic,34,ownMagic.Character(1).Points("black_mage")),"Mastered Rydia cannot unlock Cecil's Fira");
ownMagic.Award(1,30);
Check(SpellProgression.CanLearn(ownMagic,34,ownMagic.Character(1).Points("black_mage")),"Cecil's own 30 ABP unlocks Fira after Fabul");
var summons=new Campaign(); summons.StoryFlags.UnionWith(new[]{28,46});
summons.LearnedSummons.UnionWith(new[]{65,66,75});
Check(summons.IsUnlocked("summoner") && !new Campaign().IsUnlocked("summoner"),"Summoner unlocks at adult Rydia's return milestone");
Check(SummonProgression.CanUse(summons,65,0,false) && !SummonProgression.CanUse(summons,65,9,true) && SummonProgression.CanUse(summons,65,10,true),"Chocobo is usable at main 0 / sub 10 Summoner ABP");
Check(!SummonProgression.CanUse(summons,66,69,false) && SummonProgression.CanUse(summons,66,70,false),"Shiva requires this character's 70 ABP");
Check(!SummonProgression.CanUse(summons,75,150,true) && SummonProgression.CanUse(summons,75,310,true),"Bahamut requires mastery even when story learned");
Check(!SummonProgression.CanUse(summons,73,310,false),"ABP never grants an unearned story summon");
var earnedLater=new Campaign(); earnedLater.StoryFlags.UnionWith(new[]{28,46});
earnedLater.Select(1,"summoner",null); earnedLater.Award(1,310);
Check(!SummonProgression.CanUse(earnedLater,75,earnedLater.Character(1).Points("summoner"),false),"Mastery before the story does not grant Bahamut early");
earnedLater.LearnedSummons.Add(75);
Check(SummonProgression.CanUse(earnedLater,75,earnedLater.Character(1).Points("summoner"),false),"Story acquisition immediately unlocks a summon for an already trained character without another reward");
earnedLater.Select(2,"summoner",null);
Check(!SummonProgression.CanUse(earnedLater,75,earnedLater.Character(2).Points("summoner"),false),"One Summoner's mastery cannot advance another character");
earnedLater.Award(2,310);
Check(SummonProgression.CanUse(earnedLater,75,earnedLater.Character(2).Points("summoner"),false),"Training after story acquisition also unlocks immediately");
foreach(int id in Enumerable.Range(61,15))
{
    summons.LearnedSummons.Add(id); int required=SummonProgression.RequiredAbp(id);
    Check(SummonProgression.CanUse(summons,id,required,false),"Summon own-ABP threshold "+id);
    if(required>0) Check(!SummonProgression.CanUse(summons,id,required-1,false),"Summon threshold remains locked below required ABP "+id);
}
var speed = new FastForwardSpeed();
Check(BloodCasting.Applies(1,false) && !BloodCasting.Applies(13,false) && !BloodCasting.Applies(1,true),"Blood casting ends on Paladin identity or milestone");
foreach(int status in Enumerable.Range(2,12)) Check(!BloodCasting.Applies(status,false),"Blood casting excludes other characters");
Check(BloodCasting.Cost(5)==20 && BloodCasting.Cost(15)==60,"HP spells cost four times MP");
Check(BloodCasting.RemainingHp(100,5)==80 && BloodCasting.RemainingHp(20,5)==1 && BloodCasting.RemainingHp(10,5)==1,"Spell cost stops at one HP");
Check(BloodCasting.RemainingHp(1,5)==1 && BloodCasting.RemainingHp(0,5)==0,"HP cost neither kills nor revives");
Check(BloodCasting.Cost(0)==0 && BloodCasting.Cost(-1)==0 && BloodCasting.RemainingHp(50,0)==50,"Free spells remain free");
Check(BloodCasting.Cost(int.MaxValue)==int.MaxValue && BloodCasting.RemainingHp(100,int.MaxValue)==1,"Large HP cost does not overflow");
Check(speed.Update(1,true,8)==8,"Fast-forward accelerates normal gameplay");
Check(speed.Update(8,true,8)==8,"Held fast-forward does not compound each frame");
Check(speed.Update(8,false,8)==1,"Release restores original speed");
Check(speed.Update(.5f,true,8)==4 && speed.Update(4,false,8)==.5f,"Preserves a pre-existing non-default speed");
Check(speed.Update(1,true,8)==8 && speed.Update(0,true,8)==0 && speed.Update(0,false,8)==0,"A game pause wins over fast-forward and release");
Check(speed.Update(1,true,8)==8 && speed.Update(2,false,8)==2,"Release preserves an external speed change");
Check(speed.Update(1,true,8)==8 && speed.Update(8,false,1)==1,"Scene/modal transition restores normal speed");
Check(speed.Update(1,true,100)==16 && speed.Update(16,false,1)==1,"Multiplier bounded to 16x");
// Ranger completion: progression is personal, slot-specific, and story gated.
var rangerCampaign=new Campaign(); rangerCampaign.StoryFlags.Add(17);
rangerCampaign.Select(1,"ranger",null); rangerCampaign.Award(1,150);
Check(!rangerCampaign.Commands(1,new[]{1,3}).Contains(JobFeatures.RapidFire),"Rapid Fire cannot be ground out before Zot");
rangerCampaign.StoryFlags.Add(43);
Check(rangerCampaign.Commands(1,new[]{1,3}).Contains(JobFeatures.RapidFire),"Ranger main gains Rapid Fire at 150 after Zot");
rangerCampaign.Select(1,"original","ranger");
Check(!rangerCampaign.Commands(1,new[]{1,3}).Contains(JobFeatures.RapidFire),"Rapid Fire sub requires mastery");
rangerCampaign.Character(1).Abp["ranger"]=310;
Check(rangerCampaign.Commands(1,new[]{1,3}).Contains(JobFeatures.RapidFire),"Mastered Ranger sub grants Rapid Fire");
Check(!rangerCampaign.Commands(4,new[]{1,3}).Contains(JobFeatures.RapidFire),"Ranger training does not spread to Rydia");
Check(JobFeatures.RapidFireDamage(100)==50 && JobFeatures.RapidFireDamage(1)==1 && JobFeatures.RapidFireDamage(0)==0,"Rapid Fire halves damage without turning misses into hits");
Check(JobFeatures.RapidFireDamage(-100)==-100,"Rapid Fire leaves non-damage result signs alone");
// Red Mage: individual mastery, one charge, and combined spell affordability.
var red=new Campaign();red.StoryFlags.Add(43);red.Select(1,"red_mage",null);red.Award(1,309);
Check(!red.Commands(1,new[]{1,3}).Contains(JobFeatures.Dualcast),"Dualcast requires full Red Mage mastery");
red.Award(1,1);
Check(red.Commands(1,new[]{1,3}).Contains(JobFeatures.Dualcast),"Mastered Red Mage main unlocks Dualcast");
red.Select(1,"original","red_mage");
Check(red.Commands(1,new[]{1,3}).Contains(JobFeatures.Dualcast),"Mastered Red Mage sub unlocks Dualcast");
Check(!red.Commands(4,new[]{1,3}).Contains(JobFeatures.Dualcast),"Dualcast mastery belongs to its character");
Check(JobFeatures.CanPayPair(20,5,15) && !JobFeatures.CanPayPair(19,5,15),"Second spell must fit remaining MP after first selection");
Check(!JobFeatures.CanPayPair(int.MaxValue,int.MaxValue,int.MaxValue),"Combined spell cost cannot overflow");
Check(JobFeatures.DualcastCharge(3,8)==8 && JobFeatures.DualcastCharge(8,3)==8,"Dualcast charges once for the slower selected spell");
Check(JobFeatures.DualcastCharge(0,0)==0,"Instant spells do not gain an artificial charge");
// Ninja training controls the passive; the main job still controls weapon types.
var ninja=new CharacterProgress{Main="ninja"};ninja.Abp["ninja"]=69;
Check(!JobFeatures.HasPassive(ninja,"ninja"),"Ninja main cannot dual wield before 70 ABP");
ninja.Abp["ninja"]=70;
Check(JobFeatures.HasPassive(ninja,"ninja"),"Ninja main learns Dual Wield at 70 ABP");
ninja.Main="red_mage";ninja.Sub="ninja";ninja.Abp["ninja"]=149;
Check(!JobFeatures.HasPassive(ninja,"ninja"),"Ninja sub cannot dual wield before 150 ABP");
ninja.Abp["ninja"]=150;
Check(JobFeatures.HasPassive(ninja,"ninja"),"Ninja sub learns Dual Wield at 150 ABP");
ninja.Sub=null;
Check(!JobFeatures.HasPassive(ninja,"ninja"),"Unequipping trained Ninja removes its passive");
Check(!JobFeatures.HasPassive(new CharacterProgress(),"ninja"),"Ninja training is personal");
Check(JobFeatures.DualWieldWeapon(5,false) && !JobFeatures.DualWieldWeapon(5,true),"Dual Wield requires one-handed weapons");
foreach(int category in new[]{12,13,43}) Check(!JobFeatures.DualWieldWeapon(category,false),"Bows, arrows and empty hands are not dual-wield weapons");
Check(!Balance.WeaponAllowed("red_mage",4,4),"Ninja sub does not grant katana equipment to Red Mage");
foreach(bool dual in new[]{false,true})
{
    Check(!HandEquipment.PairAllowed(12,12,false,false,dual,false),"Two bows are invalid even with Dual Wield");
    Check(!HandEquipment.PairAllowed(13,13,false,false,dual,false),"Two arrow stacks are invalid even with Dual Wield");
    Check(HandEquipment.PairAllowed(12,13,false,false,dual,false) && HandEquipment.PairAllowed(13,12,false,false,dual,false),"Bow and arrows work in either hand");
    Check(!HandEquipment.PairAllowed(12,5,false,false,dual,false) && !HandEquipment.PairAllowed(13,-1,false,false,dual,false),"Bow and arrow cannot pair with unrelated gear");
}
Check(HandEquipment.PairAllowed(5,4,false,false,true,false) && !HandEquipment.PairAllowed(5,4,false,false,false,false),"Two weapons require Dual Wield");
Check(!HandEquipment.PairAllowed(5,4,true,false,true,false),"Two-handed weapon cannot pair with another weapon");
Check(HandEquipment.PairAllowed(12,0,false,false,false,false),"Empty offhand allows assembling a bow and arrow pair");
foreach(var job in Jobs.All)
    Check(HandEquipment.PairAllowed(-1,-1,false,false,true,HandEquipment.AllowsShields(job.Id))==(job.Id is "paladin" or "engineer" or "dragoon"),"Defensive hand pairing restricted to selected main job: "+job.Id);
var monk=new CharacterProgress{Main="monk"};
Check(JobFeatures.HasPassive(monk,"monk"),"Monk main starts with Barehand");
monk.Main="original";monk.Sub="monk";monk.Abp["monk"]=69;
Check(!JobFeatures.HasPassive(monk,"monk"),"Monk sub needs 70 ABP for Barehand");
monk.Abp["monk"]=70;
Check(JobFeatures.HasPassive(monk,"monk"),"Monk sub gains Barehand at 70 ABP");
Check(JobFeatures.UnarmedPower(20,40,0)==52 && JobFeatures.UnarmedPower(20,40,70)==58,"Barehand uses own level and strength plus own Monk rank");
Check(JobFeatures.UnarmedPower(21,40,70)-JobFeatures.UnarmedPower(20,40,70)==2,"Own level growth improves barehand power");
Check(JobFeatures.UnarmedPower(20,40,310)==62 && JobFeatures.UnarmedPower(20,40,int.MaxValue)==62,"Barehand rank bonus caps at mastery");
Check(JobFeatures.UnarmedPower(int.MaxValue,int.MaxValue,int.MaxValue)==234,"Barehand arithmetic remains bounded");
monk.Sub=null;
Check(!JobFeatures.HasPassive(monk,"monk"),"Unmastered Barehand is lost when unequipped");
Console.WriteLine($"PASS: {checks} progression, save, and fast-forward checks.");
