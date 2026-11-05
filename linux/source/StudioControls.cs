using System;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Studio {
        public bool CodeSelected {get{return scene.Selected.Widget.Name=="code";}}
        public void CycleCodeStyle(){
            if(!active)return;
            if(!CodeSelected){Notice="Select the code pane with Tab, then press Y.";return;}
            scene.CodePlayback=scene.CodePlayback==CodeStyle.Fast?CodeStyle.Scroll:scene.CodePlayback+1;
            Notice="W/F5 saves this setting.";
        }
        public bool ProcessSelected {get{return scene.Selected.Widget.Name=="process";}}
        public bool Resizing {get;private set;}
        public Pane ResizeDivider {get;private set;}
        public void OpenFilter(){
            if(!ProcessSelected)return;
            Panes.Close();
            filtering=true;networking=false;Editing=true;Resizing=false;confirm=false;error="";index=-1;
            name=scene.Processes.Filter;names=new string[0];replaceOnType=name.Length>0;
        }
        public void OpenNetwork(){
            Panes.Close();
            filtering=false;networking=true;Editing=true;Resizing=false;confirm=false;error="";
            var adapters=scene.NetworkAdapters;
            var choices=adapters.Select(a=>new NetworkChoice(a.Id,a.Name)).ToList();
            if(scene.Network.Id.Length>0 && !choices.Any(c=>c.Id==scene.Network.Id))choices.Add(scene.Network);
            choices.Insert(0,NetworkChoice.Automatic);networkChoices=choices.ToArray();
            names=choices.Select((c,i)=>i==0?"Automatic":i+". "+c.Name+
                (adapters.Any(a=>a.Id==c.Id && a.Up)?" (connected)":" (unavailable)")).ToArray();
            index=choices.FindIndex(c=>c.Id==scene.Network.Id);if(index<0)index=0;
            name=names[index];replaceOnType=false;
        }
        public bool ProcessKey(ConsoleKey key){
            if(key==ConsoleKey.C){scene.ShowProcesses();Notice="Process Watch selected. O sorts, F filters.";return true;}
            if(!ProcessSelected)return false;
            scene.UpdateProcesses();
            switch(key){
                case ConsoleKey.O:scene.Processes.CycleSort();break;
                case ConsoleKey.UpArrow:scene.Processes.Move(-1);break;
                case ConsoleKey.DownArrow:scene.Processes.Move(1);break;
                case ConsoleKey.PageUp:scene.Processes.Move(-Math.Max(1,scene.Processes.VisibleRows));break;
                case ConsoleKey.PageDown:scene.Processes.Move(Math.Max(1,scene.Processes.VisibleRows));break;
                case ConsoleKey.Home:scene.Processes.End(false);break;
                case ConsoleKey.End:scene.Processes.End(true);break;
                default:return false;
            }
            Notice=null;return true;
        }
        public void ToggleResize(){
            if(Resizing){Resizing=false;return;}
            ResizeDivider=scene.NearestDivider();Resizing=ResizeDivider!=null;
            if(!Resizing)Notice="Wait for a split before resizing.";
        }
        public bool ResizeKey(ConsoleKey key,bool shift){
            if(!Resizing)return false;
            switch(key){
                case ConsoleKey.Enter:case ConsoleKey.Escape:case ConsoleKey.B:Resizing=false;Notice="Pane sizes updated. Save to keep them.";return true;
                case ConsoleKey.Tab:
                    var splits=scene.Dividers.ToList();int index=splits.IndexOf(ResizeDivider);
                    if(splits.Count==0){Resizing=false;return true;}
                    ResizeDivider=splits[(index+(shift?-1:1)+splits.Count)%splits.Count];return true;
                case ConsoleKey.LeftArrow:case ConsoleKey.UpArrow:scene.SetRatio(ResizeDivider,ResizeDivider.Ratio-5);return true;
                case ConsoleKey.RightArrow:case ConsoleKey.DownArrow:scene.SetRatio(ResizeDivider,ResizeDivider.Ratio+5);return true;
                case ConsoleKey.D0:case ConsoleKey.NumPad0:scene.SetRatio(ResizeDivider,50);return true;
                default:return false;
            }
        }
    }
}
