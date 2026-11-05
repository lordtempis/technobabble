using System;
using System.Collections.Generic;
using System.Linq;

namespace Technobabble {
    // Shared editor state and undo for both frontends. It never writes a layout file.
    internal sealed class PaneEditor {
        sealed class Revision {public string Layout,Selection;}
        readonly Scene scene;
        readonly List<Revision> history=new List<Revision>();
        string[] widgets=new string[0];
        int visible=12;
        public bool IsOpen {get;private set;}
        public PaneEditAction Action {get;private set;}
        public int Choice {get;private set;}
        public string Message {get;private set;}
        public string[] Choices {get;private set;}
        public bool CanUndo {get{return history.Count>0;}}
        public bool CanRemove {get{return scene.PaneCount>1;}}
        public int PaneIndex {get{return Array.IndexOf(scene.Root.Leaves().ToArray(),scene.Selected);}}
        public string[] PaneNames {get{return scene.Root.Leaves().Select((p,i)=>(i+1)+". "+p.Widget.Name).ToArray();}}
        public string SelectedWidget {get{return scene.Selected.Widget.Name;}}
        static readonly string[] descriptions={
            "Display passwords","System process overview","Source with line numbers","Network overview",
            "Matrix rain","Scrolling source code","Error messages","Large text banners","File bytes",
            "CPU and memory overview","ASCII pictures","Windows event log","Manual pages","World map",
            "Soundwave animation","Simulated file transfers","Network graphs","SSH key art","File details",
            "Directory tree","Disk activity","GPU activity and memory","Interactive Process Watch",
            "Sensor sweep and contacts","Carrier tuning and decoded messages","Engineering repairs and telemetry"
        };
        public PaneEditor(Scene scene){this.scene=scene;Choices=new string[0];}
        public void Open(){if(!scene.IsStudio)return;IsOpen=true;SetAction(PaneEditAction.Replace);}
        public void Close(){IsOpen=false;}
        public void ClearHistory(){history.Clear();Close();}
        public void SelectPane(int index){
            var panes=scene.Root.Leaves().ToArray();
            if(index<0 || index>=panes.Length)return;
            scene.Select(panes[index].Widget);SetAction(PaneEditAction.Replace);
        }
        public void SetAction(PaneEditAction action){
            if(!Enum.IsDefined(typeof(PaneEditAction),action))return;
            Action=action;Message="";
            var panes=scene.Root.Leaves().ToArray();
            widgets=Widget.Names.Where(n=>action==PaneEditAction.Replace || !panes.Any(p=>p.Widget.Name==n)).ToArray();
            Choices=widgets.Select(n=>{
                int kind=Array.IndexOf(Widget.Names,n),other=Array.FindIndex(panes,p=>p.Widget.Name==n);
                return n+" - "+descriptions[kind]+(action==PaneEditAction.Replace && other>=0 && panes[other]!=scene.Selected?" [swap pane "+(other+1)+"]":"");
            }).ToArray();
            Choice=Math.Max(0,Array.IndexOf(widgets,scene.Selected.Widget.Name));
        }
        public void SetChoice(int index){if(index>=0 && index<widgets.Length){Choice=index;Message="";}}
        public void SelectWidget(string name){SetChoice(Array.IndexOf(widgets,name));}
        Revision Capture(){return new Revision{Layout=scene.LayoutText(),Selection=scene.Selected.Widget.Name};}
        void Remember(Revision revision){if(history.Count==20)history.RemoveAt(0);history.Add(revision);}
        public bool Apply(){
            if(!IsOpen || !scene.IsStudio)return false;
            if(Choice<0 || Choice>=widgets.Length){Message="No unused widgets available.";return false;}
            if(Action==PaneEditAction.Replace && widgets[Choice]==SelectedWidget){Message="Pane unchanged.";Close();return true;}
            var previous=Capture();string message;
            bool changed=scene.EditPane(Action,widgets[Choice],out message);Message=message;
            if(changed){Remember(previous);Close();}return changed;
        }
        public bool Remove(){
            if(!IsOpen || !scene.IsStudio)return false;
            var previous=Capture();string message;
            bool changed=scene.RemovePane(out message);Message=message;
            if(changed){Remember(previous);Close();}return changed;
        }
        public bool Undo(){
            if(!scene.IsStudio)return false;
            if(!CanUndo){Message="No pane edit to undo.";return false;}
            var previous=history[history.Count-1];
            // Undo pane structure without rolling back unrelated monitoring preferences
            // the user may have changed after that edit.
            int refresh=scene.RefreshSeconds;var network=scene.Network;var codeStyle=scene.CodePlayback;
            var sort=scene.Processes.Sort;string filter=scene.Processes.Filter;
            scene.RestoreLayout(previous.Layout);scene.SetRefresh(refresh);scene.SetNetwork(network);scene.Processes.Configure(sort,filter);
            scene.CodePlayback=codeStyle;
            var pane=scene.Root.Leaves().FirstOrDefault(p=>p.Widget.Name==previous.Selection);
            if(pane!=null)scene.Select(pane.Widget);
            history.RemoveAt(history.Count-1);Message="Undid pane edit.";Close();return true;
        }
        public void Key(ConsoleKeyInfo key){
            switch(key.Key){
                case ConsoleKey.Escape:Close();break;
                case ConsoleKey.R:SetAction(PaneEditAction.Replace);break;
                case ConsoleKey.H:SetAction(PaneEditAction.SplitRows);break;
                case ConsoleKey.V:SetAction(PaneEditAction.SplitColumns);break;
                case ConsoleKey.UpArrow:SetChoice(Math.Max(0,Choice-1));break;
                case ConsoleKey.DownArrow:SetChoice(Math.Min(widgets.Length-1,Choice+1));break;
                case ConsoleKey.PageUp:SetChoice(Math.Max(0,Choice-visible));break;
                case ConsoleKey.PageDown:SetChoice(Math.Min(widgets.Length-1,Choice+visible));break;
                case ConsoleKey.Home:SetChoice(0);break;
                case ConsoleKey.End:SetChoice(widgets.Length-1);break;
                case ConsoleKey.Tab:
                    int count=scene.PaneCount;SelectPane((PaneIndex+((key.Modifiers&ConsoleModifiers.Shift)!=0?-1:1)+count)%count);break;
                case ConsoleKey.Enter:Apply();break;
                case ConsoleKey.X:Remove();break;
                case ConsoleKey.U:Undo();break;
            }
        }
        public Grid Panel(int cols,int rows){
            int width=Math.Max(1,Math.Min(80,cols)),height=Math.Max(1,Math.Min(Choices.Length+9,rows));
            var grid=new Grid(width,height);
            for(int y=0;y<height;y++)grid.Band(y,"",Ink.White,Ink.Blue);
            string action=Action==PaneEditAction.Replace?"Replace / swap widget":Action==PaneEditAction.SplitRows?"Add pane below":"Add pane to the right";
            grid.Put(1,0,"PANE EDITOR | "+(PaneIndex+1)+" / "+scene.PaneCount+": "+SelectedWidget,Ink.Gold,Ink.Blue);
            grid.Put(1,1,action,Ink.Bright,Ink.Blue);
            visible=Math.Max(1,height-9);
            int start=Math.Max(0,Math.Min(Choice-visible/2,Choices.Length-visible));
            for(int i=0;i<visible && start+i<Choices.Length;i++){
                bool selected=start+i==Choice;
                grid.Put(1,i+3,((selected?"> ":"  ")+Choices[start+i]).PadRight(Math.Max(0,width-2)),selected?Ink.Black:Ink.Bright,selected?Ink.Aqua:Ink.Blue);
            }
            grid.Put(1,height-5,"Up/Down choose  Enter apply  R replace",Ink.White,Ink.Blue);
            grid.Put(1,height-4,"H split top/bottom  V split left/right",Ink.White,Ink.Blue);
            grid.Put(1,height-3,"X remove  U undo  Tab next pane  Esc cancel",Ink.White,Ink.Blue);
            grid.Put(1,height-2,"Edits pin all panes. P can unpin them later.",Ink.White,Ink.Blue);
            grid.Put(1,height-1,Message??"",Ink.Gold,Ink.Blue);
            return grid;
        }
    }
}
