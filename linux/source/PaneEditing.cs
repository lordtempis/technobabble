using System;
using System.Linq;

namespace Technobabble {
    internal enum PaneEditAction { Replace,SplitRows,SplitColumns }

    internal sealed partial class Scene {
        public bool IsStudio {get{return machine.StudioActive;}}
        public int PaneCount {get{return Root.Leaves().Count();}}

        // Manual edits finish the current automatic build and pin the visible layout.
        // Classic's split/rebuild algorithm remains separate and unchanged.
        void FinishPaneEdit(Widget selection){
            foreach(var pane in Root.Leaves())pane.Pinned=true;
            count=PaneCount;added=count-1;building=false;deck.Clear();next=Time+delay;
            selected=selection;Root.Arrange(bounds);Generation++;
        }
        public bool EditPane(PaneEditAction action,string name,out string message){
            message="";
            if(!IsStudio){message="Pane editing is available in Studio.";return false;}
            int kind=Array.IndexOf(Widget.Names,name);
            if(kind<0 || !Enum.IsDefined(typeof(PaneEditAction),action)){message="Choose a widget from the list.";return false;}
            var target=Selected;
            var existing=Root.Leaves().FirstOrDefault(p=>p.Widget.Name==name);
            if(action==PaneEditAction.Replace){
                if(existing==target){message="This pane already shows "+name+".";return false;}
                if(existing!=null){var old=target.Widget;target.Widget=existing.Widget;existing.Widget=old;message="Swapped "+name+" into the selected pane.";}
                else{target.Widget=Make(kind);message="Selected pane now shows "+name+".";}
                FinishPaneEdit(target.Widget);return true;
            }
            if(PaneCount>=Widget.ClassicCount){message="20-pane limit reached. Remove a pane first.";return false;}
            if(existing!=null){message="That widget is already in the layout. Choose an unused widget.";return false;}
            bool rows=action==PaneEditAction.SplitRows;
            if(rows?(target.Bounds.Height<11 || target.Bounds.Width<12):(target.Bounds.Width<25 || target.Bounds.Height<5)){
                message="Pane too small. Enlarge it with B, or enlarge the window.";return false;
            }
            var created=Make(kind);
            target.A=new Pane{Widget=target.Widget};target.B=new Pane{Widget=created};
            target.Widget=null;target.Pinned=false;target.HorizontalCut=rows;target.Ratio=50;
            FinishPaneEdit(created);message="Added "+name+(rows?" below":" to the right")+". Layout pinned.";return true;
        }
        public bool RemovePane(out string message){
            message="";
            if(!IsStudio){message="Pane editing is available in Studio.";return false;}
            if(Root.Leaf){message="Keep at least one pane. Choose a different widget instead.";return false;}
            var target=Selected;
            var parent=Dividers.First(p=>p.A==target || p.B==target);
            var sibling=parent.A==target?parent.B:parent.A;
            if(parent==Root)Root=sibling;
            else{
                var grandparent=Dividers.First(p=>p.A==parent || p.B==parent);
                if(grandparent.A==parent)grandparent.A=sibling;else grandparent.B=sibling;
            }
            FinishPaneEdit(sibling.Leaves().First().Widget);
            message="Removed "+target.Widget.Name+". Neighboring panes fill the space.";return true;
        }
    }
}
