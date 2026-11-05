using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Technobabble;

internal static partial class Checks {
    static void Edit(Studio board,PaneEditAction action,string name){
        board.OpenPaneEditor();board.Panes.SetAction(action);board.Panes.SelectWidget(name);
        Check(board.ApplyPaneEdit(),"Edit failed: "+board.Panes.Message);
    }
    static void PaneEditorChecks(Machine host,Pictures images){
        var directory=Path.Combine(Path.GetTempPath(),"pane-editor-"+Guid.NewGuid().ToString("N"));
        var store=new LayoutStore(directory);var scene=new Scene(20,10,new Random(19),host,images);var board=new Studio(scene,store);
        try{
            scene.Resize(160,50);scene.LeaveStudio();string classic=scene.LayoutText(),message;
            board.OpenPaneEditor();Check(!board.Editing,"Classic opened editor");
            Check(!scene.EditPane(PaneEditAction.Replace,"gpu",out message) && !scene.RemovePane(out message) && !board.UndoPaneEdit() && scene.LayoutText()==classic,"Editor changed Classic");
            Check(store.Names().Contains(LayoutStore.BlankExample) && LayoutStore.IsExample(LayoutStore.BlankExample),"Blank Canvas missing");
            board.Load(LayoutStore.BlankExample);string blank=scene.LayoutText();
            Check(scene.PaneCount==1 && scene.Root.Pinned && scene.Root.Widget.Name=="cmatrix","Blank Canvas incorrect");
            board.OpenPaneEditor();Check(board.Editing && board.Panes.Choices.Length==Widget.Names.Length && !board.Panes.CanRemove,"Widget picker incomplete");
            Check(!board.RemovePane() && scene.LayoutText()==blank && !board.Panes.CanUndo,"Last pane was removed");board.Cancel();
            board.OpenPaneEditor();Check(board.ApplyPaneEdit() && !board.Editing && !board.Panes.CanUndo,"No-op made an undo entry");
            var rain=scene.Root.Widget;
            Edit(board,PaneEditAction.SplitColumns,"gpu");
            Check(!scene.Root.HorizontalCut && scene.Root.A.Widget==rain && scene.Root.B.Widget.Name=="gpu" && scene.Selected==scene.Root.B,"Column split or selection wrong");
            Edit(board,PaneEditAction.SplitRows,"disk");
            Check(scene.Root.B.HorizontalCut && scene.Root.B.A.Widget.Name=="gpu" && scene.Root.B.B.Widget.Name=="disk","Row split wrong");
            var disk=scene.Selected.Widget;var gpu=scene.Root.B.A.Widget;
            Edit(board,PaneEditAction.Replace,"gpu");
            Check(scene.Root.B.B.Widget==gpu && scene.Root.B.A.Widget==disk && scene.PaneCount==3,"Existing widget was duplicated instead of swapped");
            Edit(board,PaneEditAction.Replace,"process");
            Check(scene.Selected.Widget.Name=="process" && !scene.Root.Leaves().Any(p=>p.Widget.Name=="gpu"),"Replacement failed");
            Check(scene.Root.Leaves().All(p=>p.Pinned),"Manual choices were not pinned");
            string pinned=scene.LayoutText();scene.Shuffle();for(int i=0;i<200;i++)scene.Advance(.3);
            Check(scene.LayoutText()==pinned && scene.PaneCount==3,"Timed/manual shuffle destroyed edits");
            scene.SetRatio(scene.Root,60);scene.SetRatio(scene.Root.B,40);
            board.OpenPaneEditor();Check(board.RemovePane() && scene.Root.B.Widget==disk && scene.Root.Ratio==60 && scene.PaneCount==2,"Nested removal did not expand sibling");
            scene.SetRefresh(5);scene.SetNetwork(new NetworkChoice("test-nic","Chosen adapter"));scene.Processes.Configure(ProcessSort.Name,"technobabble");
            Check(board.UndoPaneEdit() && scene.Root.B.HorizontalCut && scene.Root.B.Ratio==40 && scene.Selected.Widget.Name=="process","Undo lost structure/selection");
            Check(scene.RefreshSeconds==5 && scene.Network.Id=="test-nic" && scene.Processes.Sort==ProcessSort.Name && scene.Processes.Filter=="technobabble","Undo reverted monitoring preferences");
            string custom=scene.LayoutText();
            board.Open(true);board.SetName("Custom");board.Submit();Check(!board.Editing && store.Read("Custom")==custom,"Save after edit failed");
            board.Leave();board.OpenPaneEditor();Check(!board.Editing && !board.UndoPaneEdit(),"Editor undo active in Classic");
            scene.Shuffle();for(int i=0;i<100;i++)scene.Advance(.2);scene.Resize(99,30);board.Enter();
            Check(scene.LayoutText()==custom && scene.Selected.Widget.Name=="process" && board.Panes.CanUndo,"Mode switch lost custom layout or undo");
            Check(board.TryLoad("Missing")!=null && board.Panes.CanUndo && scene.LayoutText()==custom,"Failed load discarded edits/undo");
            board.Load("Custom");Check(!board.Panes.CanUndo && scene.LayoutText()==custom,"Load did not reset edit history");
            scene.Select(scene.Root.A.Widget);board.OpenPaneEditor();Check(board.RemovePane() && scene.Root.HorizontalCut && scene.Root.Ratio==40 && scene.PaneCount==2,"Root removal lost sibling subtree");
            Check(board.UndoPaneEdit() && scene.LayoutText()==custom,"Root removal did not undo");
            board.Load(LayoutStore.BlankExample);scene.Resize(10,4);string small=scene.LayoutText();
            board.OpenPaneEditor();board.Panes.SetAction(PaneEditAction.SplitRows);board.Panes.SelectWidget("gpu");
            Check(!board.ApplyPaneEdit() && board.Panes.Message.Contains("too small") && scene.LayoutText()==small && !board.Panes.CanUndo,"Small split was not rejected atomically");
            board.Panes.SetAction(PaneEditAction.SplitColumns);Check(!board.ApplyPaneEdit() && scene.LayoutText()==small,"Small column split mutated state");board.Cancel();
            scene.Resize(160,50);Edit(board,PaneEditAction.SplitColumns,"gpu");
            board.OpenPaneEditor();board.PaneKey(new ConsoleKeyInfo('\t',ConsoleKey.Tab,true,false,false));
            Check(board.Panes.SelectedWidget=="cmatrix","Shift Tab did not choose previous pane");
            board.PaneKey(new ConsoleKeyInfo('r',ConsoleKey.R,false,false,false));board.PaneKey(new ConsoleKeyInfo('\0',ConsoleKey.End,false,false,false));
            Check(board.Panes.Choices[board.Panes.Choice].StartsWith("engineering -"),"End did not reach final widget");
            board.PaneKey(new ConsoleKeyInfo('\r',ConsoleKey.Enter,false,false,false));Check(scene.Root.A.Widget.Name=="engineering" && !board.Editing,"Terminal picker apply failed");
            board.OpenPaneEditor();board.PaneKey(new ConsoleKeyInfo('u',ConsoleKey.U,false,false,false));Check(scene.Root.A.Widget.Name=="cmatrix" && !board.Editing,"Terminal picker undo failed");
            foreach(var size in new[]{new Size(160,50),new Size(45,11),new Size(1,1)}){
                scene.Resize(size.Width,size.Height);board.OpenPaneEditor();var panel=board.Panel(size.Width,size.Height);
                Check(panel.W<=size.Width && panel.H<=size.Height,"Editor exceeded window");
#if TERMINAL
                TerminalScreen.Paint(scene,size.Width,size.Height+1,true,false,false,board);
#endif
                board.Cancel();
            }
            // Work through a balanced 20-pane canvas and reject the 21st without mutation.
            board.Load(LayoutStore.BlankExample);scene.Resize(240,120);
            foreach(var name in Widget.Names.Where(n=>n!="cmatrix").Take(19)){
                var largest=scene.Root.Leaves().OrderByDescending(p=>p.Bounds.Width*p.Bounds.Height).First();scene.Select(largest.Widget);
                Edit(board,largest.Bounds.Width>=largest.Bounds.Height*2?PaneEditAction.SplitColumns:PaneEditAction.SplitRows,name);
            }
            string full=scene.LayoutText();Check(scene.PaneCount==20,"20-pane canvas incomplete");
            board.OpenPaneEditor();board.Panes.SetAction(PaneEditAction.SplitRows);board.Panes.SelectWidget("gpu");
            Check(!board.ApplyPaneEdit() && board.Panes.Message.Contains("20-pane") && scene.LayoutText()==full,"Pane limit mutation");board.Cancel();
            for(int i=0;i<20;i++){
                board.OpenPaneEditor();Check(board.RemovePane(),"Removal sequence failed");
                if(scene.PaneCount==1)break;
            }
            Check(scene.PaneCount==1,"Removal did not converge to one pane");
            for(int i=0;i<20;i++)Check(board.UndoPaneEdit(),"Bounded history lost a recent edit");
            Check(!board.Panes.CanUndo && !board.UndoPaneEdit(),"Undo history exceeded 20 edits");
            board.Leave();Check(!host.StudioActive && scene.Root.Leaves().All(p=>!p.Pinned && Array.IndexOf(Widget.Names,p.Widget.Name)<Widget.ClassicCount),"Manual canvas leaked into Classic");
            Console.WriteLine("Pane editor: all widgets, both splits, swaps, root/nested removals, pin stability, undo, limits, tiny windows, save/load and Classic restoration passed.");
        }finally{board.Leave();var file=Path.Combine(directory,"Custom.layout");if(File.Exists(file))File.Delete(file);if(Directory.Exists(directory))Directory.Delete(directory);}
    }
}
