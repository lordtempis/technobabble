using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Technobabble;
[assembly: SupportedOSPlatform("linux")]

internal static partial class Checks {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Contents(Grid g)=>String.Join("\n",g.Ch.Select(row=>new string(row)));
    static void Data(Machine machine) {
        var f=Enumerable.Repeat("0",24).ToArray();
        f[0]="S";f[11]="23";f[12]="19";f[19]="748";f[21]="10";
        Check(LinuxProc.TryProcess("87 (test ) worker) "+String.Join(" ",f),87,out var row,out long ticks)
              && row.Name=="test ) worker" && row.Started==748 && row.Memory==10*Environment.SystemPageSize && ticks==42,
              "Incorrect Linux proc field offsets or command parsing");
        machine.RefreshSeconds=1;
        var time=Stopwatch.StartNew();
        Snapshot s;
        do {
            machine.RequestDisks();machine.RequestGpus();Thread.Sleep(100);s=machine.Latest;
        } while(time.Elapsed.TotalSeconds<12 && (s.ProcessCount==0 || s.Disks.ReadHistory.Length==0 || !s.NetworkReady));
        Check(s.Processes.Any(p=>p.Id==Environment.ProcessId && p.Started>0 && p.Memory>0),"Own Linux PID missing");
        Check(s.TotalMB>0 && s.FreeMB>=0 && s.FreeMB<=s.TotalMB && s.CoreCpu.Length>0,"Linux CPU/memory unavailable");
        Check(s.CoreCpu.All(n=>n>=0 && n<=100),"Invalid CPU percentages");
        double mem=double.Parse(File.ReadLines("/proc/meminfo").First(l=>l.StartsWith("MemTotal:")).Split((char[])null,StringSplitOptions.RemoveEmptyEntries)[1])/1024;
        Check(Math.Abs(mem-s.TotalMB)<1,"Memory does not match Linux MemTotal");
        Check(s.NicId.Length==0 || Directory.Exists("/sys/class/net/"+s.NicId),"Non-Linux network interface");
        Check(s.Disks.Rows.All(d=>Directory.Exists("/sys/block/"+d.Name)),"Non-Linux disk name");
        if(s.Disks.Rows.Length>0)Check(s.Disks.ReadHistory.Length>0 && s.Disks.Rows.All(d=>d.Read>=0 && d.Write>=0),"Disk baselines missing");
        Console.WriteLine("Linux sample: {0} processes, {1} CPU readings, {2:F0} MiB RAM, {3}, {4} disks, {5} GPUs, {6} logs.",
            s.ProcessCount,s.CoreCpu.Length,s.TotalMB,s.Nic,s.Disks.Rows.Length,s.Gpus.Rows.Length,s.Logs.Length);
        Console.WriteLine("GPU: "+String.Join(", ",s.Gpus.Rows.Select(g=>g.Name+" "+g.Usage+"%")));
    }
    static void AllWidgets(Machine host,Pictures pictures) {
        var store=new LayoutStore(Path.Combine(Path.GetTempPath(),"technobabble-test-"+Guid.NewGuid().ToString("N")));
        var scene=new Scene(20,5,new Random(13),host,pictures);
        scene.Resize(200,54);
        for(int i=0;i<40;i++)scene.Advance(.24);
        Check(scene.Root.Leaves().All(p=>Array.IndexOf(Widget.Names,p.Widget.Name)<20),"Studio widget leaked into Classic");
        var board=new Studio(scene,store);board.Load(LayoutStore.BlankExample);
        foreach(string name in Widget.Names) {
            if(scene.Selected.Widget.Name!=name)Edit(board,PaneEditAction.Replace,name);
            bool visible=false;
            foreach(var dims in new[]{(1,1),(17,6),(42,13),(100,32)}) {
                var grid=scene.WidgetFrame(scene.Selected.Widget,dims.Item1,dims.Item2);
                visible|=Contents(grid).Trim().Length>0;
                scene.Advance(.42);
            }
            Check(visible,name+" produced no content");
        }
        board.Leave();Console.WriteLine("Classic isolation and all 26 Linux widgets passed.");
    }
    public static int Main() {
        try {
            var pictures=new Pictures();
            using var host=new Machine();
            Data(host);
            AllWidgets(host,pictures);StudioUpgrades(host,pictures);PaneEditorChecks(host,pictures);
            CodeScrollChecks(host,pictures);CodeTypingChecks(host,pictures);RelayChecks(host,pictures);
            Console.WriteLine("PASS");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
