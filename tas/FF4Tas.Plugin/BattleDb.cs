using System.Text.Json;

namespace FF4Tas;

// Monster and spell facts from research/tas-battle/battle.json (tas/battle_db.py,
// built from the game's own master tables), copied into the session by tas.py.
internal sealed class BattleDb
{
    internal sealed class Monster
    {
        public string Name {get;set;}="";
        public int Hp {get;set;}
        public int Defense {get;set;}
        public bool Undead {get;set;}
        public bool Boss {get;set;}
        public bool Flying {get;set;}
        public int[] WeakIds {get;set;}=Array.Empty<int>();
    }
    internal sealed class Spell
    {
        public string Name {get;set;}="";
        public int Mp {get;set;}
        public string? Element {get;set;}
        public bool CanTargetAll {get;set;}
    }
    public Dictionary<int,Monster> Monsters {get;set;}=new();
    public Dictionary<int,Spell> Spells {get;set;}=new();
    // Encounter groups: monster set -> [cumulative per-mille end, party id];
    // party -> monster ids; map -> its monster set (dungeons).
    public Dictionary<int,int[][]> Sets {get;set;}=new();
    public Dictionary<int,int[]> Parties {get;set;}=new();
    public Dictionary<int,int> MapSets {get;set;}=new();
    // Party a Random.Range(1,1001) roll picks from a monster set (0 = none).
    internal int Group(int set,int roll)
    {
        if(!Sets.TryGetValue(set,out var rows))return 0;
        foreach(var r in rows)if(r.Length==2 && roll<=r[0])return r[1];
        return 0;
    }
    internal static BattleDb Load(string path)
    {
        try {return File.Exists(path)?JsonSerializer.Deserialize<BattleDb>(File.ReadAllText(path),Plugin.Json)??new():new();}
        catch {return new();}
    }
    internal string MonsterName(int id)=>Monsters.TryGetValue(id,out var m) && m.Name.Length>0?m.Name:"#"+id;
    internal string SpellName(int id)=>Spells.TryGetValue(id,out var s) && s.Name.Length>0?s.Name:"#"+id;
    internal int SpellMp(int id,int fallback)=>Spells.TryGetValue(id,out var s)?s.Mp:fallback;
}
