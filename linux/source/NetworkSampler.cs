using System;
using System.Collections.Generic;
using System.Linq;


namespace Technobabble {
    internal sealed class NetworkChoice {
        public static readonly NetworkChoice Automatic=new NetworkChoice("", "Automatic");
        public readonly string Id,Name;
        public NetworkChoice(string id,string name){Id=id;Name=name;}
    }
    internal sealed class NetworkReading {
        public string Id,Name;
        public bool Up,Valid;
        public long Received,Sent;
    }
    internal sealed class NetworkRow {
        public string Id,Name;
        public bool Up;
        public double? Rx,Tx;
    }
    // Independent baselines prevent spikes when switching adapters, reconnecting, or
    // encountering a counter reset. One inaccessible adapter does not hide the others.
    internal sealed class NetworkSampler {
        Dictionary<string,NetworkReading> prior=new Dictionary<string,NetworkReading>();
        public void Reset(){prior.Clear();}
        public void Sample(Snapshot next,NetworkReading[] readings,NetworkChoice choice,double seconds){
            var baselines=new Dictionary<string,NetworkReading>();
            var rows=new List<NetworkRow>();
            foreach(var reading in readings){
                var row=new NetworkRow{Id=reading.Id,Name=reading.Name,Up=reading.Up};
                NetworkReading old;
                if(reading.Up && reading.Valid){
                    if(prior.TryGetValue(reading.Id,out old) && reading.Received>=old.Received && reading.Sent>=old.Sent){
                        row.Rx=(reading.Received-old.Received)/Math.Max(.1,seconds);
                        row.Tx=(reading.Sent-old.Sent)/Math.Max(.1,seconds);
                    }
                    baselines[reading.Id]=reading;
                }
                rows.Add(row);
            }
            prior=baselines;
            next.Networks=rows.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
            string id=choice.Id;
            if(id.Length==0){
                var automatic=readings.Where(r=>r.Up && r.Valid).OrderByDescending(r=>r.Received).FirstOrDefault();
                id=automatic==null?"":automatic.Id;
            }
            var selected=rows.FirstOrDefault(r=>r.Id==id);
            next.StudioNetwork=true;next.NicId=id;
            next.Nic=selected!=null?selected.Name:choice.Id.Length==0?"offline":choice.Name;
            next.NetworkReady=selected!=null && selected.Rx.HasValue && selected.Tx.HasValue;
            next.NetworkStatus=selected==null?"Adapter unavailable":!selected.Up?"Adapter disconnected":!next.NetworkReady?"Waiting for adapter samples":"";
            if(next.NetworkReady){next.Rx=selected.Rx.Value;next.Tx=selected.Tx.Value;}
        }
    }
}
