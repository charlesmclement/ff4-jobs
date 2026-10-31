using System;
using System.Collections.Generic;
using System.Linq;

namespace FF4Jobs.Core;

public enum JobStat { Strength, Vitality, Agility, Intelligence, Spirit }

// Derive benefits from saved personal ABP. Never rewrite native level-up stats:
// reloading, switching jobs and upgrading an old save cannot apply growth twice.
public static class Mastery
{
    public static int BaseBonus(Job job,JobStat stat) => stat switch {
        JobStat.Strength=>job.Strength,JobStat.Vitality=>job.Vitality,JobStat.Agility=>job.Agility,
        JobStat.Intelligence=>job.Intelligence,JobStat.Spirit=>job.Spirit,_=>0
    };
    public static IEnumerable<Job> Earned(Campaign campaign,int status,bool allJobs=false)
    {
        var p=campaign.Character(status);
        return Jobs.All.Where(j=>j.Id!="original" && p.Points(j.Id)>=310 && campaign.IsAvailable(status,j.Id,allJobs));
    }
    public static bool HasAny(Campaign campaign,int status,bool allJobs=false) => Earned(campaign,status,allJobs).Any();
    public static bool HasCaster(Campaign campaign,int status,bool allJobs=false) => Earned(campaign,status,allJobs).Any(j=>j.IsCaster);
    public static int ActiveBonus(Job job,JobStat stat,int points)
    {
        int value=BaseBonus(job,stat);
        return value>0?value+Jobs.Rank(Math.Clamp(points,0,310)):value;
    }
    public static int Bonus(Campaign campaign,int status,JobStat stat,bool allJobs=false,string? previewMain=null)
    {
        var p=campaign.Character(status);string main=previewMain??p.Main;int inherited=0;
        foreach(var mastered in Earned(campaign,status,allJobs))
        {
            int positive=Math.Max(0,BaseBonus(mastered,stat));
            int benefit=main=="original" ? (positive>0?positive+5:0) : (positive+3)/4;
            inherited=Math.Max(inherited,benefit);
        }
        return main=="original"?inherited:ActiveBonus(Jobs.Get(main),stat,p.Points(main))+inherited;
    }
}
