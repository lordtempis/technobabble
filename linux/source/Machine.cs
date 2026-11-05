using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Technobabble {
    internal sealed class Machine : IDisposable {
        readonly Thread worker;
        readonly ManualResetEvent stop=new(false);
        readonly AutoResetEvent wake=new(false);
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly LinuxProc proc=new();
        readonly NetworkSampler network=new();
        readonly LinuxDevices devices=new();
        long diskDemand,gpuDemand;
        int refresh=2;
        public volatile bool StudioActive;
        public volatile NetworkChoice Network=NetworkChoice.Automatic;
        public volatile Snapshot Latest=new();
        public readonly FileInfo[] Files;
        public readonly string[][] Trees;
        public static bool ValidRefresh(int value)=>value==1||value==2||value==5;
        public int RefreshSeconds {
            get=>Volatile.Read(ref refresh);
            set {
                if(!ValidRefresh(value))throw new ArgumentOutOfRangeException(nameof(value));
                if(Interlocked.Exchange(ref refresh,value)!=value)wake.Set();
            }
        }
        public Machine() {
            Files=new[]{"/usr/bin/ls","/usr/bin/bash","/usr/bin/env","/usr/bin/cat","/usr/bin/uname",
                "/etc/os-release","/etc/services"}.Where(File.Exists).Select(p=>new FileInfo(p)).ToArray();
            Trees=new[]{"/etc","/usr/share","/usr/lib","/sys/class","/proc/sys"}.Select(Tree).ToArray();
            worker=new Thread(Poll){IsBackground=true,Name="Technobabble Linux sampler"};
            worker.Start();
        }
        static string[] Tree(string dir) {
            try {
                var entries=Directory.EnumerateFileSystemEntries(dir).Select(Path.GetFileName)
                    .OrderBy(n=>n,StringComparer.Ordinal).Take(64).Select(n=>"├── "+n);
                return new[]{dir}.Concat(entries).ToArray();
            } catch(IOException){return new[]{dir};}catch(UnauthorizedAccessException){return new[]{dir};}
        }
        void Demand(ref long stamp) {
            long now=Stopwatch.GetTimestamp(), previous=Interlocked.Exchange(ref stamp,now);
            if(previous==0 || (now-previous)/(double)Stopwatch.Frequency>6)wake.Set();
        }
        static bool Recent(ref long stamp) {
            long last=Interlocked.Read(ref stamp);
            return last!=0 && (Stopwatch.GetTimestamp()-last)/(double)Stopwatch.Frequency<=6;
        }
        public void RequestDisks()=>Demand(ref diskDemand);
        public void RequestGpus()=>Demand(ref gpuDemand);

        static string[] Logs() {
            foreach(string path in new[]{"/var/log/syslog","/var/log/messages"}) {
                try {
                    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                    long start=Math.Max(0,stream.Length-65536);
                    stream.Position=start;
                    using var reader=new StreamReader(stream);
                    if(start>0)reader.ReadLine();
                    var lines=reader.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries).TakeLast(48).ToArray();
                    if(lines.Length>0)return lines;
                } catch(IOException){}catch(UnauthorizedAccessException){}
            }
            return LinuxDevices.Run("/usr/bin/journalctl","--system","--no-pager","-q","-n","48","-o","short")
                .Split('\n',StringSplitOptions.RemoveEmptyEntries);
        }
        void Poll() {
            var waits=new WaitHandle[]{stop,wake};
            double last=0,nextLogs=0;
            while(!stop.WaitOne(0)) {
                var sample=new Snapshot{RefreshSeconds=RefreshSeconds};
                double now=clock.Elapsed.TotalSeconds;
                proc.Sample(sample);
                network.Sample(sample,LinuxProc.Interfaces(),StudioActive?Network:NetworkChoice.Automatic,now-last);
                var old=Latest;
                if(sample.NetworkReady) {
                    bool same=sample.NicId==old.NicId && sample.RefreshSeconds==old.RefreshSeconds;
                    sample.RxHistory=LinuxProc.Append(same?old.RxHistory:Array.Empty<double>(),sample.Rx);
                    sample.TxHistory=LinuxProc.Append(same?old.TxHistory:Array.Empty<double>(),sample.Tx);
                }
                if(now>=nextLogs){sample.Logs=Logs();nextLogs=now+30;}else sample.Logs=old.Logs;
                if(Recent(ref diskDemand))sample.Disks=devices.Disks(now,sample.RefreshSeconds);else devices.ResetDisks();
                if(Recent(ref gpuDemand))sample.Gpus=devices.Gpus(sample.RefreshSeconds);else devices.ResetGpu();
                last=now; Latest=sample;
                if(WaitHandle.WaitAny(waits,RefreshSeconds*1000)==0)break;
            }
        }
        public void Dispose() {
            stop.Set();
            worker.Join();
            stop.Dispose();wake.Dispose();
        }
    }
}
