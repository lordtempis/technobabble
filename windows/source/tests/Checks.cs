using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading;
using Technobabble;

internal static partial class Checks {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Contents(Grid g){return String.Join("\n",g.Ch.Select(row=>new string(row)));}
    static void Scenes(Machine host,Pictures images) {
        for(int seed=0;seed<12;seed++){
            Scene s=new Scene(20,2,new Random(seed),host,images);s.Resize(200,58);
            for(int t=0;t<100 && s.Root.Leaves().Count()<20;t++)s.Advance(.06);
            var panes=s.Root.Leaves().ToArray();
            Check(panes.Length==20,"Pane count seed "+seed+": "+panes.Length);
            Check(panes.Select(p=>p.Widget.Name).Distinct().Count()==20,"Repeated widget");
            Check(panes.All(p=>Array.IndexOf(Widget.Names,p.Widget.Name)<Widget.ClassicCount),"Studio monitor entered a Classic shuffle");
            foreach(var p in panes){
                Check(p.Bounds.Width>=10 && p.Bounds.Height>=3,"Undersized pane");
                Check(!panes.Any(q=>q!=p && q.Bounds.IntersectsWith(p.Bounds)),"Overlapping panes");
            }
            var survivor=s.Root.BottomRight().Widget;int gen=s.Generation;
            for(int t=0;t<80 && s.Generation==gen;t++)s.Advance(.05);
            Check(s.Generation==gen+1 && s.Root.Leaves().Count()==1 && s.Root.Widget==survivor,"Timed shuffle must preserve bottom right");
            s.Resize(24,6);for(int t=0;t<100;t++)s.Advance(.06);
            foreach(var p in s.Root.Leaves())Check(p.Bounds.Width>0 && p.Bounds.Height>0,"Invalid small layout");
        }
        Scene single=new Scene(1,1,new Random(17),host,images);var first=single.Root.Widget;
        single.Advance(1.1);Check(single.Root.Widget==first && single.Root.Leaf,"Single pane shuffle");
        Console.WriteLine("12 randomized layouts, automatic rebuilds, survivors, small windows and one-pane mode passed.");
    }
    static void Layouts(Machine host,Pictures images){
        var dir=Path.Combine(Path.GetTempPath(),"technobabble-layout-"+Guid.NewGuid().ToString("N"));
        var store=new LayoutStore(dir);
        var scene=new Scene(10,2,new Random(31),host,images);
        scene.Resize(180,60);scene.RestoreLayout(store.Read(LayoutStore.Example));
        string original=scene.LayoutText();
        var graph=scene.Root.A;var graphWidget=graph.Widget;var bounds=graph.Bounds;
        Check(graph.Pinned && graphWidget.Name=="speedometer","Example left pane");
        var other=scene.Root.B.Leaves().First();scene.Select(other.Widget);scene.TogglePin();
        var otherBounds=other.Bounds;
        for(int round=0;round<6;round++){
            int start=scene.Generation;
            if(round%2==0)scene.Shuffle();
            while(scene.Generation==start)scene.Advance(1);
            for(int t=0;t<16;t++)scene.Advance(.21);
            Check(graph.Widget==graphWidget && graph.Bounds==bounds && other.Bounds==otherBounds && scene.Root.Leaves().Contains(other),"Pin moved or restarted");
            Check(scene.Root.Leaves().Select(p=>p.Widget.Name).Distinct().Count()==6,"Rebuild lost or duplicated widgets");
        }
        string snapshot=scene.LayoutText();
        try{
            store.Save("My board",snapshot,false);
            var copy=new Scene(20,10,new Random(3),host,images);
            copy.Resize(90,25);copy.RestoreLayout(store.Read("My board"));
            Check(copy.LayoutText()==snapshot,"Cross-size save/load mismatch");
            bool rejected=false;try{store.Save("My board",original,false);}catch(IOException){rejected=true;}
            Check(rejected && store.Read("My board")==snapshot,"Unexpected overwrite");
            store.Save("My board",original,true);Check(store.Read("My board")==original,"Atomic replacement failed");
            string existingDisk=Path.Combine(dir,LayoutStore.DiskExample+".layout");
            File.WriteAllText(existingDisk,original);
            try{
                Check(store.Read(LayoutStore.DiskExample)==original,"New built-in hid a pre-existing user layout");
                Check(store.Names().Count(n=>n==LayoutStore.DiskExample)==1,"Duplicate layout name");
            }finally{File.Delete(existingDisk);}
            foreach(var bad in new[]{"",snapshot.Replace("V *speedometer","V *unknown"),"Hollywood Studio 1\n4 2\nH *map map","Hollywood Studio 1\n1 5\nV map"}){
                rejected=false;try{copy.RestoreLayout(bad);}catch(InvalidDataException){rejected=true;}
                Check(rejected && copy.LayoutText()==snapshot,"Invalid layout changed live board");
            }
            rejected=false;try{store.Save("../outside",snapshot,false);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Unsafe layout name");
            foreach(var p in copy.Root.Leaves())p.Pinned=true;
            string all=copy.LayoutText();copy.Shuffle();Check(copy.LayoutText()==all,"All-pinned shuffle");
            copy.LeaveStudio();Check(copy.LayoutText().StartsWith("Hollywood Studio 1\n20 10\n") && !copy.Root.Leaves().Any(p=>p.Pinned),"Classic settings leaked");
        }finally{
            var file=Path.Combine(dir,"My board.layout");if(File.Exists(file))File.Delete(file);
            if(Directory.Exists(dir))Directory.Delete(dir);
        }
        Console.WriteLine("Pins survived manual and timed shuffles. Named layouts, overwrite, Classic switching and invalid-file recovery passed.");
    }
    static void SystemWatch(Machine host,Pictures images){
        var dir=Path.Combine(Path.GetTempPath(),"technobabble-system-"+Guid.NewGuid().ToString("N"));
        var store=new LayoutStore(dir);string layout=store.Read(LayoutStore.SystemExample);
        Check(store.Names().Contains(LayoutStore.SystemExample) && LayoutStore.IsExample(LayoutStore.SystemExample),"System Watch missing from layout picker");
        var scene=new Scene(20,10,new Random(42),host,images);scene.Resize(209,53);scene.RestoreLayout(layout);
        var pins=scene.Root.Leaves().Where(p=>p.Pinned).ToArray();
        Check(pins.Select(p=>p.Widget.Name).OrderBy(n=>n).SequenceEqual(new[]{"disk","gpu","htop","speedometer"}),"System Watch monitors or pins incorrect");
        Check(scene.Root.Leaves().Count()==6 && scene.Root.B.B.Leaves().All(p=>!p.Pinned),"Animation area not free to shuffle");
        Check(scene.Root.B.B.Bounds.Height<scene.Root.Bounds.Height/3,"Animation area is too large");
        foreach(var size in new[]{new Size(209,53),new Size(121,29),new Size(80,23),new Size(240,63)}){
            scene.Resize(size.Width,size.Height);var before=pins.Select(p=>p.Bounds).ToArray();
            for(int round=0;round<4;round++){
                int generation=scene.Generation;
                if(round%2==0)scene.Shuffle();else while(scene.Generation==generation)scene.Advance(.5);
                for(int i=0;i<20;i++)scene.Advance(.1);
                Check(pins.All(p=>scene.Root.Leaves().Contains(p)) && pins.Select(p=>p.Bounds).SequenceEqual(before),"System Watch monitor moved during a shuffle");
                var panes=scene.Root.Leaves().ToArray();
                Check(panes.Length==6 && panes.Select(p=>p.Widget.Name).Distinct().Count()==6,"System Watch lost or duplicated a pane");
                Check(panes.Where(p=>!p.Pinned).All(p=>scene.Root.B.B.Bounds.Contains(p.Bounds)),"Animation escaped the bottom strip");
                Check(panes.All(p=>p.Bounds.Width>0 && p.Bounds.Height>0 && !panes.Any(q=>q!=p && q.Bounds.IntersectsWith(p.Bounds))),"System Watch panes overlap");
                foreach(var pane in panes)pane.Widget.Frame(pane.Bounds.Width,Math.Max(1,pane.Bounds.Height-1),scene.Time);
            }
        }
        string saved=scene.LayoutText();scene.RestoreLayout(saved);Check(scene.LayoutText()==saved,"System Watch round trip changed layout");
        try{
            store.Save("System copy",saved,false);Check(store.Read("System copy")==saved,"Saving System Watch copy failed");
            string collision=Path.Combine(dir,LayoutStore.SystemExample+".layout");File.WriteAllText(collision,store.Read(LayoutStore.Example));
            Check(store.Read(LayoutStore.SystemExample)==store.Read(LayoutStore.Example) && store.Names().Count(n=>n==LayoutStore.SystemExample)==1,"Built-in hid an existing System Watch layout");
            File.Delete(collision);
        }finally{File.Delete(Path.Combine(dir,"System copy.layout"));if(Directory.Exists(dir))Directory.Delete(dir);}
        scene.LeaveStudio();Check(scene.Root.Leaves().All(p=>!p.Pinned && Array.IndexOf(Widget.Names,p.Widget.Name)<Widget.ClassicCount),"System Watch leaked into Classic");
        Console.WriteLine("System Watch: four fixed monitors, isolated animations, timed/manual shuffles, resizing, save/load and Classic isolation passed.");
    }
    static void Widgets(Machine host,Pictures images) {
        bool priorMode = host.StudioActive;
        var feed = new RelayFeed(0);
        for(int kind=0;kind<Widget.Names.Length;kind++){
            Widget widget=new Widget(kind,17,host,images,0);bool nonempty=false;
            host.StudioActive = widget.IsRelay || priorMode;
            foreach(var dim in new[]{new Size(1,1),new Size(10,3),new Size(47,10),new Size(48,11),new Size(82,23)}){
                for(int step=0;step<10;step++){
                    Grid g=widget.Frame(dim.Width,dim.Height,step*.37,CodeStyle.Scroll,feed);
                    Check(g.W==dim.Width && g.H==dim.Height,"Wrong dimensions");
                    nonempty|=Contents(g).Trim().Length>0;
                }
            }
            Check(nonempty,widget.Name+" never rendered content");
            Console.Write(widget.Name+" ");
        }
        host.StudioActive = priorMode;
        Console.WriteLine("\n"+Widget.Names.Length+" widgets rendered across five sizes.");
        var matrix=new Widget(Array.IndexOf(Widget.Names,"cmatrix"),23,host,images,0);
        Check(Contents(matrix.Frame(80,24,1))!=Contents(matrix.Frame(80,24,1.5)),"Matrix animation stopped");
        var wave=new Widget(Array.IndexOf(Widget.Names,"mplayer"),23,host,images,0);
        Check(Contents(wave.Frame(80,24,1))!=Contents(wave.Frame(80,24,1.5)),"Wave animation stopped");
    }
#if TERMINAL
    static void TerminalViews(Machine host,Pictures images){
        var input=new StreamInput(new StringReader("SIZE 209 54\nSIZE 0 9\nSIZE 2001 54\nKEY 9 2 9\nKEY 76 4 12\nKEY 999 0 1\nKEY 116 0 0\n"));
        for(int i=0;i<100 && !input.Closed;i++)Thread.Sleep(5);
        int cols,rows;input.Size(out cols,out rows);
        Check(input.Ready && cols==209 && rows==54,"Bridge accepted invalid dimensions");
        var key=input.ReadKey();Check(key.Key==ConsoleKey.Tab && key.Modifiers==ConsoleModifiers.Shift,"Bridge lost Shift+Tab");
        key=input.ReadKey();Check(key.Key==ConsoleKey.L && key.Modifiers==ConsoleModifiers.Control && key.KeyChar==12,"Bridge lost Ctrl+L");
        key=input.ReadKey();Check(key.Key==ConsoleKey.F5 && !input.KeyAvailable,"Bridge key filtering failed");
        var scene=new Scene(10,4,new Random(23),host,images);
        scene.Resize(160,54);
        for(int i=0;i<40;i++)scene.Advance(.08);
        Check(scene.Root.Leaves().Count()==10,"Expected ten panes for resize test");
        Grid previous=null;
        foreach(var size in new[]{new Size(160,55),new Size(1,1),new Size(13,4),new Size(80,24),new Size(160,55)}){
            scene.Resize(size.Width,size.Height-1);
            var screen=TerminalScreen.Paint(scene,size.Width,size.Height,true,false,false);
            Check(TerminalScreen.Update(screen,previous).StartsWith("\x1b[2J"),"Resize must clear old content");
            Check(TerminalScreen.Update(screen,screen)=="","Unchanged cells should produce no output");
            string repair=TerminalScreen.Update(screen,screen,true);
            Check(!repair.Contains("\x1b[2J") && repair.StartsWith("\x1b[1;1H") && repair.Contains("\x1b["+size.Height+";1H"),"Screen repair must repaint every row without a clear flash");
            previous=screen;
        }
        var help=TerminalScreen.Paint(scene,160,55,true,true,true);
        Check(Contents(help).Contains("[PAUSED]") && Contents(help).Contains("Close controls"),"Missing controls");
        var before=new Grid(20,3);var after=new Grid(20,3);
        after.Set(4,1,'\x1b',Ink.Red);
        string delta=TerminalScreen.Update(after,before);
        Check(delta.StartsWith("\x1b[2;5H") && delta.EndsWith(" "),"Unsafe data or incorrect positioning");
        Console.WriteLine("Terminal resize, differential updates, text sanitization and controls passed.");
    }
#endif
    static void DiskTests(Machine host,Pictures images){
        var read=new Dictionary<string,double?>{{"C:",1048576},{"D:",0}};
        var write=new Dictionary<string,double?>{{"C:",2097152},{"D:",0}};
        var sample=DiskSampler.Combine(read,write,new DiskSnapshot());
        Check(sample.Rows.Length==2 && sample.ReadHistory.Single()==1048576 && sample.WriteHistory.Single()==2097152,"Incorrect disk totals");
        var grid=new Grid(55,24);Widget.Disk(grid,sample,0);
        string shown=Contents(grid);
        Check(shown.Contains("C:") && shown.Contains("D:") && shown.Contains("1.0 MiB/s") && shown.Contains("2.0 MiB/s"),"Disk rates not rendered");
        read["C:"]=null;
        var missing=DiskSampler.Combine(read,write,sample);
        grid=new Grid(55,10);Widget.Disk(grid,missing,0);
        Check(Contents(grid).Contains("sampling") && missing.ReadHistory.Length==0,"Unavailable data shown as zero or stale graph");
        read.Remove("D:");write.Remove("D:");read["C:"]=0;
        var removed=DiskSampler.Combine(read,write,sample);
        Check(removed.Rows.Length==1 && removed.ReadHistory.Length==1,"Removed volume retained in history");
        var manyRead=new Dictionary<string,double?>();var manyWrite=new Dictionary<string,double?>();
        for(int i=0;i<12;i++){manyRead["Volume"+i]=i;manyWrite["Volume"+i]=i;}
        var many=DiskSampler.Combine(manyRead,manyWrite,new DiskSnapshot());
        var page1=new Grid(55,8);var page2=new Grid(55,8);
        Widget.Disk(page1,many,0);Widget.Disk(page2,many,10);
        Check(Contents(page1)!=Contents(page2),"Disk volume paging did not advance");
        var s=new Scene(20,10,new Random(7),host,images);s.Resize(160,55);
        var store=new LayoutStore(Path.GetTempPath());s.RestoreLayout(store.Read(LayoutStore.DiskExample));
        Check(s.Root.A.Pinned && s.Root.A.Widget.Name=="disk","Disk Watch did not pin activity pane");
        string saved=s.LayoutText();s.RestoreLayout(saved);Check(saved==s.LayoutText(),"Disk layout round trip");
        s.SelectNext(1);s.ShowDisk();Check(s.Selected.Widget.Name=="disk" && s.Root.Leaves().Count(p=>p.Widget.Name=="disk")==1,"Duplicate disk selection");
        s.LeaveStudio();Check(s.Root.Leaves().All(p=>p.Widget.Name!="disk"),"Disk leaked into Classic");
        for(int i=0;i<30;i++)s.Advance(.2);
        s.ShowDisk();Check(s.Selected.Widget.Name=="disk","Add disk to existing board failed");
        s.LeaveStudio();Check(s.Root.Leaves().All(p=>p.Widget.Name!="disk"),"Unpinned disk leaked into Classic");
        Console.WriteLine("Disk rates, unavailable samples, removal, paging, layout compatibility and Classic isolation passed.");
    }
    static string GpuKey(int pid,int engine,string type,string luid="0x00000000_0x00000001",int physical=0){
        return "pid_"+pid+"_luid_"+luid+"_phys_"+physical+"_eng_"+engine+"_engtype_"+type;
    }
    static void GpuTests(Machine host,Pictures images){
        var names=new Dictionary<string,string>{{"0x00000000_0x00000001","Test graphics adapter"}};
        var values=new Dictionary<string,double?>{{GpuKey(1,0,"3D"),25},{GpuKey(2,0,"3D"),30},{GpuKey(1,1,"Copy"),40}};
        var sample=GpuSampler.Combine(values,new GpuSnapshot(),names);
        Check(sample.Rows.Single().Usage==55 && sample.Rows[0].History.Single()==55,"GPU busiest-engine aggregation incorrect");
        var grid=new Grid(60,25);Widget.Gpu(grid,sample,0);
        Check(Contents(grid).Contains("55.0%") && Contents(grid).Contains("Test graphics adapter") && Contents(grid).Contains("0-100%"),"GPU rendering missing live values");
        var second=GpuSampler.Combine(values,sample,names);
        Check(second.Rows[0].History.Length==2,"GPU history not retained");
        values[GpuKey(1,0,"3D")]=90;
        Check(GpuSampler.Combine(values,sample,names).Rows[0].Usage==100,"GPU percentage not capped");
        values[GpuKey(1,0,"3D")]=null;
        var missing=GpuSampler.Combine(values,sample,names);grid=new Grid(60,25);Widget.Gpu(grid,missing,0);
        Check(!missing.Rows[0].Usage.HasValue && missing.Rows[0].History.Length==0 && Contents(grid).Contains("sampling"),"Missing GPU sample presented as zero or stale");
        foreach(double invalid in new[]{Double.NaN,Double.PositiveInfinity,-1}){
            values[GpuKey(1,0,"3D")]=invalid;
            Check(!GpuSampler.Combine(values,sample,names).Rows[0].Usage.HasValue,"Invalid GPU data accepted");
        }
        values.Remove(GpuKey(1,0,"3D"));
        Check(GpuSampler.Combine(values,missing,names).Rows[0].History.Length==1,"GPU history bridged a missing sample");
        values.Remove(GpuKey(2,0,"3D"));
        Check(GpuSampler.Combine(values,sample,names).Rows[0].History.Length==1,"GPU engine removal retained misleading history");
        values[GpuKey(1,0,"3D","0x00000000_0x00000002")]=70;
        values[GpuKey(1,0,"3D","0x00000000_0x00000001",1)]=15;
        values["malformed"]=99;
        var multi=GpuSampler.Combine(values,sample,names);
        Check(multi.Rows.Length==3 && multi.Rows.Select(r=>r.Usage.Value).OrderBy(n=>n).SequenceEqual(new double[]{15,40,70}),"GPU adapters or linked members merged");
        var filtered=GpuSampler.Combine(values,sample,names,new HashSet<string>{"0x00000000_0x00000002"});
        Check(filtered.Rows.Length==2 && filtered.Rows.All(r=>!r.Id.Contains("00000002")),"Software renderer was presented as hardware");
        var a=new Grid(40,10);var b=new Grid(40,10);Widget.Gpu(a,multi,0);Widget.Gpu(b,multi,10);
        Check(Contents(a)!=Contents(b),"GPU adapter paging stuck");
        for(int i=0;i<18;i++)values[GpuKey(1,i,"Copy")]=i;
        var many=GpuSampler.Combine(values,sample,names);
        for(int i=0;i<50;i++){Widget.Gpu(new Grid(1,1),many,i);Widget.Gpu(new Grid(18,5),many,i);}
        a=new Grid(40,10);b=new Grid(40,10);Widget.Gpu(a,many,0);Widget.Gpu(b,many,10);
        Check(Contents(a)!=Contents(b),"GPU engine paging stuck");
        values.Clear();Check(GpuSampler.Combine(values,sample,names).Rows.Length==0,"Removed GPU retained");
        var s=new Scene(20,10,new Random(9),host,images);s.Resize(160,55);
        var dir=Path.Combine(Path.GetTempPath(),"gpu-layout-"+Guid.NewGuid().ToString("N"));var store=new LayoutStore(dir);
        s.RestoreLayout(store.Read(LayoutStore.GpuExample));
        Check(s.Root.A.Pinned && s.Root.A.Widget.Name=="gpu","GPU Watch did not pin GPU");
        string saved=s.LayoutText();s.RestoreLayout(saved);Check(saved==s.LayoutText(),"GPU layout roundtrip failed");
        s.SelectNext(1);s.ShowGpu();Check(s.Selected.Widget.Name=="gpu" && s.Root.Leaves().Count(p=>p.Widget.Name=="gpu")==1,"Duplicate GPU selection");
        var pinned=s.Selected;var bounds=pinned.Bounds;s.Shuffle();for(int i=0;i<20;i++)s.Advance(.2);
        Check(s.Root.Leaves().Contains(pinned) && pinned.Bounds==bounds,"GPU pin lost on shuffle");
        s.LeaveStudio();Check(s.Root.Leaves().All(p=>p.Widget.Name!="gpu"),"GPU leaked into Classic");
        s.ShowGpu();s.ShowDisk();s.ShowGpu();s.LeaveStudio();
        Check(s.Root.Leaves().All(p=>Array.IndexOf(Widget.Names,p.Widget.Name)<20),"Unpinned monitor leaked into Classic");
        Directory.CreateDirectory(dir);string collision=Path.Combine(dir,LayoutStore.GpuExample+".layout");
        try{File.WriteAllText(collision,saved);Check(store.Read(LayoutStore.GpuExample)==saved && store.Names().Count(n=>n==LayoutStore.GpuExample)==1,"User GPU layout hidden");}
        finally{File.Delete(collision);Directory.Delete(dir);}
        Console.WriteLine("GPU aggregation, adapters, missing samples, history, paging, pins and Classic isolation passed.");
    }
    static void GpuMemoryTests(){
        const string luid="0x00000000_0x00000001",other="0x00000000_0x00000002";
        const string id="luid_"+luid+"_phys_0";
        double gib=1073741824;
        var d=new Dictionary<string,double?>(StringComparer.OrdinalIgnoreCase){{id,2*gib},{"luid_"+other+"_phys_0",gib}};
        var sh=new Dictionary<string,double?>(StringComparer.OrdinalIgnoreCase){{id.ToUpperInvariant(),gib/2}};
        d["pid_99_"+id]=99*gib;d["malformed"]=99*gib;
        var memory=GpuMemorySampler.Combine(d,sh);
        Check(memory.Count==2 && memory[luid+"/0"].Dedicated==2*gib && memory[luid+"/0"].Shared==gib/2,"GPU memory mapping or process-total filtering failed");
        var gpu=new GpuRow{Id=luid+"/0",Name="Test GPU",Usage=55,Engines=new[]{new GpuEngine{Id=0,Name="3D",Usage=55}},History=new[]{55.0}};
        var snapshot=new GpuSnapshot{Rows=new[]{gpu}};
        var capacity=new Dictionary<string,GpuCapacity>{{luid,new GpuCapacity{Dedicated=8*gib,Shared=16*gib}}};
        GpuMemorySampler.Apply(snapshot,memory,capacity);
        Check(gpu.Memory.DedicatedLimit==8*gib && gpu.Memory.SharedLimit==16*gib,"GPU memory limits missing");
        var screen=new Grid(60,25);Widget.Gpu(screen,snapshot,0);
        string text=Contents(screen);
        Check(text.Contains("Dedicated 2.0 GiB / 8.0 GiB") && text.Contains("Shared 512.0 MiB / 16.0 GiB"),"GPU memory usage not rendered");
        foreach(var size in new[]{new Size(1,1),new Size(10,5),new Size(24,9),new Size(40,15),new Size(104,53)})Widget.Gpu(new Grid(size.Width,size.Height),snapshot,0);
        var saved=gpu.Memory;
        d[id]=null;sh[id]=0;
        GpuMemorySampler.Apply(snapshot,GpuMemorySampler.Combine(d,sh),capacity);
        Check(!gpu.Memory.Dedicated.HasValue && gpu.Memory.Shared==0 && saved.Dedicated==2*gib,"Missing GPU memory became zero or mutated a previous sample");
        screen=new Grid(60,25);Widget.Gpu(screen,snapshot,0);text=Contents(screen);
        Check(text.Contains("Dedicated unavailable") && text.Contains("Shared 0 B") && text.Contains("55.0%"),"Memory failure interrupted engine reporting");
        foreach(double invalid in new[]{-1,Double.NaN,Double.PositiveInfinity}){d[id]=invalid;Check(!GpuMemorySampler.Combine(d,sh)[luid+"/0"].Dedicated.HasValue,"Invalid memory counter accepted");}
        memory[luid+"/1"]=new GpuMemory{Dedicated=gib};
        GpuMemorySampler.Apply(snapshot,memory,capacity);
        Check(!gpu.Memory.DedicatedLimit.HasValue && !gpu.Memory.SharedLimit.HasValue,"Logical-adapter limit assigned to linked physical GPU");
        GpuMemorySampler.Apply(snapshot,new Dictionary<string,GpuMemory>(),capacity);
        Check(!gpu.Memory.Dedicated.HasValue && !gpu.Memory.Shared.HasValue && gpu.Usage==55,"Removed memory adapter retained stale readings");
        Console.WriteLine("GPU memory totals, limits, unavailable data, linked adapters and responsive rendering passed.");
    }
    static void OptionsTest(){
        var o=Options.Parse(new[]{"--studio","-s","8","-d","15","--fullscreen"});
        Check(o.Count==8&&o.Delay==15&&o.Studio&&o.Full,"Options not applied");
        foreach(int seconds in new[]{1,2,5}){
            o=Options.Parse(new[]{"--refresh",seconds.ToString()});
            Check(o.Studio && o.RefreshSeconds==seconds,"Refresh option did not enable Studio");
        }
        foreach(var bad in new[]{new[]{"--refresh","3"},new[]{"--refresh","4"},new[]{"--refresh"},new[]{"--refresh","0"},new[]{"--classic","--refresh","5"},new[]{"--refresh","1","--classic"}}){
            bool rejected=false;try{Options.Parse(bad);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid or Classic refresh option accepted");
        }
        foreach(var bad in new[]{new[]{"-s","0"},new[]{"-d"},new[]{"-s","21"},new[]{"--invalid"}}){
            bool rejected=false;try{Options.Parse(bad);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid options accepted");
        }
    }
    static void RefreshSettings(Machine host,Pictures images){
        var dir=Path.Combine(Path.GetTempPath(),"refresh-"+Guid.NewGuid().ToString("N"));var store=new LayoutStore(dir);
        var scene=new Scene(20,10,new Random(13),host,images);var board=new Studio(scene,store);
        board.Load(LayoutStore.SystemExample);string original=scene.LayoutText();
        var panes=scene.Root.Leaves().ToArray();
        Check(scene.RefreshSeconds==2 && original.StartsWith("Hollywood Studio 1\n"),"Default layout compatibility lost");
        try{
            foreach(int seconds in new[]{5,1,2}){
                board.Refresh();Check(host.RefreshSeconds==seconds && board.Status().Contains(seconds+"s |"),"Refresh cycle or status wrong");
                Check(scene.Root.Leaves().SequenceEqual(panes),"Refresh rebuilt the panes");
                string saved=scene.LayoutText();scene.SetRefresh(2);scene.RestoreLayout(saved);
                Check(scene.RefreshSeconds==seconds && scene.LayoutText()==saved,"Refresh round trip failed");
                panes=scene.Root.Leaves().ToArray();
            }
            Check(scene.LayoutText()==original,"Default refresh changed old layout format");
            board.Refresh();string slow=scene.LayoutText();
            store.Save("Slow",slow,false);board.Leave();
            Check(host.RefreshSeconds==2,"Refresh leaked into Classic");
            board.Load("Slow");Check(host.RefreshSeconds==5,"Saved refresh did not load");
            foreach(string bad in new[]{slow.Replace("6 15 5","6 15 3"),slow.Replace("6 15 5","6 15"),slow.Replace("Studio 2","Studio 1"),slow.Replace("*htop","*unknown"),slow.Replace("Studio 2","Studio 3")}){
                bool rejected=false;try{scene.RestoreLayout(bad);}catch(InvalidDataException){rejected=true;}
                Check(rejected && scene.LayoutText()==slow && host.RefreshSeconds==5,"Invalid layout changed refresh or panes");
            }
            board.Load(LayoutStore.SystemExample);Check(host.RefreshSeconds==2,"Old layout did not restore standard sampling");
            bool invalid=false;try{scene.SetRefresh(0);}catch(ArgumentOutOfRangeException){invalid=true;}
            Check(invalid && host.RefreshSeconds==2,"Invalid refresh changed the sampler");
            var rates=new Dictionary<string,double?>{{"C:",42}};
            var disk=DiskSampler.Combine(rates,rates,new DiskSnapshot());
            disk=DiskSampler.Combine(rates,rates,disk);Check(disk.ReadHistory.Length==2,"Disk test history missing");
            var slower=DiskSampler.Combine(rates,rates,disk,5);
            Check(slower.ReadHistory.Length==1 && disk.ReadHistory.Length==2 && slower.Status.Contains("refresh 5s"),"Disk refresh mixed history or mislabeled rate");
            var engines=new Dictionary<string,double?>{{GpuKey(1,0,"3D"),25}};var names=new Dictionary<string,string>();
            var gpu=GpuSampler.Combine(engines,new GpuSnapshot(),names);
            gpu=GpuSampler.Combine(engines,gpu,names);var fast=GpuSampler.Combine(engines,gpu,names,null,1);
            Check(fast.Rows[0].History.Length==1 && gpu.Rows[0].History.Length==2,"GPU refresh mixed history or changed prior snapshot");
            var grid=new Grid(60,25);Widget.Gpu(grid,fast,0);Check(Contents(grid).Contains("refresh 1s"),"GPU refresh label wrong");
        }finally{board.Leave();var path=Path.Combine(dir,"Slow.layout");if(File.Exists(path))File.Delete(path);if(Directory.Exists(dir))Directory.Delete(dir);}
        Console.WriteLine("Refresh cycle, saved rates, old layouts, atomic invalid-file rejection, graph resets and Classic isolation passed.");
    }
    static void RenameCompatibility(Machine host, Pictures images) {
        string root = Path.Combine(Path.GetTempPath(), "technobabble-rename-" + Guid.NewGuid().ToString("N"));
        string legacy = Path.Combine(root, "old"), current = Path.Combine(root, "new");
        string oldEnv = Environment.GetEnvironmentVariable("HOLLYWOOD_LAYOUT_DIR");
        string newEnv = Environment.GetEnvironmentVariable("TECHNOBABBLE_LAYOUT_DIR");
        string oldFile = Path.Combine(legacy,"My original typing board.layout");
        string newFile = Path.Combine(current,"My original typing board.layout");
        const string before = "Hollywood Studio 4\n1 15 2\n*code\n0 - - - Fast\n";
        const string after = "Hollywood Studio 1\n1 15\n*cmatrix\n";
        try {
            Directory.CreateDirectory(legacy);
            File.WriteAllText(oldFile, before);
            var store = new LayoutStore(current, legacy);
            Check(store.Names().Contains("My original typing board") && store.Exists("My original typing board"), "Legacy layout is missing");
            var scene = new Scene(8, 10, new Random(11), host, images);
            var board = new Studio(scene, store);
            board.Load("My original typing board");
            Check(scene.LayoutText() == before && scene.CodePlayback == CodeStyle.Fast, "Legacy typing layout did not round trip");
#if TERMINAL
            scene.CodePlayback = CodeStyle.Normal;
            scene.Resize(80,40);
            var frame = TerminalScreen.Paint(scene,80,41,true,true,false,board);
            Check(new string(frame.Ch[40]).Contains("Y: typing normal"), "Product name clipped typing status at 80 columns");
            scene.CodePlayback = CodeStyle.Fast;
#endif
            bool blocked = false;
            try { store.Save("My original typing board", after, false); } catch (IOException) { blocked = true; }
            Check(blocked && !File.Exists(newFile), "Save shadowed a legacy layout without overwrite confirmation");
            store.Save("My original typing board", after, true);
            Check(File.ReadAllText(oldFile) == before && File.ReadAllText(newFile) == after &&
                store.Read("My original typing board") == after && store.Names().Count(n => n == "My original typing board") == 1,
                "Saving a new copy changed the original or duplicated the menu entry");
            Environment.SetEnvironmentVariable("TECHNOBABBLE_LAYOUT_DIR", null);
            Environment.SetEnvironmentVariable("HOLLYWOOD_LAYOUT_DIR", legacy);
            Check(LayoutStore.ForUser().Read("My original typing board") == before, "Legacy folder override failed");
            Environment.SetEnvironmentVariable("TECHNOBABBLE_LAYOUT_DIR", current);
            Check(LayoutStore.ForUser().Read("My original typing board") == after, "New folder override did not take precedence");
            board.Leave();
        } finally {
            Environment.SetEnvironmentVariable("TECHNOBABBLE_LAYOUT_DIR", newEnv);
            Environment.SetEnvironmentVariable("HOLLYWOOD_LAYOUT_DIR", oldEnv);
            foreach (string file in new[]{oldFile,newFile}) if(File.Exists(file))File.Delete(file);
            foreach (string dir in new[]{legacy,current,root}) if(Directory.Exists(dir))Directory.Delete(dir);
        }
        Console.WriteLine("Legacy layouts, save precedence, original preservation and both folder overrides passed.");
    }
    [STAThread] static int Main(){
        try{
            OptionsTest();var images=new Pictures();
            using(var machine=new Machine()){
                // Event-log/process enumeration can exceed five seconds on a busy host.
                for(int i=0;i<400 && machine.Latest.ProcessCount==0;i++)Thread.Sleep(50);
                Console.WriteLine("Sample: {0} processes; {1:F0} MiB RAM; NIC {2}; {3} events.",
                    machine.Latest.ProcessCount,machine.Latest.TotalMB,machine.Latest.Nic,machine.Latest.Logs.Length);
                Check(machine.Latest.ProcessCount>0 && machine.Latest.TotalMB>0,"Live Windows sample unavailable");
                Scenes(machine,images);Widgets(machine,images);Layouts(machine,images);SystemWatch(machine,images);DiskTests(machine,images);GpuTests(machine,images);GpuMemoryTests();RefreshSettings(machine,images);StudioUpgrades(machine,images);PaneEditorChecks(machine,images);
                CodeScrollChecks(machine, images);
                CodeTypingChecks(machine, images);
                RenameCompatibility(machine, images);
                RelayChecks(machine, images);
#if TERMINAL
                TerminalViews(machine,images);
#endif
            }
            Console.WriteLine("PASS");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
