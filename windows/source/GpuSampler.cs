using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Technobabble {
    internal sealed class GpuEngine {
        public int Id;
        public string Name;
        public double? Usage;
    }
    internal sealed class GpuRow {
        public string Id,Name;
        public GpuEngine[] Engines=new GpuEngine[0];
        public double? Usage;
        public double[] History=new double[0];
        public GpuMemory Memory=new GpuMemory();
    }
    internal sealed class GpuSnapshot {
        public GpuRow[] Rows=new GpuRow[0];
        public string Status="Waiting for GPU samples";
        public int RefreshSeconds=2;
    }
    internal sealed class GpuSampler : IDisposable {
        readonly PdhCounters counters=new PdhCounters();
        readonly GpuMemorySampler memory=new GpuMemorySampler();
        Dictionary<string,GpuCapacity> capacities=new Dictionary<string,GpuCapacity>();
        static readonly Regex Instance=new Regex(@"\Apid_\d+_luid_(0x[0-9a-f]{8}_0x[0-9a-f]{8})_phys_(\d+)_eng_(\d+)_engtype_(.+)\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        Dictionary<string,string> names=new Dictionary<string,string>();
        HashSet<string> software=new HashSet<string>();
        bool primed;
        DateTime retryAt=DateTime.MinValue;
        GpuSnapshot latest=new GpuSnapshot();

        internal static GpuSnapshot Combine(Dictionary<string,double?> values,GpuSnapshot previous,Dictionary<string,string> names,HashSet<string> software=null,int seconds=2){
            var adapters=new SortedDictionary<string,Dictionary<int,GpuEngine>>(StringComparer.OrdinalIgnoreCase);
            var labels=new Dictionary<string,string>();
            foreach(var value in values){
                var match=Instance.Match(value.Key);int physical,engine;
                if(!match.Success || !int.TryParse(match.Groups[2].Value,out physical) || !int.TryParse(match.Groups[3].Value,out engine))continue;
                string luid=match.Groups[1].Value.ToLowerInvariant(),id=luid+"/"+physical;
                if(software!=null && software.Contains(luid))continue;
                Dictionary<int,GpuEngine> engines;
                if(!adapters.TryGetValue(id,out engines)){
                    adapters[id]=engines=new Dictionary<int,GpuEngine>();
                    string label;
                    if(!names.TryGetValue(luid,out label))label="GPU "+luid;
                    // Keep the physical member distinct even for a linked adapter.
                    labels[id]=label;
                }
                GpuEngine row;
                if(!engines.TryGetValue(engine,out row))engines[engine]=row=new GpuEngine{Id=engine,Name=match.Groups[4].Value,Usage=0};
                double? usage=value.Value;
                // A new/stale process instance makes this engine incomplete, rather than a fabricated zero.
                if(!usage.HasValue || usage<0 || Double.IsNaN(usage.Value) || Double.IsInfinity(usage.Value))row.Usage=null;
                else if(row.Usage.HasValue)row.Usage=Math.Min(100,row.Usage.Value+usage.Value);
            }
            var rows=new List<GpuRow>();
            foreach(var adapter in adapters){
                var engines=adapter.Value.Values.OrderBy(e=>e.Id).ToArray();
                var row=new GpuRow{Id=adapter.Key,Name=labels[adapter.Key],Engines=engines};
                if(engines.All(e=>e.Usage.HasValue)){
                    // Overall utilization is the busiest engine, not a sum of independent engines.
                    row.Usage=engines.Max(e=>e.Usage.Value);
                    var old=previous.Rows.FirstOrDefault(r=>r.Id==row.Id);
                    double[] history=old!=null && previous.RefreshSeconds==seconds && old.Engines.Select(e=>e.Id).SequenceEqual(engines.Select(e=>e.Id))?old.History:new double[0];
                    int keep=Math.Min(119,history.Length);row.History=new double[keep+1];
                    Array.Copy(history,history.Length-keep,row.History,0,keep);row.History[keep]=row.Usage.Value;
                }
                rows.Add(row);
            }
            return new GpuSnapshot{Rows=rows.ToArray(),RefreshSeconds=seconds,Status=rows.Count==0?"No GPU engine samples available":
                rows.Any(r=>!r.Usage.HasValue)?"Waiting for valid GPU samples":"Windows GPU engines | refresh "+seconds+"s"};
        }
        public GpuSnapshot Sample(int seconds=2){
            if(!counters.IsOpen){
                if(DateTime.UtcNow<retryAt)return latest;
                uint status=counters.Open(@"\GPU Engine(*)\Utilization Percentage");
                if(status!=0)return Failed(status);
                names=GpuAdapters.Read(out software,out capacities);
            }
            uint collect=counters.Collect();
            if(collect!=0)return Failed(collect);
            if(!primed){primed=true;return latest=new GpuSnapshot{RefreshSeconds=seconds};}
            Dictionary<string,double?> values;
            uint read=counters.Values(0,out values);
            if(read!=0)return Failed(read);
            // Refresh identities when a previously unknown adapter appears after hot-plug or driver restart.
            if(values.Keys.Any(k=>{var m=Instance.Match(k);return m.Success && !names.ContainsKey(m.Groups[1].Value);}))
                names=GpuAdapters.Read(out software,out capacities);
            var next=Combine(values,latest,names,software,seconds);
            GpuMemorySampler.Apply(next,memory.Sample(),capacities);
            return latest=next;
        }
        GpuSnapshot Failed(uint status){
            Dispose();retryAt=DateTime.UtcNow.AddSeconds(30);
            return latest=new GpuSnapshot{Status="GPU counters unavailable (0x"+status.ToString("X8")+")"};
        }
        public void Dispose(){counters.Dispose();memory.Dispose();primed=false;retryAt=DateTime.MinValue;latest=new GpuSnapshot();}
    }
}
