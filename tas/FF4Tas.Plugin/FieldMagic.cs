using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;
using AbilityWindow=Serial.FF4.UI.KeyInput.AbilityWindowController;

namespace FF4Tas;

// Out-of-battle healing with magic before items (guide: spells refill, items
// cost gil): Life on a fallen member, else Cure on the lowest member under the
// threshold, cast by Rosa, then Rydia, Tellah, Porom, FuSoYa. Keys only: Menu -> Magic ->
// caster -> White Magic -> spell -> target, then Back to the field. When no
// one can cast it finishes and the item heal (FieldMenu) takes over.
internal sealed class FieldMagic
{
    private const int MenuMagic=2,WhiteMagicCommand=7,Cure=6,Life=11,Esuna=10,CureMp=3,LifeMp=8,EsunaMp=20,AfterCast=48;
    // AbilityWindowController.State (Command) and AbilityUseContentListController.State (All).
    private const int CommandState=4,UseAll=2;
    // Paladin Cecil, Rosa, Rydia, Porom, FuSoYa, then Tellah (Knows() skips
    // anyone without the spell). Cecil first: he has no use for MP in battle
    // (user). Tellah last: his MP is for bosses (live: the heal after Baigan
    // spent his last 20 MP on Cures while Porom had 83, and he met Cagnazzo
    // with none for Thundaga).
    private static readonly int[] Casters={13,3,4,10,12,6};
    private float topUpBelow=.7f;
    // RNG steering casts one Cure, never Tellah's, and keeps the caster's MP reserve.
    private int maxCasts=12,keepMp;private bool tellah=true;
    private bool finished,flip;
    // Backs without a cast: give up on magic (items take over), never loop.
    private int casts,wait,stuck,backs,lastIndex=-1,caster,spell,target,columns,targetPresses;
    private const int MaxBacks=10;
    private string? lastKey;
    private MainMenuController? main;
    internal void Reset(float topUp,int casts=12,bool allowTellah=true,int keepMpAbove=0) {maxCasts=casts;tellah=allowTellah;keepMp=keepMpAbove;topUpBelow=topUp;finished=flip=false;casts=wait=stuck=backs=columns=targetPresses=casterShift=0;lastIndex=-1;lastKey=null;main=null;}
    internal bool Wanted() {Need();return spell!=0;}
    internal void Read(Snapshot s,int frame)
    {
        Need();
        if(finished && s.Phase=="Player") {s.MenuDone=true;s.Detail="Magic: done";return;}
        if(wait>0) {wait--;s.Detail="Magic: casting";return;}
        if(spell==0 || casts>=maxCasts)finished=true;
        if(s.Phase=="Player")
        {
            if(finished) {s.MenuDone=true;return;}
            Pulse(s,frame,"Menu","Open the menu to cast");return;
        }
        if(s.Phase!="Menu")return;
        if(finished) {Pulse(s,frame,"Back","Close the menu");return;}
        if(main==null || !main)main=UnityEngine.Object.FindObjectOfType<MainMenuController>();
        if(main?.stateMachine==null)return;
        int menuState=(int)main.stateMachine.Current;
        if(menuState==0)
        {
            var commands=main.commandMenuController;
            if(commands?.contents==null || commands.selectCursor==null)return;
            int want=-1;
            for(int i=0;i<commands.contents.Count;i++)if(commands.contents[i]?.Data!=null && (int)commands.contents[i].Data.Id==MenuMagic) {want=i;break;}
            if(want<0) {finished=true;return;}
            Move(s,frame,commands.selectCursor.Index,want,false,"Magic");return;
        }
        if(menuState!=MenuMagic) {Pulse(s,frame,"Back","Leave this submenu");return;}
        var window=UnityEngine.Object.FindObjectOfType<AbilityWindow>();
        int state=window!=null && window.gameObject.activeInHierarchy && window.stateMachine!=null?(int)window.stateMachine.Current:0;
        if(state==0)
        {
            // Party list: pick the caster.
            var status=UnityEngine.Object.FindObjectOfType<StatusWindowController>();
            var cursor=status?.selectCursor;
            if(cursor==null)return;
            // The row that shows the caster (character id, never a slot
            // count). If the list can't be read, step through the rows: each
            // time the opened window shows someone else, the next row is tried.
            int row=PartyList.RowOf(status,caster);
            if(row<0) {int rows=Math.Max(1,PartyList.Rows(status));row=casterShift%rows;}
            Move(s,frame,cursor.Index,row,false,"Caster #"+caster);return;
        }
        // The window shows someone else: back to the party list.
        if(window!.commandController?.targetCharacter?.characterStatusId is int shown && shown!=caster)
        {
            if(frame%12==0) {casterShift++;if(casterShift>8)finished=true;}
            Pulse(s,frame,"Back",$"Other caster (#{shown}); next guess");return;
        }
        // The state numbers don't say which panel is up (after White Magic it
        // reported UseList 2 while the spell list was open, and the step looped
        // White Magic -> Back): use the command state, then whichever panel is
        // actually open - the target panel, else the spell list.
        if(state==CommandState)
        {
            var c=window.commandController;
            if(c?.contentList==null || c.selectCursor==null)return;
            int want=-1;
            for(int i=0;i<c.contentList.Count;i++)if(c.contentList[i]?.Command?.Id==WhiteMagicCommand) {want=i;break;}
            if(want<0) {finished=true;Pulse(s,frame,"Back","No White Magic");return;}
            Move(s,frame,c.selectCursor.Index,want,true,"White Magic");return;
        }
        var u=window.useController;
        if(u!=null && u.gameObject.activeInHierarchy && u.targetAbility!=null && u.targetSelectList!=null && u.targetSelectList.Count>0 && u.selectCursor!=null)
        {
            if(u.targetAbility?.Ability?.Id is int chosen && chosen!=spell) {if(frame%12==0 && ++backs>=MaxBacks)finished=true;Pulse(s,frame,"Back","Switch spell");return;}
            if(u.stateMachine!=null && (int)u.stateMachine.Current==UseAll) {Cast(s,frame);return;}
            bool listed=false;int members=0;
            for(int i=0;i<u.targetSelectList.Count;i++) {int id=u.targetSelectList[i]?.Id??0;if(id!=0)members++;if(id==target)listed=true;}
            if(!listed) {if(frame%12==0 && ++backs>=MaxBacks)finished=true;Pulse(s,frame,"Back","Target not in the list");return;}
            // Read who the cursor is on (TargetCursor), not a list index: the
            // list keeps empty entries the cursor skips. Down wraps; two laps
            // without landing and magic gives up (items take over).
            int under=TargetCursor.Under(u.currentContent,u.selectCursor,u.contentList);
            if(under==target) {targetPresses=0;Cast(s,frame);return;}
            if(frame%12==0 && ++targetPresses>2*members+4) {finished=true;Pulse(s,frame,"Back","Can't reach #"+target+"; giving up");return;}
            Pulse(s,frame,"Down","Target #"+target+" (cursor on #"+under+")");return;
        }
        var l=window.listController;
        if(l?.abilityList!=null && l.selectCursor!=null && l.abilityList.Count>0)
        {
            int want=-1;
            for(int i=0;i<l.abilityList.Count;i++)if(l.abilityList[i]?.Ability?.Id==spell) {want=i;break;}
            if(want<0) {finished=true;Pulse(s,frame,"Back","Spell not learned");return;}
            Move(s,frame,l.selectCursor.Index,want,true,(spell==Life?"Life":spell==Esuna?"Esuna":"Cure")+$" (state {state}, cursor {l.selectCursor.Index} -> {want})");return;
        }
        s.Detail=$"Magic: waiting (window state {state})";
    }
    private void Cast(Snapshot s,int frame)
    {
        if(frame%12==0) {casts++;backs=0;wait=AfterCast;}
        Pulse(s,frame,"Confirm",(spell==Life?"Life":spell==Esuna?"Esuna":"Cure")+" on #"+target);
    }
    // Who casts what on whom right now (owned-character id as the target).
    private void Need()
    {
        caster=spell=target=0;
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        if(corps==null || owned==null)return;
        // poisoned: anyone with a lasting status (not only Poison).
        int dead=0,hurt=0,poisoned=0;float lowest=topUpBelow;
        var members=new List<Last.Data.User.OwnedCharacterData>();
        for(int c=0;c<corps.Count;c++)
        {
            int id=corps[c]?.CharacterId??0;if(id==0)continue;
            for(int o=0;o<owned.Count;o++)if(owned[o]?.Id==id && owned[o].Parameter!=null) {members.Add(owned[o]);break;}
        }
        foreach(var m in members)
        {
            var p=m.Parameter;
            if(p.CurrentHP<=0) {if(dead==0 || m.characterStatusId==3)dead=m.Id;continue;}
            float ratio=p.CurrentHP/(float)Math.Max(1,p.ConfirmedMaxHp());
            if(ratio<lowest) {lowest=ratio;hurt=m.Id;}
            if(poisoned==0 && Status(m)!=0)poisoned=m.Id;
        }
        foreach(int who in Casters)
        {
            var m=members.Find(x=>x.characterStatusId==who);
            if(m==null || m.Parameter.CurrentHP<=0 || Muted(m))continue;
            int mp=m.Parameter.CurrentMP;
            if(dead!=0 && mp>=LifeMp && Knows(m,Life)) {caster=who;spell=Life;target=dead;return;}
        }
        // A lasting status (Poison drains HP as he walks; Tellah Mini'd by
        // Baron's guards): Esuna clears all of them, else FieldMenu's item
        // for that status (user: "look into each status effect and what
        // cures it").
        if(poisoned!=0)
            foreach(int who in Casters)
            {
                var m=members.Find(x=>x.characterStatusId==who);
                if(m==null || m.Parameter.CurrentHP<=0 || Muted(m))continue;
                if(m.Parameter.CurrentMP>=EsunaMp+keepMp && Knows(m,Esuna)) {caster=who;spell=Esuna;target=poisoned;return;}
            }
        if(hurt==0)return;
        foreach(int who in Casters)
        {
            var m=members.Find(x=>x.characterStatusId==who);
            if(m==null || m.Parameter.CurrentHP<=0)continue;
            if(who==6 && !tellah)continue;
            if(Muted(m))continue;
            if(m.Parameter.CurrentMP>=CureMp+keepMp && Knows(m,Cure)) {caster=who;spell=Cure;target=hurt;return;}
        }
    }
    // Statuses that last after the fight, and what clears each out of battle
    // (master function groups: Esuna and Remedy clear condition group 76;
    // the items clear one each). Sleep, Paralyze and Confuse end with the fight.
    // Silence 6, Darkness 9, Poison 10, Petrify 11 / gradual 17, Mini 13/57,
    // Toad 14/58, Pig 15/59, Curse 18.
    internal static readonly int[] Lasting={6,9,10,11,13,14,15,17,18,57,58,59};
    // The first lasting status on a member, else 0.
    internal static int Status(Last.Data.User.OwnedCharacterData m)
    {
        var list=m.Parameter?.CurrentConditionList;
        for(int i=0;list!=null && i<list.Count;i++)if(list[i]?.Id is int id && Array.IndexOf(Lasting,id)>=0)return id;
        return 0;
    }
    // Every condition id on a member from both lists, for the log.
    internal static string Describe(Last.Data.User.OwnedCharacterData m)
    {
        var sb=new System.Text.StringBuilder(m.Name).Append(" [");
        var list=m.Parameter?.CurrentConditionList;
        for(int i=0;list!=null && i<list.Count;i++)sb.Append(list[i]?.Id).Append(',');
        return sb.Append(']').ToString();
    }
    // Silence, Toad and Pig stop spells; Mini still casts.
    private static bool Muted(Last.Data.User.OwnedCharacterData m)=>Status(m) is 6 or 14 or 15 or 58 or 59;
    private static bool Knows(Last.Data.User.OwnedCharacterData m,int ability)
    {
        var list=m.OwnedAbilityList;
        for(int i=0;list!=null && i<list.Count;i++)if(list[i]?.Ability?.Id==ability)return true;
        return false;
    }
    // Party slot of a character_status id (corps order, empty slots skipped).
    private int casterShift;
    // Up/Down; grids and sideways rows flip to Left/Right when a key leaves
    // the cursor in place three times.
    // The spell list is a grid: Down jumps a whole row (0 -> 3 -> wraps to 0,
    // and Life at 5 was never reached). A Down that moves more than one slot
    // gives the row width; then Up/Down picks the row and Left/Right the column.
    private void Move(Snapshot s,int frame,int at,int want,bool canFlip,string what)
    {
        if(want<0) {finished=true;return;}
        if(frame%12==0 && lastKey=="Down" && at-lastIndex>1 && lastIndex>=0)columns=at-lastIndex;
        bool sideways=canFlip && flip;
        string key=at==want?"Confirm":
            canFlip && columns>1?(want/columns!=at/columns?(want<at?"Up":"Down"):(want<at?"Left":"Right")):
            sideways?(want<at?"Left":"Right"):(want<at?"Up":"Down");
        if(frame%12==0 && key!="Confirm")
        {
            if(key==lastKey && at==lastIndex && ++stuck>=3) {flip=!flip;stuck=0;}
            else if(at!=lastIndex)stuck=0;
            lastKey=key;lastIndex=at;
        }
        Pulse(s,frame,key,what);
    }
    private static void Pulse(Snapshot s,int frame,string key,string detail)
    {
        s.Detail="Magic: "+detail;
        if(frame%12==0)s.MenuInput=key;
    }
}
