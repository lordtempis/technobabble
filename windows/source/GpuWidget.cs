using System;
using System.Globalization;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Widget {
        static string Percent(double? value){return value.HasValue?value.Value.ToString("F1",CultureInfo.InvariantCulture)+"%":"sampling";}
        static void MemoryLine(Grid g,int y,string label,string compact,double? used,double? limit,byte color){
            string amount=used.HasValue?Size(used.Value):"unavailable";
            string text=label+" "+amount+(used.HasValue && limit.HasValue?" / "+Size(limit.Value):"");
            if(text.Length>g.W)text=label+" "+amount;
            if(text.Length>g.W)text=compact+" "+amount.Replace(" ","");
            g.Put(0,y,text,used.HasValue?color:Ink.Gray);
            if(g.W>=52 && used.HasValue && limit.HasValue){
                int x=Math.Max(40,text.Length+2),width=g.W-x;
                int fill=(int)Math.Round(width*Math.Min(1,used.Value/limit.Value));
                for(int i=0;i<width;i++)g.Set(x+i,y,i<fill?' ':'·',Ink.Gray,i<fill?color:Ink.Black);
            }
        }
        static string EngineName(string name){
            switch(name.ToLowerInvariant()){
                case "3d":return "3D";
                case "videodecode":return "Video decode";
                case "videoencode":return "Video encode";
                case "videoprocessing":return "Video processing";
                default:return name.Replace('_',' ');
            }
        }
        internal static void Gpu(Grid g,GpuSnapshot s,int tick){
            g.Band(0,"GPU ACTIVITY / Windows engines",Ink.Black,Ink.Purple);
            if(s.Rows.Length==0){
                g.Put(0,1,s.Status,Ink.Gold);
                g.Put(0,3,"Requires GPU performance counters",Ink.Gray);
                g.Put(0,4,"from a supported Windows driver.",Ink.Gray);return;
            }
            // Give every engine a turn in a short pane before advancing to the next adapter.
            int slots=Math.Max(1,Math.Min(16,g.H-12));
            int[] pages=s.Rows.Select(r=>Math.Max(1,(r.Engines.Length+slots-1)/slots)).ToArray();
            int page=(tick/10)%pages.Sum(),index=0;
            while(page>=pages[index])page-=pages[index++];
            var gpu=s.Rows[index];int start=page*slots;
            g.Put(0,1,gpu.Name,Ink.White);
            g.Put(0,2,"Busy "+Percent(gpu.Usage)+" | busiest engine",Ink.Pink);
            MemoryLine(g,3,"Dedicated","Ded",gpu.Memory.Dedicated,gpu.Memory.DedicatedLimit,Ink.Aqua);
            MemoryLine(g,4,"Shared","Shared",gpu.Memory.Shared,gpu.Memory.SharedLimit,Ink.Sky);
            g.Put(0,5,"Adapter "+(index+1)+" / "+s.Rows.Length,Ink.Gray);
            g.Put(0,6,"Engines "+(start+1)+"-"+Math.Min(start+slots,gpu.Engines.Length)+" / "+gpu.Engines.Length,Ink.Gray);
            int y=7;
            foreach(var engine in gpu.Engines.Skip(start).Take(slots)){
                string label=engine.Id+" "+EngineName(engine.Name),usage=Percent(engine.Usage);
                int split=Math.Max(0,g.W-9);
                g.Put(0,y,label.Substring(0,Math.Min(label.Length,split)),Ink.Aqua);
                g.Put(split,y,usage.PadLeft(Math.Min(8,g.W)),engine.Usage.HasValue?Ink.Lime:Ink.Gold);y++;
            }
            if(g.H-y>=5 && gpu.History.Length>0){
                g.Put(0,y++,"BUSY HISTORY / 0-100%",Ink.Pink);
                int h=g.H-y-1,margin=g.W>=20?5:0,w=g.W-margin;
                if(margin>0){g.Put(0,y,"100%",Ink.Gray);g.Put(0,y+h-1,"0%",Ink.Gray);}
                for(int x=0;x<w;x++){
                    if(x%3==0)g.Set(margin+x,y+h/2,'·',Ink.Gray);
                    int n=gpu.History.Length-w+x;if(n<0)continue;
                    int filled=(int)Math.Ceiling(h*gpu.History[n]/100);
                    for(int row=0;row<filled;row++)g.Set(margin+x,y+h-1-row,' ',Ink.Pink,Ink.Purple);
                }
            }
            if(g.H>8)g.Put(0,g.H-1,gpu.Usage.HasValue?"Live Windows GPU | refresh "+s.RefreshSeconds+"s":s.Status,Ink.Gray);
        }
    }
}
