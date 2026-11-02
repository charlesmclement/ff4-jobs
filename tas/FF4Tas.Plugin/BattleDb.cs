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
    internal static BattleDb Load(string path)
    {
        try {return File.Exists(path)?JsonSerializer.Deserialize<BattleDb>(File.ReadAllText(path),Plugin.Json)??new():new();}
        catch {return new();}
    }
    internal string MonsterName(int id)=>Monsters.TryGetValue(id,out var m) && m.Name.Length>0?m.Name:"#"+id;
    internal string SpellName(int id)=>Spells.TryGetValue(id,out var s) && s.Name.Length>0?s.Name:"#"+id;
    internal int SpellMp(int id,int fallback)=>Spells.TryGetValue(id,out var s)?s.Mp:fallback;
}
