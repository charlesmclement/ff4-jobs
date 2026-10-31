using FF4Jobs.Core;
using HarmonyLib;
using Last.Data.Master;
using Last.Management;

namespace FF4Jobs;

// Runtime master entries only. Nothing from the game's master files is bundled
// or written to disk. Projection restores native command lists before saving.
internal static class CustomCommands
{
    internal static Command Get(int id)
    {
        var master=MasterManager.Instance;
        if(id is not (JobFeatures.RapidFire or JobFeatures.Dualcast)) return master.GetData<Command>(id);
        var table=master.GetList<Command>();
        if(table.ContainsKey(id)) return table[id];
        bool dual=id==JobFeatures.Dualcast;
        var source=master.GetData<Command>(dual?7:1);
        string key=dual?"FF4JOBS_DUALCAST":"FF4JOBS_RAPID_FIRE";
        var command=new Command {
            Id=id, CommandLv=source.CommandLv, MesIdName=key,
            MesIdName2=key, MesIdDescription=key+"_HELP",
            ConsumeType=source.ConsumeType,CommandType=source.CommandType,
            ExecutionType=source.ExecutionType,ListType=source.ListType,
            NumberSelect=dual?2:source.NumberSelect,AbilityGroupId=source.AbilityGroupId,
            AbilityId=source.AbilityId,JobGroupId=source.JobGroupId,
            ConditionGroupId=source.ConditionGroupId,ReleaseType=0,
            ReleaseValue1=0,ReleaseValue2=0,CommandWait=source.CommandWait,
            AutoExecutionAbilityId=source.AutoExecutionAbilityId
        };
        table[id]=command;
        return command;
    }
    internal static string? Text(string key) => key switch {
        "FF4JOBS_RAPID_FIRE"=>"Rapid Fire",
        "FF4JOBS_RAPID_FIRE_HELP"=>"Four half-damage attacks against random living enemies.",
        "FF4JOBS_PASSIVE_monk"=>"Barehand",
        "FF4JOBS_PASSIVE_ninja"=>"Dual Wield",
        "FF4JOBS_DUALCAST"=>"Dualcast",
        "FF4JOBS_DUALCAST_HELP"=>"Select two learned white or black spells and their targets. Each spell pays its normal cost.",
        _=>null
    };
}

[HarmonyPatch(typeof(MessageManager),nameof(MessageManager.GetMessage),new[]{typeof(string),typeof(bool)})]
internal static class CustomCommandText
{
    static bool Prefix(string key,ref string __result)
    {
        string? text=CustomCommands.Text(key);
        if(text==null) return true;
        __result=text;return false;
    }
}
