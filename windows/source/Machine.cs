using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace Technobabble {
    internal sealed class ProcessRow {
        public int Id; public string Name; public long Memory,Started; public double Cpu;
    }
    internal sealed class Snapshot {
        public DateTime Time=DateTime.Now;
        public ProcessRow[] Processes=new ProcessRow[0];
        public string[] Logs=new string[0];
        public string Nic="waiting";
        public string NicId="",NetworkStatus="";
        public bool StudioNetwork,NetworkReady;
        public NetworkRow[] Networks=new NetworkRow[0];
        public double Cpu, Rx, Tx, TotalMB, FreeMB;
        public double[] CoreCpu=new double[0];
        public double[] RxHistory=new double[0],TxHistory=new double[0];
        public int ProcessCount;
        public int RefreshSeconds=2;
        public DiskSnapshot Disks=new DiskSnapshot();
        public GpuSnapshot Gpus=new GpuSnapshot();
    }
    internal sealed class Machine : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
            public uint Size,Load; public ulong Total,Free,PageTotal,PageFree,VirtualTotal,VirtualFree,ExtendedFree;
        }
        [DllImport("kernel32")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus m);
        [DllImport("kernel32")] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
        [StructLayout(LayoutKind.Sequential)] struct CoreTimes {
            public long Idle,Kernel,User,ReservedA,ReservedB;public uint ReservedC;
        }
        [DllImport("ntdll")] static extern int NtQuerySystemInformation(int info,[Out] CoreTimes[] data,int bytes,out int used);
        CoreTimes[] previousCores;
        void ReadCores(Snapshot s){
            var current=new CoreTimes[Environment.ProcessorCount];int size=Marshal.SizeOf(typeof(CoreTimes)),used;
            if(NtQuerySystemInformation(8,current,current.Length*size,out used)!=0)return;
            int n=Math.Min(current.Length,used/size);
            if(previousCores!=null){
                s.CoreCpu=new double[n];
                for(int i=0;i<n;i++){
                    long delta=current[i].Kernel+current[i].User-previousCores[i].Kernel-previousCores[i].User;
                    if(delta>0)s.CoreCpu[i]=Math.Max(0,Math.Min(100,100.0*(delta-current[i].Idle+previousCores[i].Idle)/delta));
                }
            }
            previousCores=current;
        }
        long lastIdle,lastTotal,oldRx,oldTx;
        string oldNic="";
        double lastTime;
        readonly Stopwatch clock=Stopwatch.StartNew();
        Dictionary<int,long> prior=new Dictionary<int,long>();
        Dictionary<int,long> priorStarts=new Dictionary<int,long>();
        readonly NetworkSampler networks=new NetworkSampler();
        public volatile bool StudioActive;
        public volatile NetworkChoice Network=NetworkChoice.Automatic;
        bool lastStudio;
        readonly ManualResetEvent stop=new ManualResetEvent(false);
        readonly AutoResetEvent refreshChanged=new AutoResetEvent(false);
        int refreshSeconds=2;
        public static bool ValidRefresh(int seconds){return seconds==1 || seconds==2 || seconds==5;}
        public int RefreshSeconds {
            get{return Interlocked.CompareExchange(ref refreshSeconds,0,0);}
            set{
                if(!ValidRefresh(value))throw new ArgumentOutOfRangeException("value","Refresh must be 1, 2 or 5 seconds.");
                if(Interlocked.Exchange(ref refreshSeconds,value)!=value)refreshChanged.Set();
            }
        }
        readonly Thread worker;
        long diskRequestedAt,gpuRequestedAt,processRequestedAt;
        public void RequestProcesses(){Interlocked.Exchange(ref processRequestedAt,Stopwatch.GetTimestamp());}
        public void RequestGpus(){Interlocked.Exchange(ref gpuRequestedAt,Stopwatch.GetTimestamp());}
        public void RequestDisks(){Interlocked.Exchange(ref diskRequestedAt,Stopwatch.GetTimestamp());}
        public volatile Snapshot Latest=new Snapshot();
        public readonly FileInfo[] Files;
        public readonly string[][] Trees;
        public Machine() {
            string sys=Environment.SystemDirectory;
            Files=new[]{"kernel32.dll","user32.dll","ntdll.dll","cmd.exe","gdi32.dll","advapi32.dll","conhost.exe","where.exe"}
                .Select(n=>new FileInfo(Path.Combine(sys,n))).Where(f=>f.Exists).ToArray();
            Trees=new[]{"drivers","wbem","WindowsPowerShell","en-US","config"}.Select(n=>Tree(Path.Combine(sys,n))).ToArray();
            worker=new Thread(Poll){IsBackground=true,Name="Technobabble system sampler",Priority=ThreadPriority.BelowNormal};
            worker.Start();
        }
        static string[] Tree(string root) {
            List<string> lines=new List<string>{root};
            ReadBranch(root,"",2,lines);
            lines.Add(String.Format("{0} entries shown",lines.Count-1));
            return lines.ToArray();
        }
        static void ReadBranch(string dir,string prefix,int depth,List<string> lines) {
            if(lines.Count>=70)return;
            try {
                var entries=Directory.EnumerateFileSystemEntries(dir).Take(16).ToArray();
                for(int i=0;i<entries.Length && lines.Count<70;i++) {
                    bool last=i==entries.Length-1; string path=entries[i];
                    lines.Add(prefix+(last?"└── ":"├── ")+Path.GetFileName(path));
                    if(depth>1 && Directory.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)
                        ReadBranch(path,prefix+(last?"    ":"│   "),depth-1,lines);
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
        void Poll() {
            double nextLogs=0;
            var signals=new WaitHandle[]{stop,refreshChanged};
            using(var gpus=new GpuSampler())
            using(var disks=new DiskSampler()){
            while(!stop.WaitOne(0)) {
                try {
                    Snapshot next=new Snapshot{RefreshSeconds=RefreshSeconds}; double now=clock.Elapsed.TotalSeconds;
                    double elapsed=Math.Max(0.1,now-lastTime);
                    long idle,k,u;
                    if(GetSystemTimes(out idle,out k,out u)) {
                        long total=k+u,delta=total-lastTotal;
                        if(lastTotal>0 && delta>0) next.Cpu=100.0*(delta-idle+lastIdle)/delta;
                        lastIdle=idle; lastTotal=total;
                    }
                    MemoryStatus m=new MemoryStatus(); m.Size=(uint)Marshal.SizeOf(m);
                    ReadCores(next);
                    if(GlobalMemoryStatusEx(ref m)){next.TotalMB=m.Total/1048576.0;next.FreeMB=m.Free/1048576.0;}
                    ReadProcesses(next,elapsed);
                    ReadNetwork(next,elapsed);
                    bool sameRate=next.RefreshSeconds==Latest.RefreshSeconds && next.StudioNetwork==Latest.StudioNetwork &&
                        (!next.StudioNetwork || next.NicId==Latest.NicId);
                    if(!next.StudioNetwork || next.NetworkReady){
                        next.RxHistory=History(sameRate?Latest.RxHistory:new double[0],next.Rx);
                        next.TxHistory=History(sameRate?Latest.TxHistory:new double[0],next.Tx);
                    }
                    if(now>=nextLogs){next.Logs=ReadLogs();nextLogs=now+30;}
                    else next.Logs=Latest.Logs;
                    long requested=Interlocked.Read(ref diskRequestedAt);
                    if(requested!=0 && (Stopwatch.GetTimestamp()-requested)/(double)Stopwatch.Frequency<6)
                        next.Disks=disks.Sample(next.RefreshSeconds);
                    else disks.Dispose();
                    requested=Interlocked.Read(ref gpuRequestedAt);
                    if(requested!=0 && (Stopwatch.GetTimestamp()-requested)/(double)Stopwatch.Frequency<6)
                        next.Gpus=gpus.Sample(next.RefreshSeconds);
                    else gpus.Dispose();
                    lastTime=now; Latest=next;
                }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
                // Recalculate the remaining delay on a settings change, without an extra
                // collection or a long wait left over from the previous refresh rate.
                double completed=clock.Elapsed.TotalSeconds;
                while(true){
                    int remaining=(int)Math.Ceiling(1000*(RefreshSeconds-(clock.Elapsed.TotalSeconds-completed)));
                    if(remaining<=0)break;
                    int signal=WaitHandle.WaitAny(signals,remaining);
                    if(signal==0)return;
                    if(signal==WaitHandle.WaitTimeout)break;
                }
            }
            }
        }
        static double[] History(double[] previous,double value){
            int keep=Math.Min(previous.Length,159);
            var samples=new double[keep+1];Array.Copy(previous,previous.Length-keep,samples,0,keep);samples[keep]=value;
            return samples;
        }
        void ReadProcesses(Snapshot s,double seconds) {
            var rows=new List<ProcessRow>(); var times=new Dictionary<int,long>();var starts=new Dictionary<int,long>();
            long requested=Interlocked.Read(ref processRequestedAt);
            bool details=StudioActive && requested!=0 && (Stopwatch.GetTimestamp()-requested)/(double)Stopwatch.Frequency<6;
            Process[] all=Process.GetProcesses(); s.ProcessCount=all.Length;
            foreach(var p in all) using(p) {
                try{
                    long cpu=p.TotalProcessorTime.Ticks,previous;
                    double rate=prior.TryGetValue(p.Id,out previous)?(cpu-previous)/100000.0/seconds/Environment.ProcessorCount:0;
                    long started=0,oldStart;
                    if(details){
                        try{started=p.StartTime.ToUniversalTime().Ticks;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(NotSupportedException){}
                        if(started>0 && (!priorStarts.TryGetValue(p.Id,out oldStart) || oldStart!=started))rate=0;
                        starts[p.Id]=started;
                    }
                    times[p.Id]=cpu;
                    if(p.Id!=0)rows.Add(new ProcessRow{Id=p.Id,Name=p.ProcessName,Memory=p.WorkingSet64,Started=started,Cpu=Math.Min(100,Math.Max(0,rate))});
                }catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}catch(NotSupportedException){}
            }
            prior=times;priorStarts=starts;
            s.Processes=rows.OrderByDescending(p=>p.Cpu).ThenByDescending(p=>p.Memory).ToArray();
        }
        void ReadNetwork(Snapshot s,double seconds) {
            bool studio=StudioActive;
            if(studio!=lastStudio){oldNic="";networks.Reset();lastStudio=studio;}
            if(studio){networks.Sample(s,NetworkSampler.Read(),Network,seconds);return;}
            try{
                var nics=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                    .Select(n=>new {Nic=n,Data=n.GetIPStatistics()}).OrderByDescending(n=>n.Data.BytesReceived).FirstOrDefault();
                if(nics==null){s.Nic="offline";oldNic="";return;}
                s.Nic=nics.Nic.Name;
                long rx=nics.Data.BytesReceived,tx=nics.Data.BytesSent;
                if(oldNic==nics.Nic.Id){s.Rx=Math.Max(0,(rx-oldRx)/seconds);s.Tx=Math.Max(0,(tx-oldTx)/seconds);}
                oldNic=nics.Nic.Id;oldRx=rx;oldTx=tx;
            }catch(NetworkInformationException){s.Nic="offline";}catch(NotSupportedException){s.Nic="offline";}
        }
        static string[] ReadLogs() {
            var lines=new List<string>();
            try{
                using(var log=new EventLog("System")){
                    int count=log.Entries.Count;
                    for(int i=Math.Max(0,count-48);i<count;i++){
                        var e=log.Entries[i];
                        lines.Add(String.Format("{0:MMM dd HH:mm:ss}  {1,-11} {2}[{3}] event {4}",e.TimeGenerated,e.EntryType,e.Source,"System",e.InstanceId&65535));
                    }
                }
            }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(ArgumentException){}
            return lines.ToArray();
        }
        public void Dispose(){stop.Set(); if(worker.Join(400)){stop.Dispose();refreshChanged.Dispose();}}
    }
}
