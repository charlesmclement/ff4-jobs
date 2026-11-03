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
    // Gear by content id (tas/battle_db.py): K 1 weapon / 2 armor, A attack,
    // D defense, M magic defense, E evasion, B stat bonuses, C weapon category
    // (12 bow, 13 arrow, 18 claw), P armor part (2 shield), X element, T two-handed.
    internal sealed class GearRow
    {
        public int K {get;set;}public int A {get;set;}public int D {get;set;}public int M {get;set;}
        public int E {get;set;}public int B {get;set;}public int C {get;set;}public int P {get;set;}
        public int X {get;set;}public int T {get;set;}
    }
    public Dictionary<int,GearRow> Gear {get;set;}=new();
    // character_status id -> dominant arm: 1 right, 2 left, 3 both (Yang, Edge).
    public Dictionary<int,int> Arms {get;set;}=new();
    // The last database loaded (the equipment pass reads gear from it).
    internal static BattleDb Current=new();
    // Party a Random.Range(1,1001) roll picks from a monster set (0 = none).
    internal int Group(int set,int roll)
    {
        if(!Sets.TryGetValue(set,out var rows))return 0;
        foreach(var r in rows)if(r.Length==2 && roll<=r[0])return r[1];
        return 0;
    }
    internal static BattleDb Load(string path)
    {
        try {return Current=File.Exists(path)?JsonSerializer.Deserialize<BattleDb>(File.ReadAllText(path),Plugin.Json)??new():new();}
        catch {return new();}
    }
    internal string MonsterName(int id)=>Monsters.TryGetValue(id,out var m) && m.Name.Length>0?m.Name:"#"+id;
    internal string SpellName(int id)=>Spells.TryGetValue(id,out var s) && s.Name.Length>0?s.Name:"#"+id;
    internal int SpellMp(int id,int fallback)=>Spells.TryGetValue(id,out var s)?s.Mp:fallback;
}
