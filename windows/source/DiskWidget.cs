using System;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Widget {
        static string Rate(double? value){return value.HasValue?Size(value.Value)+"/s":"sampling";}
        internal static void Disk(Grid g,DiskSnapshot s,int tick){
            g.Band(0,"DISK ACTIVITY / Windows volumes",Ink.Black,Ink.Cyan);
            if(s.Rows.Length==0){
                g.Put(0,1,s.Status,Ink.Gold);
                if(g.H>3)g.Put(0,3,"Reads = green    Writes = cyan",Ink.Gray);
                return;
            }
            bool compact=g.W<38;
            int rowsPerDrive=compact?3:1;
            int available=Math.Max(1,(g.H-4)/rowsPerDrive);
            int pageSize=Math.Min(s.Rows.Length,available);
            int pages=(s.Rows.Length+pageSize-1)/pageSize;
            int page=(tick/10)%pages,start=page*pageSize;
            string status=pages>1?"Volumes "+(start+1)+"-"+Math.Min(start+pageSize,s.Rows.Length)+" of "+s.Rows.Length:s.Status;
            g.Put(0,1,status,Ink.Gray);
            int row=2;
            if(!compact){g.Put(0,row++,"Volume       Read/s          Write/s",Ink.White);}
            foreach(var drive in s.Rows.Skip(start).Take(pageSize)){
                if(compact){
                    g.Put(0,row++,drive.Name,Ink.Gold);
                    g.Put(0,row++,"R "+Rate(drive.Read),Ink.Lime);
                    g.Put(0,row++,"W "+Rate(drive.Write),Ink.Aqua);
                }else{
                    string name=drive.Name.Length>9?"~"+drive.Name.Substring(drive.Name.Length-8):drive.Name;
                    g.Put(0,row,name,Ink.Gold);
                    g.Put(10,row,Rate(drive.Read),Ink.Lime);
                    g.Put(25,row++,Rate(drive.Write),Ink.Aqua);
                }
            }
            // Only complete samples enter the aggregate graphs.
            if(g.H-row>=8 && s.ReadHistory.Length>0 && s.WriteHistory.Length>0){
                int half=(g.H-row)/2;
                Plot(g,row,half,s.ReadHistory,"READ total",s.ReadHistory[s.ReadHistory.Length-1],Ink.Lime);
                Plot(g,row+half,g.H-row-half,s.WriteHistory,"WRITE total",s.WriteHistory[s.WriteHistory.Length-1],Ink.Aqua);
            }
        }
    }
}
