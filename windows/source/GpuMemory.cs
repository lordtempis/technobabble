using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Technobabble {
    internal sealed class GpuCapacity {public double Dedicated,Shared;}
    internal sealed class GpuMemory {
        public double? Dedicated,Shared,DedicatedLimit,SharedLimit;
    }
    // Adapter totals avoid double counting resources shared between processes.
    // Memory has its own query so unavailable memory counters do not interrupt engine readings.
    internal sealed class GpuMemorySampler : IDisposable {
        readonly PdhCounters counters=new PdhCounters();
        static readonly Regex Instance=new Regex(@"\Aluid_(0x[0-9a-f]{8}_0x[0-9a-f]{8})_phys_(\d+)\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        DateTime retryAt=DateTime.MinValue;
        static double? Valid(double? n){return n.HasValue && n>=0 && !Double.IsNaN(n.Value) && !Double.IsInfinity(n.Value)?n:null;}
        internal static Dictionary<string,GpuMemory> Combine(Dictionary<string,double?> dedicated,Dictionary<string,double?> shared){
            var result=new Dictionary<string,GpuMemory>(StringComparer.OrdinalIgnoreCase);
            foreach(string name in dedicated.Keys.Union(shared.Keys,StringComparer.OrdinalIgnoreCase)){
                var match=Instance.Match(name);int physical;
                if(!match.Success || !int.TryParse(match.Groups[2].Value,out physical))continue;
                string id=match.Groups[1].Value.ToLowerInvariant()+"/"+physical;
                double? d,s;dedicated.TryGetValue(name,out d);shared.TryGetValue(name,out s);
                result[id]=new GpuMemory{Dedicated=Valid(d),Shared=Valid(s)};
            }
            return result;
        }
        internal static void Apply(GpuSnapshot snapshot,Dictionary<string,GpuMemory> memory,Dictionary<string,GpuCapacity> capacities){
            var ids=memory.Keys.Union(snapshot.Rows.Select(r=>r.Id),StringComparer.OrdinalIgnoreCase).ToArray();
            foreach(var gpu in snapshot.Rows){
                GpuMemory sample;memory.TryGetValue(gpu.Id,out sample);
                var next=new GpuMemory();
                if(sample!=null){next.Dedicated=sample.Dedicated;next.Shared=sample.Shared;}
                string luid=gpu.Id.Substring(0,gpu.Id.IndexOf('/'));GpuCapacity capacity;
                // DXGI describes a logical adapter. Do not attach that limit to individual
                // members of a linked multi-GPU adapter when their separate limits are unknown.
                if(ids.Count(id=>id.StartsWith(luid+"/",StringComparison.OrdinalIgnoreCase))==1 && capacities.TryGetValue(luid,out capacity)){
                    if(Valid(capacity.Dedicated)>0)next.DedicatedLimit=capacity.Dedicated;
                    if(Valid(capacity.Shared)>0)next.SharedLimit=capacity.Shared;
                }
                gpu.Memory=next;
            }
        }
        public Dictionary<string,GpuMemory> Sample(){
            if(!counters.IsOpen){
                if(DateTime.UtcNow<retryAt)return new Dictionary<string,GpuMemory>();
                if(counters.Open(@"\GPU Adapter Memory(*)\Dedicated Usage",@"\GPU Adapter Memory(*)\Shared Usage")!=0)return Failed();
            }
            if(counters.Collect()!=0)return Failed();
            Dictionary<string,double?> d,s;
            uint dr=counters.Values(0,out d),sr=counters.Values(1,out s);
            if(dr!=0 && sr!=0)return Failed();
            return Combine(d,s);
        }
        Dictionary<string,GpuMemory> Failed(){
            counters.Dispose();retryAt=DateTime.UtcNow.AddSeconds(30);
            return new Dictionary<string,GpuMemory>();
        }
        public void Dispose(){counters.Dispose();retryAt=DateTime.MinValue;}
    }
}
