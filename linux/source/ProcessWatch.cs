using System;
using System.Collections.Generic;
using System.Linq;

namespace Technobabble {
    internal enum ProcessSort { Cpu,Memory,Name }
    // UI state is separate from the sampler and Classic's decorative htop pane.
    // PID + creation time is used when available, so reused PIDs cannot inherit history.
    internal sealed class ProcessWatch {
        Snapshot sample;
        ProcessRow selected;
        int offset;
        bool chooseFirst=true;
        public ProcessSort Sort {get;private set;}
        public string Filter {get;private set;}
        public ProcessRow[] Rows {get;private set;}
        public ProcessRow Selected {get{return selected;}}
        public bool Present {get;private set;}
        public double[] CpuHistory {get;private set;}
        public double[] MemoryHistory {get;private set;}
        public int VisibleRows {get;private set;}
        public ProcessWatch(){Reset();}
        public void Reset(){sample=null;selected=null;chooseFirst=true;offset=0;Sort=ProcessSort.Cpu;Filter="";Rows=new ProcessRow[0];ClearHistory();}
        void ClearHistory(){CpuHistory=new double[0];MemoryHistory=new double[0];}
        public static bool ValidFilter(string text){return text!=null && text.Length<=64 && !text.Any(Char.IsControl);}
        public void Configure(ProcessSort sort,string filter){
            if(!Enum.IsDefined(typeof(ProcessSort),sort) || !ValidFilter(filter))throw new ArgumentException("Invalid process preferences.");
            Sort=sort;Filter=filter;selected=null;chooseFirst=true;offset=0;ClearHistory();Reorder();
        }
        public void CycleSort(){Sort=(ProcessSort)(((int)Sort+1)%3);Reorder();}
        static bool Same(ProcessRow a,ProcessRow b){return a!=null && b!=null && a.Id==b.Id && a.Started==b.Started && a.Name==b.Name;}
        public void Update(Snapshot next){
            if(Object.ReferenceEquals(sample,next))return;
            bool changedRate=sample!=null && sample.RefreshSeconds!=next.RefreshSeconds;
            sample=next;Reorder();
            if(selected!=null && selected.Started==0){
                var identified=next.Processes.FirstOrDefault(p=>p.Id==selected.Id && p.Name==selected.Name && p.Started!=0);
                if(identified!=null){selected=identified;ClearHistory();}
            }
            var current=next.Processes.FirstOrDefault(p=>Same(p,selected));
            Present=current!=null;
            if(current!=null){
                selected=current;
                if(changedRate || current.Started==0)ClearHistory();
                // If Linux withholds the process start time, show current values but do not
                // pretend that historical samples belong to a verified process identity.
                if(current.Started!=0){CpuHistory=Append(CpuHistory,current.Cpu);MemoryHistory=Append(MemoryHistory,current.Memory);}
            }
        }
        void Reorder(){
            IEnumerable<ProcessRow> rows=sample==null?new ProcessRow[0]:sample.Processes;
            rows=rows.Where(p=>p.Name.IndexOf(Filter,StringComparison.OrdinalIgnoreCase)>=0 || p.Id.ToString().Contains(Filter));
            Rows=(Sort==ProcessSort.Name?rows.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Id):
                Sort==ProcessSort.Memory?rows.OrderByDescending(p=>p.Memory).ThenBy(p=>p.Id):rows.OrderByDescending(p=>p.Cpu).ThenBy(p=>p.Id)).ToArray();
            if(chooseFirst && Rows.Length>0){selected=Rows[0];chooseFirst=false;Present=true;ClearHistory();}
        }
        public void Move(int step){
            if(Rows.Length==0)return;
            int index=Array.FindIndex(Rows,p=>Same(p,selected));
            int target=index<0?(step<0?Rows.Length-1:0):Math.Max(0,Math.Min(Rows.Length-1,index+step));
            if(!Same(Rows[target],selected)){selected=Rows[target];Present=true;ClearHistory();}
        }
        public void End(bool last){Move(last?Int32.MaxValue/2:-Int32.MaxValue/2);}
        public void ClickRow(int row){if(row<0 || row>=VisibleRows || offset+row>=Rows.Length)return;var target=Rows[offset+row];if(!Same(target,selected)){selected=target;Present=true;ClearHistory();}}
        static double[] Append(double[] values,double value){int keep=Math.Min(119,values.Length);var next=new double[keep+1];Array.Copy(values,values.Length-keep,next,0,keep);next[keep]=value;return next;}
        public int Start(int visible){
            VisibleRows=visible;int index=Array.FindIndex(Rows,p=>Same(p,selected));
            if(index>=0){if(index<offset)offset=index;if(index>=offset+visible)offset=index-visible+1;}
            offset=Math.Max(0,Math.Min(offset,Math.Max(0,Rows.Length-visible)));return offset;
        }
        public bool IsSelected(ProcessRow row){return Same(row,selected);}
    }

    internal sealed partial class Widget {
        void ProcessPane(Grid g){
            var view=processWatch;view.Update(host.Latest);
            g.Band(0,"PROCESS WATCH / "+view.Sort.ToString().ToUpperInvariant(),Ink.Black,Ink.Aqua);
            g.Put(0,1,"Filter: "+(view.Filter.Length==0?"all":view.Filter)+" | "+view.Rows.Length+" matches",Ink.Gray);
            g.Band(2,"  PID   CPU%    RAM  Name",Ink.Black,Ink.Green);
            int listBudget=g.H>=20?(g.H-6)*2/3:g.H>=12?g.H-8:g.H-5;
            int visible=Math.Max(1,Math.Min(listBudget,Math.Max(1,view.Rows.Length))),start=view.Start(visible);
            for(int i=0;i<visible && start+i<view.Rows.Length;i++){
                var row=view.Rows[start+i];bool chosen=view.IsSelected(row);
                g.Band(3+i,String.Format("{0}{1,5} {2,5:0.0} {3,5:0}M {4}",chosen?">":" ",row.Id,row.Cpu,row.Memory/1048576.0,row.Name),chosen?Ink.Black:Ink.White,chosen?Ink.Aqua:Ink.Black);
            }
            if(view.Rows.Length==0)g.Put(0,3,"No matching processes",Ink.Gold);
            int y=3+visible;var selected=view.Selected;
            if(selected!=null){
                g.Put(0,y++,selected.Name+" ["+selected.Id+"]"+(view.Present?"":" exited / unavailable"),view.Present?Ink.Aqua:Ink.Gold);
                if(view.Present)g.Put(0,y++,String.Format("CPU {0:0.0}%   RAM {1}",selected.Cpu,Size(selected.Memory)),Ink.White);
                else y++;
                if(g.H-y>=7 && selected.Started!=0){
                    int height=(g.H-y-1)/2;double peak=Math.Max(1048576,view.MemoryHistory.DefaultIfEmpty(0).Max());
                    HistoryBars(g,y,height,view.CpuHistory,100,"CPU history / 0-100%",Ink.Lime);
                    HistoryBars(g,y+height,g.H-y-height-1,view.MemoryHistory,peak,"RAM history / peak "+Size(peak),Ink.Sky);
                }else if(selected.Started==0 && g.H-y>1)g.Put(0,y,"History unavailable for this process",Ink.Gray);
            }
            if(g.H>5)g.Band(g.H-1,"Up/Down select  PgUp/PgDn  O sort  F filter",Ink.Black,Ink.Cyan);
        }
        static void HistoryBars(Grid g,int top,int height,double[] samples,double max,string label,byte color){
            g.Put(0,top,label,color);
            for(int x=0;x<g.W;x++){
                int index=samples.Length-g.W+x;if(index<0)continue;
                int fill=(int)Math.Ceiling((height-1)*Math.Min(1,samples[index]/max));
                for(int y=0;y<fill;y++)g.Set(x,top+height-1-y,' ',color,color);
            }
        }
    }
}
