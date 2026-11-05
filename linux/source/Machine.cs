using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Technobabble {
    internal sealed partial class Machine : IDisposable {
        readonly Thread worker;
        readonly ManualResetEvent stop=new(false);
        readonly AutoResetEvent wake=new(false);
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly NetworkSampler network=new();
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
            Files=FilePaths.Where(File.Exists).Select(p=>new FileInfo(p)).ToArray();
            Trees=TreePaths.Select(Tree).ToArray();
            worker=new Thread(Poll){IsBackground=true,Name="Technobabble "+PlatformName+" sampler"};
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

        static string[] FileLogs(params string[] paths) {
            foreach(string path in paths) {
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
            return Array.Empty<string>();
        }
        void Poll() {
            var waits=new WaitHandle[]{stop,wake};
            double last=0,nextLogs=0;
            while(!stop.WaitOne(0)) {
                var sample=new Snapshot{RefreshSeconds=RefreshSeconds};
                double now=clock.Elapsed.TotalSeconds;
                proc.Sample(sample);
                network.Sample(sample,Interfaces(),StudioActive?Network:NetworkChoice.Automatic,now-last);
                var old=Latest;
                if(sample.NetworkReady) {
                    bool same=sample.NicId==old.NicId && sample.RefreshSeconds==old.RefreshSeconds;
                    sample.RxHistory=Snapshot.Append(same?old.RxHistory:Array.Empty<double>(),sample.Rx);
                    sample.TxHistory=Snapshot.Append(same?old.TxHistory:Array.Empty<double>(),sample.Tx);
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
