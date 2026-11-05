using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Technobabble;

internal static partial class Checks {
    static ProcessRow Proc(int id,string name,double cpu,long memory,long started=10){return new ProcessRow{Id=id,Name=name,Cpu=cpu,Memory=memory,Started=started};}
    static Snapshot Processes(params ProcessRow[] rows){return new Snapshot{Processes=rows};}
    static void ProcessChecks(){
        var watch=new ProcessWatch();
        ProcessRow a=Proc(1,"Alpha",70,100),b=Proc(2,"Beta",10,900),c=Proc(3,"gamma",30,200);
        var sample=Processes(a,b,c);watch.Update(sample);
        Check(watch.Selected.Id==1 && watch.CpuHistory.Length==1,"Initial process selection/history missing");
        watch.Update(sample);Check(watch.CpuHistory.Length==1,"Rendering appended duplicate history");
        watch.CycleSort();Check(watch.Rows[0].Id==2 && watch.Selected.Id==1,"Memory sort lost selected identity");
        watch.CycleSort();Check(watch.Rows.Select(p=>p.Id).SequenceEqual(new[]{1,2,3}),"Name sort wrong");
        watch.Update(Processes(Proc(1,"Alpha",1,100),Proc(2,"Beta",90,900),c));
        Check(watch.Selected.Id==1 && watch.CpuHistory.Length==2,"Ranking change lost selection/history");
        watch.Move(1);Check(watch.Selected.Id==2 && watch.CpuHistory.Length==0,"Changing selection mixed history");
        watch.Update(Processes(a,b,c));Check(watch.CpuHistory.Length==1,"Selected process history did not restart");
        watch.Update(Processes(a,Proc(2,"Beta",1,1,20),c));
        Check(!watch.Present && watch.Selected.Started==10 && watch.CpuHistory.Length==1,"Reused PID inherited selection/history");
        watch.Move(1);Check(watch.Present,"Navigation did not recover after selected process exit");
        watch.Configure(ProcessSort.Cpu,"bEtA");watch.Update(Processes(a,b,c));Check(watch.Rows.Single().Id==2,"Case-insensitive filter failed");
        watch.Configure(ProcessSort.Cpu,"3");Check(watch.Rows.Single().Id==3,"PID filter failed");
        watch.Configure(ProcessSort.Name,"missing");Check(watch.Rows.Length==0 && watch.Selected==null,"Empty filter retained selected process");
        watch.Configure(ProcessSort.Cpu,"");
        for(int i=0;i<150;i++)watch.Update(Processes(a,b,c));
        Check(watch.CpuHistory.Length==120 && watch.MemoryHistory.Length==120,"Process history is unbounded");
        sample=Processes(a,b,c);sample.RefreshSeconds=5;watch.Update(sample);Check(watch.CpuHistory.Length==1,"Rate change mixed history scales");
        watch.Reset();watch.Update(Processes(Proc(9,"Unknown",2,4,0)));
        Check(watch.CpuHistory.Length==0,"Unverified process identity gained history");
        watch.Update(Processes(Proc(9,"Unknown",2,4,30)));Check(watch.Present && watch.CpuHistory.Length==1,"Delayed process identity was not adopted");
        watch.Reset();watch.Update(Processes(Enumerable.Range(1,50).Select(i=>Proc(i,"p"+i,100-i,i)).ToArray()));
        watch.End(true);Check(watch.Start(5)==45,"Scroll did not keep last selection visible");
        watch.End(false);Check(watch.Start(5)==0,"Home did not return to first page");
        watch.ClickRow(4);Check(watch.Selected.Id==5,"Mouse row selection failed");
        Check(!ProcessWatch.ValidFilter("bad\nfilter") && !ProcessWatch.ValidFilter(new string('a',65)),"Unsafe or oversized filter accepted");
        Console.WriteLine("Process sorting, filtering, stable selection, PID reuse, exit recovery, scrolling and bounded histories passed.");
    }
    static NetworkReading Nic(string id,long rx,long tx,bool up=true,bool valid=true){return new NetworkReading{Id=id,Name="Adapter "+id,Up=up,Valid=valid,Received=rx,Sent=tx};}
    static void NetworkChecks(){
        var sampler=new NetworkSampler();var first=new Snapshot();
        sampler.Sample(first,new[]{Nic("a",1000,200),Nic("b",5000,400)},NetworkChoice.Automatic,2);
        Check(first.NicId=="b" && !first.NetworkReady,"Automatic selection or initial baseline incorrect");
        var next=new Snapshot();sampler.Sample(next,new[]{Nic("a",1200,300),Nic("b",5600,600)},new NetworkChoice("a","Adapter a"),2);
        Check(next.Rx==100 && next.Tx==50 && next.NicId=="a","Explicit adapter rates incorrect");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("a",1400,400),Nic("b",6000,800)},new NetworkChoice("b","Adapter b"),2);
        Check(next.Rx==200 && next.Tx==100,"Switching adapters mixed counter baselines");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("a",1500,450),Nic("b",6000,800,false)},new NetworkChoice("b","Adapter b"),2);
        Check(!next.NetworkReady && next.NetworkStatus.Contains("disconnected"),"Disconnected adapter appeared active");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("a",1600,500)},new NetworkChoice("b","Saved name"),2);
        Check(!next.NetworkReady && next.Nic=="Saved name" && next.NicId=="b","Missing chosen adapter silently fell back");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("b",1,1)},new NetworkChoice("b","Adapter b"),2);
        Check(!next.NetworkReady,"Reconnection reused an old baseline");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("b",0,0)},new NetworkChoice("b","Adapter b"),2);
        Check(!next.NetworkReady,"Counter reset produced a fabricated rate");
        next=new Snapshot();sampler.Sample(next,new[]{Nic("a",2000,700,false),Nic("b",200,100)},NetworkChoice.Automatic,2);
        Check(next.NetworkReady && next.NicId=="b" && next.Rx==100,"Connected adapter recovery failed");
        Console.WriteLine("Network adapter selection, independent rates, disconnect/reconnect, missing adapters and counter resets passed.");
    }
    static void StudioUpgrades(Machine host,Pictures images){
        ProcessChecks();NetworkChecks();ModeRestoration(host,images);
        var dir=Path.Combine(Path.GetTempPath(),"studio18-"+Guid.NewGuid().ToString("N"));
        var store=new LayoutStore(dir);var scene=new Scene(20,10,new Random(81),host,images);var board=new Studio(scene,store);
        try{
            scene.Resize(209,53);board.Load(LayoutStore.ProcessExample);
            Check(scene.Root.A.Widget.Name=="process" && scene.Root.A.Pinned && scene.Root.Ratio==65 && host.StudioActive,"Process Watch example incorrect");
            board.ProcessKey(ConsoleKey.O);board.OpenFilter();board.SetName("bé ta");board.Submit();
            Check(scene.Processes.Sort==ProcessSort.Memory && scene.Processes.Filter=="bé ta","Process controls did not apply");
            scene.SetNetwork(new NetworkChoice("nic-123","Ethernet 中文"));
            board.ToggleResize();Check(board.Resizing && board.ResizeDivider==scene.Root,"Nearest divider incorrect");
            board.ResizeKey(ConsoleKey.RightArrow,false);Check(scene.Root.Ratio==70,"Keyboard resize failed");
            board.ResizeKey(ConsoleKey.Escape,false);Check(!board.Resizing,"Escape failed to leave resize mode");
            int dividerX=scene.Root.A.Bounds.Right;
            Check(scene.DividerAt(new Point(dividerX,2))==scene.Root,"Divider hit testing failed");
            scene.DragDivider(scene.Root,new Point(104,3));Check(scene.Root.Ratio==50,"Mouse resize coordinate conversion failed");
            scene.SetRatio(scene.Root,65);
            var pins=scene.Root.Leaves().Where(p=>p.Pinned).ToArray();
            var original=pins.Select(p=>p.Bounds).ToArray();scene.Shuffle();for(int i=0;i<20;i++)scene.Advance(.2);
            Check(scene.Root.Ratio==65 && pins.Select(p=>p.Bounds).SequenceEqual(original),"Pinned divider changed during shuffle");
            foreach(int ratio in new[]{10,30,50,65,90})foreach(var size in new[]{new Size(209,53),new Size(80,23),new Size(24,8),new Size(1,1)}){
                scene.SetRatio(scene.Root,ratio);scene.Resize(size.Width,size.Height);
                var leaves=scene.Root.Leaves().Where(p=>p.Bounds.Width>0 && p.Bounds.Height>0).ToArray();
                Check(leaves.All(p=>p.Bounds.Width>=0 && p.Bounds.Height>=0 && scene.Root.Bounds.Contains(p.Bounds) && !leaves.Any(q=>q!=p && q.Bounds.IntersectsWith(p.Bounds))),"Resized pane bounds overlap or escape");
#if TERMINAL
                TerminalScreen.Paint(scene,size.Width,size.Height+1,true,false,false,board);
#endif
            }
            scene.Resize(209,53);scene.SetRatio(scene.Root,65);string saved=scene.LayoutText();
            store.Save("All upgrades",saved,false);board.Leave();board.Load("All upgrades");
            Check(scene.LayoutText()==saved && scene.Network.Id=="nic-123" && scene.Processes.Filter=="bé ta" && scene.Processes.Sort==ProcessSort.Memory,"Version 3 settings did not round trip");
            foreach(string invalid in new[]{saved.Replace("V:65","V:0"),saved.Replace("V:65","V:101"),saved.Replace("V:65","V:abc"),saved+"extra",saved.Replace("1 YsOpIHRh","9 YsOpIHRh"),saved.Replace("YsOpIHRh","%%%")}){
                bool rejected=false;try{scene.RestoreLayout(invalid);}catch(InvalidDataException){rejected=true;}
                Check(rejected && scene.LayoutText()==saved,"Invalid extended layout mutated state");
            }
            board.OpenNetwork();Check(board.Choices.Any(n=>n.Contains("Ethernet 中文") && n.Contains("unavailable")),"Missing saved adapter hidden in picker");board.Cancel();
            board.Load(LayoutStore.SystemExample);
            Check(scene.Network.Id=="" && scene.Processes.Filter=="" && scene.Processes.Sort==ProcessSort.Cpu && scene.LayoutText().StartsWith("Hollywood Studio 1\n"),"Old layout inherited Studio preferences");
            board.ProcessKey(ConsoleKey.C);Check(board.ProcessSelected,"Process pane shortcut failed");
            board.Leave();
            Check(!host.StudioActive && host.Network.Id=="" && scene.RefreshSeconds==2 && scene.Processes.Filter=="" && scene.Dividers.All(p=>p.Ratio==50) && scene.Root.Leaves().All(p=>!p.Pinned && Array.IndexOf(Widget.Names,p.Widget.Name)<20),"Studio additions leaked into Classic");
            Console.WriteLine("Version 3 layouts, Unicode preferences, mouse/keyboard divider sizing, pin stability, tiny windows, old layouts and Classic isolation passed.");
        }finally{board.Leave();var file=Path.Combine(dir,"All upgrades.layout");if(File.Exists(file))File.Delete(file);if(Directory.Exists(dir))Directory.Delete(dir);}
    }
    static void ModeRestoration(Machine host,Pictures images){
        var dir=Path.Combine(Path.GetTempPath(),"studio-mode-"+Guid.NewGuid().ToString("N"));
        var store=new LayoutStore(dir);var scene=new Scene(20,10,new Random(41),host,images);var board=new Studio(scene,store);
        try{
            scene.Resize(209,53);string classic=scene.LayoutText();board.Enter();
            Check(scene.LayoutText()==classic && host.StudioActive,"First Studio entry changed the Classic arrangement");
            board.Load(LayoutStore.ProcessExample);scene.SetRatio(scene.Root,75);scene.SetRefresh(5);
            scene.Processes.Configure(ProcessSort.Memory,"technobabble");scene.SetNetwork(new NetworkChoice("nic-id","Saved adapter"));
            scene.SelectNext(1);string saved=scene.LayoutText(),selected=scene.Selected.Widget.Name;
            for(int pass=0;pass<3;pass++){
                board.Leave();board.Leave();
                Check(!host.StudioActive && scene.RefreshSeconds==2 && scene.Network.Id=="" && scene.Processes.Filter=="" && scene.Dividers.All(p=>p.Ratio==50),"Suspended settings leaked into Classic");
                Check(scene.Root.Leaves().All(p=>!p.Pinned && Array.IndexOf(Widget.Names,p.Widget.Name)<20),"Suspended Studio panes leaked into Classic");
                scene.Shuffle();for(int i=0;i<80;i++)scene.Advance(.2);scene.Resize(pass==0?121:209,pass==0?29:53);
                board.Enter();
                Check(scene.LayoutText()==saved && scene.Selected.Widget.Name==selected && board.ActiveName==LayoutStore.ProcessExample,"Studio layout or selection not restored after Classic activity");
                Check(host.StudioActive && scene.RefreshSeconds==5 && scene.Network.Id=="nic-id" && scene.Processes.Filter=="technobabble","Restored Studio settings missing");
                var root=scene.Root;board.Enter();Check(Object.ReferenceEquals(root,scene.Root),"Repeated Enter restarted Studio");
            }
            Check(!Directory.Exists(dir),"Mode switching wrote a saved profile");
            board.Leave();board.Load(LayoutStore.SystemExample);string replacement=scene.LayoutText();
            board.Enter();Check(scene.LayoutText()==replacement,"Old suspended layout replaced an explicitly opened layout");
            board.Leave();board.Enter();Check(scene.LayoutText()==replacement && board.ActiveName==LayoutStore.SystemExample,"Newly opened layout was not remembered");
            board.Leave();
            var fresh=new Scene(20,10,new Random(7),host,images);var newBoard=new Studio(fresh,store);string initial=fresh.LayoutText();newBoard.Enter();
            Check(fresh.LayoutText()==initial && newBoard.ActiveName==null,"A new session loaded a suspended layout");newBoard.Leave();
            Console.WriteLine("Studio mode round trips preserve unsaved layouts, pins, sizes, refresh, process preferences, adapter and pane selection; Classic and new sessions stay isolated.");
        }finally{board.Leave();}
    }
}
