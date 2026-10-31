using FF4Jobs.Core;
using Last.Data.Master;
using Last.Data.User;
using Last.Management;

namespace FF4Jobs;

internal static class SkillUnlockNotifications
{
    private static Campaign? owner;
    private static int generation=-1;
    private static string partyKey="";
    private static List<(Ability Ability, string Name)>? spells;

    internal static void Collect()
    {
        if(Plugin.TestAllJobs.Value || Plugin.TestAllSkills.Value) return;
        var data=UserDataManager.Instance();
        if(data==null || MessageManager.Instance==null || MasterManager.Instance==null) return;
        var members=new List<OwnedCharacterData>();
        foreach(int index in data.GetMemberIndexList())
        {
            var c=data.GetMemberData(index);
            if(c!=null && c.characterStatusId is >=1 and <=13) members.Add(c);
        }
        string key=string.Join(",",members.Select(c=>Jobs.CharacterKey(c.characterStatusId)));
        if(owner==State.Campaign && generation==State.Generation && partyKey==key) return;
        if(spells==null)
        {
            spells=new(); var seen=new HashSet<int>();
            foreach(var pair in MasterManager.Instance.GetList<Content>())
            {
                var content=pair.Value;
                if(content.TypeId!=4 || !content.MesIdName.StartsWith("MSG_MAGIC_NAME_",StringComparison.Ordinal)
                    || !SpellProgression.IsPlayerSpell(content.TypeValue) || !seen.Add(content.TypeValue)) continue;
                var ability=MasterManager.Instance.GetData<Ability>(content.TypeValue);
                if(ability!=null) spells.Add((ability,content.MesIdName));
            }
        }
        foreach(var c in members)
        {
            var progress=State.Campaign.Character(c.characterStatusId);
            var native=Projection.OriginalAbilities(c);
            foreach(var job in Jobs.All)
            {
                if(job.Id=="original" || !State.Campaign.IsAvailable(c.characterStatusId,job.Id)) continue;
                int points=progress.Points(job.Id);
                var names=new Dictionary<string,string>();
                foreach(bool sub in new[]{false,true})
                {
                    string prefix=sub?"s/":"m/";
                    foreach(int id in job.LearnedCommands(points,sub))
                    {
                        var command=MasterManager.Instance.GetData<Command>(id);
                        if(command!=null) names[prefix+"command/"+id]=command.MesIdName;
                    }
                    foreach(var spell in spells)
                        if(Projection.Allows(job,spell.Ability,points,sub,native))
                            names[prefix+"spell/"+spell.Ability.Id]=spell.Name;
                }
                bool trained=points>0 || progress.Main==job.Id || progress.Sub==job.Id;
                var added=State.Campaign.Learning.Observe(c.characterStatusId,job.Id,names.Keys,trained);
                var lines=added.GroupBy(id=>id.Substring(2)).Select(group=>
                {
                    var ids=group.ToArray();
                    string slots=ids.Length>1?"Main + Sub":ids[0].StartsWith("s/")?"Sub Job":"Main Job";
                    string name=System.Text.RegularExpressions.Regex.Replace(
                        MessageManager.Instance.GetMessage(names[ids[0]],true),"<[^>]+>","");
                    return name+" ("+slots+")";
                }).ToArray();
                // Small pages fit the native popup without shrinking the font.
                for(int i=0;i<lines.Length;i+=3)
                {
                    var notice=new LearningNotice { Title="Skills / spells unlocked!",
                        Message=c.Name+" / "+job.Name+"\n"+string.Join("\n",lines.Skip(i).Take(3)) };
                    State.Campaign.Learning.Pending.Add(notice);
                    Plugin.Logger.LogInfo("Skill unlock: "+notice.Message.Replace('\n',';'));
                }
            }
            State.Campaign.Learning.FinishCharacter(c.characterStatusId);
        }
        owner=State.Campaign; generation=State.Generation; partyKey=key;
    }
}
