using System;
using System.Collections.Generic;
using System.Linq;

namespace Technobabble {
    internal sealed class DiskRow {
        public string Name;
        public double? Read,Write;
    }
    internal sealed class DiskSnapshot {
        public DiskRow[] Rows=new DiskRow[0];
        public string Status="Waiting for disk samples";
        public int RefreshSeconds=2;
        public double[] ReadHistory=new double[0],WriteHistory=new double[0];
    }

    // Uses language-neutral Windows performance counters, with one query shared by all volumes.
    internal sealed class DiskSampler : IDisposable {
        readonly PdhCounters counters=new PdhCounters();
        bool primed;
        DateTime retryAt=DateTime.MinValue;
        DiskSnapshot latest=new DiskSnapshot();

        static double[] Append(double[] previous,double value){
            int keep=Math.Min(119,previous.Length);
            var result=new double[keep+1];
            Array.Copy(previous,previous.Length-keep,result,0,keep);result[keep]=value;
            return result;
        }
        internal static DiskSnapshot Combine(Dictionary<string,double?> reads,Dictionary<string,double?> writes,DiskSnapshot previous,int seconds=2){
            string[] names=reads.Keys.Union(writes.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();
            var rows=new List<DiskRow>();
            foreach(string name in names){
                double? r,w;reads.TryGetValue(name,out r);writes.TryGetValue(name,out w);
                rows.Add(new DiskRow{Name=name,Read=r,Write=w});
            }
            var next=new DiskSnapshot{Rows=rows.ToArray(),RefreshSeconds=seconds};
            bool ready=rows.Count>0 && rows.All(r=>r.Read.HasValue && r.Write.HasValue);
            if(!ready){next.Status=rows.Count==0?"No local volume counters available":"Waiting for valid volume samples";return next;}
            next.Status="Windows volumes | refresh "+seconds+"s";
            // Start a new aggregate history when membership changes, so hot-plugged volumes do not create misleading jumps.
            bool same=previous.RefreshSeconds==seconds && previous.Rows.Select(r=>r.Name).SequenceEqual(names,StringComparer.OrdinalIgnoreCase);
            next.ReadHistory=Append(same?previous.ReadHistory:new double[0],rows.Sum(r=>r.Read.Value));
            next.WriteHistory=Append(same?previous.WriteHistory:new double[0],rows.Sum(r=>r.Write.Value));
            return next;
        }
        public DiskSnapshot Sample(int seconds=2){
            if(!counters.IsOpen){
                if(DateTime.UtcNow<retryAt)return latest;
                uint open=counters.Open(@"\LogicalDisk(*)\Disk Read Bytes/sec",@"\LogicalDisk(*)\Disk Write Bytes/sec");
                if(open!=0)return Failed(open);
            }
            uint status=counters.Collect();
            if(status!=0)return Failed(status);
            if(!primed){primed=true;return latest=new DiskSnapshot{RefreshSeconds=seconds};}
            Dictionary<string,double?> r,w;
            status=counters.Values(0,out r);
            if(status!=0)return Failed(status);
            status=counters.Values(1,out w);
            if(status!=0)return Failed(status);
            return latest=Combine(r,w,latest,seconds);
        }
        DiskSnapshot Failed(uint status){
            Dispose();retryAt=DateTime.UtcNow.AddSeconds(30);
            return latest=new DiskSnapshot{Status="Disk counters unavailable (0x"+status.ToString("X8")+")"};
        }
        public void Dispose(){
            counters.Dispose();
            primed=false;retryAt=DateTime.MinValue;latest=new DiskSnapshot();
        }
    }
}
