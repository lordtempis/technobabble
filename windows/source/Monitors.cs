using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Technobabble {
    internal sealed partial class Widget {
        static string Size(double bytes){
            if(bytes<1024)return bytes.ToString("0")+" B";
            if(bytes<1048576)return (bytes/1024).ToString("0.0")+" KiB";
            if(bytes<1073741824)return (bytes/1048576).ToString("0.0")+" MiB";
            return (bytes/1073741824).ToString("0.0")+" GiB";
        }
        static void Meter(Grid g,int row,string name,double percent,string label,int x=0,int span=0) {
            int w=Math.Min(56,Math.Max(1,(span==0?g.W:span)-6));
            g.Put(x,row,name.PadLeft(3)+"[",Ink.Aqua);g.Put(x+4,row,new string(' ',w)+"]",Ink.Gray);
            int fill=Math.Min(w,Math.Max(0,(int)(percent*w/100)));
            for(int j=0;j<fill;j++)g.Set(x+4+j,row,'|',j<w*0.62?Ink.Lime:j<w*.86?Ink.Gold:Ink.Coral);
            g.Put(x+Math.Max(4,4+w-label.Length),row,label,Ink.White);
        }
        void Processes(Grid g,bool htop) {
            var s=host.Latest;double used=s.TotalMB-s.FreeMB;
            int header;
            if(htop){
                int columns=g.W>=50?2:1;
                int coreRows=(s.CoreCpu.Length+columns-1)/columns,row=1;
                if(coreRows>0 && coreRows+7<=g.H){
                    row=coreRows;int span=g.W/columns;
                    for(int i=0;i<s.CoreCpu.Length;i++)Meter(g,i%row,(i+1).ToString(),s.CoreCpu[i],s.CoreCpu[i].ToString("0.0")+"%",(i/row)*span,span);
                }else Meter(g,0,"CPU",s.Cpu,String.Format("{0:0.0}%",s.Cpu));
                Meter(g,row,"Mem",s.TotalMB>0?100*used/s.TotalMB:0,String.Format("{0:0.0}/{1:0.0}G",used/1024,s.TotalMB/1024));
                g.Put(0,row+1," Tasks: "+s.ProcessCount+"   CPUs: "+Environment.ProcessorCount+"   "+s.Time.ToString("HH:mm:ss"),Ink.Aqua);
                header=row+3;g.Band(header,"  PID   CPU%    RES  Command",Ink.Black,Ink.Green);
            }else{
                g.Band(0,"ATOP - localhost     "+s.Time.ToString("yyyy/MM/dd  HH:mm:ss"),Ink.Black,Ink.White);
                g.Put(0,1,String.Format("PRC | #proc {0,5} | #cpu {1}",s.ProcessCount,Environment.ProcessorCount),Ink.White);
                g.Put(0,2,String.Format("CPU | busy {0,5:0.0}% | idle {1,5:0.0}%",s.Cpu,100-s.Cpu),Ink.Aqua);
                g.Put(0,3,String.Format("MEM | tot {0,6:0}M | free {1,6:0}M",s.TotalMB,s.FreeMB),Ink.White);
                g.Put(0,4,"NET | "+s.Nic+" | rx "+Size(s.Rx)+"/s | tx "+Size(s.Tx)+"/s",Ink.White);
                header=6;g.Band(header,"  PID   CPU%    RES  COMMAND",Ink.White,Ink.Blue);
            }
            for(int i=0;i<s.Processes.Length && i+header+1<g.H;i++){
                var p=s.Processes[i];int row=i+header+1;
                g.Put(0,row,String.Format("{0,5} {1,6:0.0} {2,5:0}M  {3}",p.Id,p.Cpu,p.Memory/1048576.0,p.Name),i==0?Ink.Bright:Ink.White);
            }
            if(htop && g.H>7)g.Band(g.H-1,"F1Help  F2Setup  F3Search  F4Filter  F5Tree  F6SortBy",Ink.Black,Ink.Cyan);
        }
        void Network(Grid g,int t,bool bmon){
            var s=host.Latest;
            int first=bmon?4:1;
            g.Band(0,bmon?"bmon   Interfaces                    RX          TX":"speedometer   "+s.Nic,Ink.Black,bmon?Ink.White:Ink.Cyan);
            if(s.StudioNetwork && !s.NetworkReady){g.Put(0,1,s.Nic,Ink.Aqua);g.Put(0,2,s.NetworkStatus,Ink.Gold);return;}
            if(bmon){
                g.Put(0,1,"  Interface             RX Rate          TX Rate",Ink.Green);
                g.Put(0,2,String.Format("> {0,-19} {1,10}/s   {2,10}/s",s.Nic,Size(s.Rx),Size(s.Tx)),Ink.Aqua);
            }
            int half=Math.Max(1,(g.H-first)/2);
            Plot(g,first,half,s.RxHistory,"RX",s.Rx,Ink.Lime);
            Plot(g,first+half,Math.Max(1,g.H-first-half),s.TxHistory,"TX",s.Tx,Ink.Sky);
        }
        static void Plot(Grid g,int top,int height,double[] hist,string dir,double speed,byte color){
            g.Put(0,top,dir+"  "+Size(speed)+"/s",color);
            int h=height-2;if(h<1)return;
            double peak=Math.Max(1024,hist.Length==0?0:hist.Max());
            int margin=g.W>=30?11:0,w=Math.Max(1,g.W-margin);
            if(margin>0){
                g.Put(0,top+1,Size(peak),Ink.Gray);
                for(int y=1;y<=h;y++)g.Set(margin-1,top+y,'│',Ink.Gray);
                if(h>3)g.Put(0,top+h,"0 B",Ink.Gray);
            }
            for(int j=0;j<w;j++){
                if(h>=4 && j%3==0)g.Set(margin+j,top+1+h/2,'·',Ink.Gray);
                int i=hist.Length-w+j;if(i<0)continue;
                int n=Math.Min(h,(int)Math.Ceiling(h*hist[i]/peak));
                for(int y=0;y<n;y++)g.Set(margin+j,top+h-y,' ',color,color);
            }
            g.Put(0,top+height-1,new string('─',g.W),Ink.Gray);
        }
        string[] keyLines;
        int keyTick=-1;
        // RFC 5656 uncompressed nistp256 public key, hashed as an OpenSSH wire blob.
        static void Field(Stream stream,byte[] data){
            int n=data.Length;stream.WriteByte((byte)(n>>24));stream.WriteByte((byte)(n>>16));stream.WriteByte((byte)(n>>8));stream.WriteByte((byte)n);stream.Write(data,0,n);
        }
        void Fingerprint(Grid g,int t) {
            if(keyTick!=t){
                byte[] digest;
                using(var key=new ECDsaCng(256))
                using(var wire=new MemoryStream())
                using(var sha=SHA256.Create()){
                    byte[] blob=key.Key.Export(CngKeyBlobFormat.EccPublicBlob);
                    byte[] point=new byte[65];point[0]=4;Array.Copy(blob,8,point,1,64);
                    Field(wire,Encoding.ASCII.GetBytes("ecdsa-sha2-nistp256"));Field(wire,Encoding.ASCII.GetBytes("nistp256"));Field(wire,point);
                    digest=sha.ComputeHash(wire.ToArray());
                }
                int[,] board=new int[17,9];int x=8,y=4;
                foreach(byte b in digest)for(int k=0;k<8;k+=2){
                    int direction=(b>>k)&3;x=Math.Max(0,Math.Min(16,x+((direction&1)==0?-1:1)));y=Math.Max(0,Math.Min(8,y+((direction&2)==0?-1:1)));board[x,y]++;
                }
                const string symbols=" .o+=*BOX@%&#/^";
                var lines=new List<string>{"SHA256:"+Convert.ToBase64String(digest).TrimEnd('='),"+---[ECDSA 256]---+"};
                for(int j=0;j<9;j++){
                    var s=new StringBuilder("|");for(int i=0;i<17;i++)s.Append(i==x&&j==y?'E':i==8&&j==4?'S':symbols[Math.Min(symbols.Length-1,board[i,j])]);
                    lines.Add(s+"|");
                }
                lines.Add("+----[SHA256]-----+");keyLines=lines.ToArray();keyTick=t;
            }
            int first=g.H<keyLines.Length?1:0;
            for(int i=first;i<keyLines.Length;i++)g.Put(0,i-first,keyLines[i],Ink.Coral);
        }
    }
}
