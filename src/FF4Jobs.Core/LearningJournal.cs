using System;
using System.Collections.Generic;
using System.Linq;

namespace FF4Jobs.Core;

public sealed class LearningNotice
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
}

// Optional in older saves. A character's first observation seeds a baseline;
// subsequent progress reports only new skills, including separate sub-job use.
public sealed class LearningJournal
{
    public HashSet<string> SeenCharacters { get; set; } = new();
    public Dictionary<string,HashSet<string>> Known { get; set; } = new();
    public List<LearningNotice> Pending { get; set; } = new();

    public string[] Observe(int status, string job, IEnumerable<string> available, bool announce)
    {
        string character=Jobs.CharacterKey(status), key=character+"/"+job;
        if(!Known.TryGetValue(key,out var known)) Known[key]=known=new HashSet<string>();
        var added=available.Where(id=>!known.Contains(id)).Distinct().ToArray();
        known.UnionWith(added);
        return announce && SeenCharacters.Contains(character) ? added : Array.Empty<string>();
    }
    public void FinishCharacter(int status) => SeenCharacters.Add(Jobs.CharacterKey(status));
    public void Validate()
    {
        if(SeenCharacters==null || Known==null || Pending==null || Known.Any(p=>p.Value==null)
            || Pending.Any(n=>n==null || n.Title==null || n.Message==null))
            throw new InvalidOperationException("Invalid skill announcement data.");
    }
}
