using System;
using System.Globalization;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Widget {
        static string Percent(double? value){return value.HasValue?value.Value.ToString("F1",CultureInfo.InvariantCulture)+"%":"unavailable";}
        static void MemoryLine(Grid g,int y,string label,string compact,double? used,double? limit,byte color){
            string amount=used.HasValue?Size(used.Value):"unavailable";
            string text=label+" "+amount+(used.HasValue && limit.HasValue?" / "+Size(limit.Value):"");
            if(text.Length>g.W)text=label+" "+amount;
            if(text.Length>g.W)text=compact+" "+amount.Replace(" ","");
            g.Put(0,y,text,used.HasValue?color:Ink.Gray);
            if(g.W>=52 && used.HasValue && limit>0){
                int x=Math.Max(40,text.Length+2),width=g.W-x;
                int fill=(int)Math.Round(width*Math.Min(1,used.Value/limit.Value));
                for(int i=0;i<width;i++)g.Set(x+i,y,i<fill?' ':'·',Ink.Gray,i<fill?color:Ink.Black);
            }
        }
        internal static void Gpu(Grid g,GpuSnapshot s,int tick){
            g.Band(0,"GPU ACTIVITY / Linux",Ink.Black,Ink.Purple);
            if(s.Rows.Length==0){
                g.Put(0,1,s.Status,Ink.Gold);
                g.Put(0,3,"No readable DRM or NVIDIA metrics.",Ink.Gray);
                return;
            }
            int index=(tick/10)%s.Rows.Length;
            var gpu=s.Rows[index];
            g.Put(0,1,gpu.Name,Ink.White);
            g.Put(0,2,"Busy "+Percent(gpu.Usage),Ink.Pink);
            MemoryLine(g,3,"VRAM","VRAM",gpu.Memory.Dedicated,gpu.Memory.DedicatedLimit,Ink.Aqua);
            if(gpu.Memory.Shared.HasValue)MemoryLine(g,4,"GTT","GTT",gpu.Memory.Shared,gpu.Memory.SharedLimit,Ink.Sky);
            g.Put(0,5,"Adapter "+(index+1)+" / "+s.Rows.Length,Ink.Gray);
            int y=6;
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
            if(g.H>8)g.Put(0,g.H-1,gpu.Usage.HasValue?"Live Linux GPU | refresh "+s.RefreshSeconds+"s":s.Status,Ink.Gray);
        }
    }
}
