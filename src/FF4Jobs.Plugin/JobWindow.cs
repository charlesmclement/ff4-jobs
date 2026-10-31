using FF4Jobs.Core;
using Last.Data.User;
using Last.Management;
using UnityEngine;
using NativeInput=Il2CppSystem.Input.InputSystemManager;
using ActionType=Il2CppSystem.Input.InputSystemManager.InputActionType;

namespace FF4Jobs;

public sealed class JobWindow : MonoBehaviour
{
    private enum Page { Characters, Slot, Jobs }
    private Page page;
    private int selected, slot, cursor;
    private float nextSync, inputReady, repeatAt;
    private int heldDirection;
    private bool open;
    private OwnedCharacterData[] party=Array.Empty<OwnedCharacterData>();
    private List<Core.Job?> choices=new();
    private Last.UI.KeyInput.MainMenuController? nativeMenu;
    private GameObject? root;
    internal static bool IsOpen,RequestOpen;
    public JobWindow(IntPtr pointer):base(pointer) { }
    private bool CanOpen()
    {
        if(!Plugin.Enabled || State.BrokenSave || State.GamePhase!=SubSceneManagerMainGame.State.Menu) return false;
        if(nativeMenu==null) nativeMenu=UnityEngine.Object.FindObjectOfType<Last.UI.KeyInput.MainMenuController>();
        return nativeMenu!=null && nativeMenu.IsOpne && !nativeMenu.IsLoadingSave
            && nativeMenu.stateMachine?.Current==Last.Defaine.MenuCommandId.Job;
    }
    private void Close()
    {
        open=false; IsOpen=false; RequestOpen=false; Hide();
        if(nativeMenu==null) return;
        nativeMenu.SetEnableCommandClick(true);
        if(nativeMenu.stateMachine?.Current==Last.Defaine.MenuCommandId.Job)
        {
            nativeMenu.nextState=Last.Defaine.MenuCommandId.Non;
            nativeMenu.stateMachine.Change(Last.Defaine.MenuCommandId.Non);
        }
    }
    private void Hide() { if(root!=null) { root.SetActive(false); UnityEngine.Object.Destroy(root); root=null; } }
    private void OnDestroy() => Close();
    private void Update()
    {
        try { UpdateJobs(); }
        catch(Exception e) { Plugin.Logger.LogError("Jobs menu recovered from error: "+e); Close(); }
    }
    private void UpdateJobs()
    {
        if(Time.unscaledTime>=nextSync)
        {
            nextSync=Time.unscaledTime+.25f;
            Projection.Sync(); PartyClassLabels.Refresh(); Diagnostics.Poll(); ReleaseProbe.Poll(); BloodMagicProbe.Poll(); CombatMenuTest.Poll();
        }
        if(open && !CanOpen()) { Close(); return; }
        if(RequestOpen && CanOpen())
        {
            RequestOpen=false; var data=UserDataManager.Instance(); var ids=data.GetMemberIndexList();
            var members=new List<OwnedCharacterData>();
            for(int i=0;i<ids.Count;i++) { var c=data.GetMemberData(ids[i]); if(c!=null && c.characterStatusId is >=1 and <=13) members.Add(c); }
            party=members.ToArray(); if(party.Length==0) { Close(); return; }
            selected=Math.Clamp(selected,0,party.Length-1); page=Page.Characters;
            open=true; IsOpen=true; nativeMenu!.SetEnableCommandClick(false);
            MenuSkin.Capture(nativeMenu); inputReady=Time.unscaledTime+.2f; Build();
            Plugin.Logger.LogInfo("Jobs character selection opened."); return;
        }
        if(!open || Time.unscaledTime<inputReady) return;
        var input=NativeInput.Instance;
        if(input==null) return;
        if(MenuInput.Down(ActionType.Cancel) || Input.GetKeyDown(KeyCode.Escape)) { Back(); return; }
        int direction=MenuInput.Held(ActionType.Down) || MenuInput.Held(ActionType.Right)?1:
            MenuInput.Held(ActionType.Up) || MenuInput.Held(ActionType.Left)?-1:0;
        if(direction!=heldDirection) { heldDirection=direction; repeatAt=0; }
        if(direction!=0 && Time.unscaledTime>=repeatAt)
        {
            bool first=repeatAt==0; Move(direction); repeatAt=Time.unscaledTime+(first?.32f:.13f);
        }
        if(MenuInput.Down(ActionType.Enter)) Confirm();
    }
    private void Move(int direction)
    {
        if(page==Page.Characters) selected=(selected+direction+party.Length)%party.Length;
        else if(page==Page.Slot) slot=1-slot;
        else if(choices.Count>0) cursor=(cursor+direction+choices.Count)%choices.Count;
        Build();
    }
    private void Back()
    {
        if(page==Page.Characters) { Close(); return; }
        page=page==Page.Jobs?Page.Slot:Page.Characters;
        inputReady=Time.unscaledTime+.12f; Build();
    }
    private void Confirm()
    {
        if(page==Page.Characters) { page=Page.Slot; slot=0; }
        else if(page==Page.Slot) { page=Page.Jobs; cursor=0; AvailableJobs(); }
        else { if(choices.Count==0) return; Choose(choices[cursor]); page=Page.Slot; }
        inputReady=Time.unscaledTime+.12f; Build();
    }
    private void AvailableJobs()
    {
        var p=State.Campaign.Character(party[selected].characterStatusId);
        choices=new List<Core.Job?>(); if(slot==1) choices.Add(null);
        foreach(var job in Jobs.All)
        {
            if(!State.Campaign.IsAvailable(party[selected].characterStatusId,job.Id,Plugin.TestAllJobs.Value)) continue;
            if(slot==1 && (job.Id=="original" || job.Id==p.Main)) continue;
            choices.Add(job);
        }
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void Choose(Core.Job? job)
    {
        var c=party[selected]; var p=State.Campaign.Character(c.characterStatusId);
        string main=slot==0?job!.Id:p.Main; string? sub=slot==1?job?.Id:p.Sub;
        if(main==sub) sub=null;
        State.Campaign.Select(c.characterStatusId,main,sub,Plugin.TestAllJobs.Value,Plugin.TestAllSkills.Value);
        State.Changed(); EquipmentRules.Reconcile(c); Projection.Apply(c); PartyClassLabels.Refresh();
        c.Parameter.CurrentMP=Math.Min(c.Parameter.CurrentMP,c.Parameter.ConfirmedMaxMp());
        State.Notice=$"{c.Name}: {PartyClassLabels.Description(c)}. Save to keep this choice.";
        Plugin.Logger.LogInfo(State.Notice);
    }
    private void Build()
    {
        Hide(); root=MenuSkin.Root(nativeMenu!); var parent=root.transform;
        MenuSkin.Label(parent,"Jobs",.035f,.025f,.35f,.065f,52);
        string test="Jobs: "+(Plugin.TestAllJobs.Value?"All":"Story")+"\nSkills: "+(Plugin.TestAllSkills.Value?"All":"Earned");
        MenuSkin.Label(parent,test,.73f,.015f,.24f,.09f,28);
        MenuSkin.Label(parent,"Confirm: A / Enter       Back: B / Backspace       Move: D-pad / Stick",.04f,.91f,.92f,.055f,30);
        if(page==Page.Characters)
        {
            MenuSkin.Label(parent,"Choose a character",.04f,.105f,.85f,.065f,42);
            float row=Math.Min(.135f,.68f/party.Length);
            for(int i=0;i<party.Length;i++)
            {
                int index=i; float y=.19f+i*row;
                MenuSkin.Choice(parent,"",.04f,y,.92f,row-.01f,selected==i,()=>{selected=index;Confirm();});
                MenuSkin.Portrait(parent,PartyClassLabels.Portrait(party[i].characterStatusId),.12f,y+.01f,.08f,row-.03f);
                MenuSkin.Label(parent,party[i].Name,.23f,y,.22f,row-.01f,38);
                MenuSkin.Label(parent,PartyClassLabels.Description(party[i]),.46f,y,.47f,row-.01f,34);
            }
            return;
        }
        var c=party[selected]; var p=State.Campaign.Character(c.characterStatusId);
        MenuSkin.Portrait(parent,PartyClassLabels.Portrait(c.characterStatusId),.04f,.105f,.08f,.135f);
        MenuSkin.Label(parent,c.Name+"   "+PartyClassLabels.Description(c),.14f,.11f,.8f,.09f,40);
        if(page==Page.Slot)
        {
            MenuSkin.Choice(parent,"Main Job     "+PartyClassLabels.MainName(c),.08f,.28f,.84f,.10f,slot==0,()=>{slot=0;Confirm();},44);
            DrawProgress(parent,p.Main,p,false,.1f,.39f,.8f);
            MenuSkin.Choice(parent,"Sub Job         "+(p.Sub==null?"None":Jobs.Get(p.Sub).Name),.08f,.53f,.84f,.10f,slot==1,()=>{slot=1;Confirm();},44);
            if(p.Sub!=null) DrawProgress(parent,p.Sub,p,true,.1f,.64f,.8f);
            MenuSkin.Label(parent,"Main Job: full ABP. Sub Job: 25% ABP, even with Original as main.",.09f,.79f,.82f,.09f,30);
            return;
        }
        MenuSkin.Label(parent,slot==0?"Choose Main Job":"Choose Sub Job",.04f,.235f,.6f,.07f,40);
        int start=cursor/7*7;
        for(int i=start;i<Math.Min(start+7,choices.Count);i++)
        {
            int index=i; var job=choices[i]; string name=job==null?"None":job.Id=="original"?"Original class":job.Name;
            MenuSkin.Choice(parent,name,.04f,.32f+(i-start)*.071f,.51f,.066f,cursor==i,()=>{cursor=index;Confirm();},40);
        }
        MenuSkin.Label(parent,$"{(choices.Count==0?0:cursor+1)} / {choices.Count} available",.04f,.835f,.5f,.055f,30);
        if(choices.Count==0) return;
        var current=choices[cursor];
        if(current==null) { MenuSkin.Label(parent,"Remove the sub job.",.6f,.33f,.34f,.2f,36); return; }
        DrawProgress(parent,current.Id,p,slot==1,.6f,.32f,.34f);
        MenuSkin.Label(parent,current.Description+"\n"+(slot==0?Balance.EquipmentHint(current.Id)+"\n"+Balance.StatHint(current):"Sub Job: learned skills only; no equipment or stat bonus."),.6f,.47f,.35f,.22f,24);
        var lines=new List<string>(); int points=p.Points(current.Id);
        for(int i=0;i<current.Commands.Length;i++)
        {
            int required=current.CommandRequirement(i,slot==1);
            lines.Add($"{CommandName(current.Commands[i])}: "+(points>=required?"Learned":$"{AbpText(p.ExactPoints(current.Id))}/{required} ABP"));
        }
        if(slot==1 && points<10 && !Plugin.TestAllSkills.Value) lines.Add("Sub trains at 25%; main trains faster.");
        if(current.IsCaster) lines.Add("Advanced spells need ABP and story progress.");
        MenuSkin.Label(parent,string.Join("\n",lines),.6f,.71f,.34f,.17f,24);
    }
    private static string AbpText(decimal points) => points.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
    private static void DrawProgress(Transform parent,string jobId,CharacterProgress progress,bool sub,float x,float y,float width)
    {
        if(jobId=="original") { MenuSkin.Label(parent,"Original class does not earn ABP.",x,y,width,.07f,28); return; }
        int points=progress.Points(jobId); decimal exact=progress.ExactPoints(jobId);
        var job=Jobs.Get(jobId); int next=Jobs.NextThreshold(points);
        string label=next==0?$"{points} ABP - Mastered":$"{AbpText(exact)} / {next} ABP - Rank {Jobs.Rank(points)}";
        MenuSkin.Label(parent,label,x,y,width,.05f,30);
        MenuSkin.ProgressBar(parent,x,y+.058f,width,.014f,next==0?1:(float)exact/next);
        string detail=next==0?"All ABP ranks earned.":$"{AbpText(next-exact)} ABP to next rank";
        for(int i=0;i<job.Commands.Length;i++)
        {
            int required=job.CommandRequirement(i,sub);
            if(points<required) { detail=$"{CommandName(job.Commands[i])} in {AbpText(required-exact)} ABP"; break; }
        }
        MenuSkin.Label(parent,detail,x,y+.08f,width,.06f,28);
    }
    private static string CommandName(int id) => id switch
    {
        JobFeatures.RapidFire=>"Rapid Fire",
        7=>"White Magic",8=>"Black Magic",9=>"Summon",10=>"Darkness",11=>"Jump",12=>"Pray",13=>"Aim",14=>"Cover",
        15=>"Ninjutsu",16=>"Throw",17=>"Steal",20=>"Bluff",22=>"Sing",23=>"Heal",25=>"Focus",26=>"Kick",27=>"Gird",31=>"Study",_=>id.ToString()
    };
}
