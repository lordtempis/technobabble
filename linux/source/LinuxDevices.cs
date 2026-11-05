using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Technobabble {
    internal sealed class LinuxDevices {
        readonly record struct Sectors(long Read,long Written);
        Dictionary<string,Sectors> diskBaseline=new();
        DiskSnapshot disk=new();
        double diskTime;
        GpuSnapshot gpu=new();

        public DiskSnapshot Disks(double now,int refresh) {
            var next=new Dictionary<string,Sectors>();
            var rows=new List<DiskRow>();
            foreach(var line in LinuxProc.Read("/proc/diskstats").Split('\n')) {
                var f=LinuxProc.Fields(line);
                if(f.Length<10)continue;
                string name=f[2], dir="/sys/block/"+name;
                if(name.StartsWith("loop") || name.StartsWith("ram") || !Directory.Exists(dir))continue;
                // Use whole backing devices only, so a partition or stacked
                // device does not count the same I/O twice.
                if(LinuxProc.Directories(dir+"/slaves").Length!=0)continue;
                if(!long.TryParse(f[5],out long read) || !long.TryParse(f[9],out long written))continue;
                next[name]=new Sectors(read,written);
                var row=new DiskRow{Name=name};
                if(now>diskTime && diskBaseline.TryGetValue(name,out var old) && read>=old.Read && written>=old.Written) {
                    row.Read=(read-old.Read)*512.0/(now-diskTime);
                    row.Write=(written-old.Written)*512.0/(now-diskTime);
                }
                rows.Add(row);
            }
            var result=new DiskSnapshot{Rows=rows.OrderBy(r=>r.Name,StringComparer.Ordinal).ToArray(),RefreshSeconds=refresh};
            if(rows.Count==0)result.Status="No Linux block device counters exposed";
            else if(rows.All(r=>r.Read.HasValue)) {
                bool same=refresh==disk.RefreshSeconds && result.Rows.Select(r=>r.Name).SequenceEqual(disk.Rows.Select(r=>r.Name));
                result.ReadHistory=Snapshot.Append(same?disk.ReadHistory:Array.Empty<double>(),rows.Sum(r=>r.Read.Value));
                result.WriteHistory=Snapshot.Append(same?disk.WriteHistory:Array.Empty<double>(),rows.Sum(r=>r.Write.Value));
                result.Status="Linux block devices | refresh "+refresh+"s";
            } else result.Status="Collecting block device baselines";
            diskTime=now; diskBaseline=next; disk=result;
            return result;
        }

        public void ResetDisks(){diskBaseline.Clear();disk=new DiskSnapshot();}
        public void ResetGpu(){gpu=new GpuSnapshot();}

        public GpuSnapshot Gpus(int refresh) {
            var rows=new List<GpuRow>();
            const string drm="/sys/class/drm";
            foreach(string card in LinuxProc.Directories(drm,"card*")) {
                string id=Path.GetFileName(card);
                if(!int.TryParse(id.AsSpan(4),out _))continue;
                string device=card+"/device/";
                double? usage=Metric(LinuxProc.Read(device+"gpu_busy_percent"));
                var memory=new GpuMemory{Dedicated=Metric(LinuxProc.Read(device+"mem_info_vram_used")),
                    DedicatedLimit=Metric(LinuxProc.Read(device+"mem_info_vram_total")),
                    Shared=Metric(LinuxProc.Read(device+"mem_info_gtt_used")),SharedLimit=Metric(LinuxProc.Read(device+"mem_info_gtt_total"))};
                if(!usage.HasValue && !memory.Dedicated.HasValue && !memory.Shared.HasValue)continue;
                string driver=LinuxProc.Read(device+"uevent").Split('\n').FirstOrDefault(s=>s.StartsWith("DRIVER="));
                rows.Add(new GpuRow{Id=id,Name=id+(driver==null?"":" / "+driver.Substring(7)),Usage=usage,Memory=memory});
            }
            if(rows.Count==0) {
                string tool=new[]{"/usr/lib/wsl/lib/nvidia-smi","/usr/bin/nvidia-smi"}.FirstOrDefault(File.Exists);
                if(tool!=null) {
                    string data=Run(tool,"--query-gpu=uuid,name,utilization.gpu,memory.used,memory.total","--format=csv,noheader,nounits");
                    foreach(string line in data.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {
                        var f=line.Split(',',StringSplitOptions.TrimEntries);
                        if(f.Length!=5 || !f[0].StartsWith("GPU-"))continue;
                        rows.Add(new GpuRow{Id=f[0],Name=f[1],Usage=Metric(f[2]),Memory=new GpuMemory{
                            Dedicated=Metric(f[3])*1048576,DedicatedLimit=Metric(f[4])*1048576}});
                    }
                }
            }
            foreach(var row in rows) {
                if(row.Usage>100)row.Usage=null;
                var old=gpu.Rows.FirstOrDefault(r=>r.Id==row.Id);
                if(row.Usage.HasValue)row.History=Snapshot.Append(old!=null && refresh==gpu.RefreshSeconds?old.History:Array.Empty<double>(),row.Usage.Value);
            }
            gpu=new GpuSnapshot{Rows=rows.ToArray(),RefreshSeconds=refresh,Status=rows.Count==0?"GPU metrics unavailable in Linux":"Linux GPU / refresh "+refresh+"s"};
            return gpu;
        }
        static double? Metric(string text) =>
            double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out double n) && double.IsFinite(n) && n>=0 ? n : null;

        internal static string Run(string executable,params string[] args) {
            try {
                var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,
                    RedirectStandardError=true,RedirectStandardInput=true};
                foreach(var arg in args)start.ArgumentList.Add(arg);
                start.Environment["LC_ALL"]="C";
                using var child=Process.Start(start);
                child.StandardInput.Close();
                var output=child.StandardOutput.ReadToEndAsync();
                var error=child.StandardError.ReadToEndAsync();
                if(!child.WaitForExit(1800)){child.Kill();child.WaitForExit();return "";}
                _=error.GetAwaiter().GetResult();
                return child.ExitCode==0?output.GetAwaiter().GetResult():"";
            } catch(Win32Exception){return "";}catch(IOException){return "";}
        }
    }
}
