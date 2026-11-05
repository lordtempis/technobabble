using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Technobabble {
    internal sealed class LinuxProc {
        readonly record struct CpuTicks(long Total, long Idle);
        readonly record struct TaskTicks(long Started, long Runtime);
        Dictionary<string,CpuTicks> cpus = new();
        Dictionary<int,TaskTicks> tasks = new();

        internal static string Read(string path) {
            try { return File.ReadAllText(path); }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }
        internal static string[] Fields(string text) => text.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
        internal static string[] Directories(string path,string pattern="*") {
            try { return Directory.GetDirectories(path,pattern); }
            catch(IOException) { return Array.Empty<string>(); }
            catch(UnauthorizedAccessException) { return Array.Empty<string>(); }
        }
        internal static long Number(string value) => long.TryParse(value,out long n) ? n : 0;

        public void Sample(Snapshot sample) {
            var next=new Dictionary<string,CpuTicks>();
            var cores=new List<double>();
            long aggregateDelta=0;
            foreach (string line in Read("/proc/stat").Split('\n')) {
                if(!line.StartsWith("cpu",StringComparison.Ordinal)) continue;
                var f=Fields(line);
                if(f.Length<5)continue;
                // guest and guest_nice are already included in user/nice.
                var ticks=new CpuTicks(f.Skip(1).Take(8).Sum(Number),Number(f[4])+(f.Length>5?Number(f[5]):0));
                next[f[0]]=ticks;
                double busy=0;
                if(cpus.TryGetValue(f[0],out var old) && ticks.Total>old.Total) {
                    long delta=ticks.Total-old.Total;
                    busy=Math.Clamp(100.0*(1-(ticks.Idle-old.Idle)/(double)delta),0,100);
                    if(f[0]=="cpu")aggregateDelta=delta;
                }
                if(f[0]=="cpu")sample.Cpu=busy;else cores.Add(busy);
            }
            cpus=next; sample.CoreCpu=cores.ToArray();
            var mem=Read("/proc/meminfo").Split('\n').Select(Fields).Where(f=>f.Length>=2)
                .ToDictionary(f=>f[0].TrimEnd(':'),f=>Number(f[1]));
            sample.TotalMB=mem.GetValueOrDefault("MemTotal")/1024.0;
            sample.FreeMB=mem.GetValueOrDefault("MemAvailable",mem.GetValueOrDefault("MemFree"))/1024.0;
            var previous=tasks;
            tasks=new Dictionary<int,TaskTicks>();
            var rows=new List<ProcessRow>();
            foreach(string dir in Directories("/proc")) {
                if(!int.TryParse(Path.GetFileName(dir),out int pid))continue;
                if(!TryProcess(Read(Path.Combine(dir,"stat")),pid,out var row,out long runtime))continue;
                tasks[pid]=new TaskTicks(row.Started,runtime);
                if(aggregateDelta>0 && previous.TryGetValue(pid,out var old) && old.Started==row.Started && runtime>=old.Runtime)
                    row.Cpu=Math.Clamp(100.0*(runtime-old.Runtime)/aggregateDelta,0,100);
                rows.Add(row);
            }
            sample.ProcessCount=rows.Count;
            sample.Processes=rows.OrderByDescending(r=>r.Cpu).ThenByDescending(r=>r.Memory).ToArray();
        }

        internal static bool TryProcess(string stat,int pid,out ProcessRow row,out long runtime) {
            row=null; runtime=0;
            int open=stat.IndexOf('('),close=stat.LastIndexOf(')');
            if(open<0 || close<=open)return false;
            var f=Fields(stat.Substring(close+1)); // f[0] is field 3 (state).
            if(f.Length<22 || !long.TryParse(f[11],out long user) || !long.TryParse(f[12],out long system)
                || !long.TryParse(f[19],out long start) || !long.TryParse(f[21],out long rss))return false;
            row=new ProcessRow{Id=pid,Name=stat.Substring(open+1,close-open-1),Started=start,Memory=Math.Max(0,rss)*Environment.SystemPageSize};
            runtime=user+system;
            return true;
        }

        public static NetworkReading[] Interfaces() {
            var rows=new List<NetworkReading>();
            foreach(string line in Read("/proc/net/dev").Split('\n')) {
                int colon=line.LastIndexOf(':');
                if(colon<0)continue;
                string name=line.Substring(0,colon).Trim();
                var f=Fields(line.Substring(colon+1));
                if(name=="lo" || f.Length<16 || !long.TryParse(f[0],out long rx) || !long.TryParse(f[8],out long tx))continue;
                string state=Read("/sys/class/net/"+name+"/operstate").Trim();
                rows.Add(new NetworkReading{Id=name,Name=name,Up=state=="up"||state=="unknown",Valid=true,Received=rx,Sent=tx});
            }
            return rows.ToArray();
        }
    }
}
