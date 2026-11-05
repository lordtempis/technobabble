using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Technobabble {
    internal sealed class TechnobabbleWindow : Form {
        readonly Machine host;
        readonly Scene scene;
        readonly Studio board;
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        TerminalFont term;
        int fontSize=14,dpi;
        bool studio,full,paused,help,released;
        Widget focus;
        Pane dragging;
        double previous;
        Rectangle restore;
        FormWindowState restoreState;
        readonly ContextMenuStrip commands=new ContextMenuStrip();
        public TechnobabbleWindow(Options o) {
            AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;ResizeRedraw=true;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);
            BackColor=Ink.Colors[Ink.Black];MinimumSize=new Size(360,220);
            StartPosition=FormStartPosition.CenterScreen;
            var area=Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize=new Size(Math.Min(1250,area.Width-60),Math.Min(760,area.Height-80));
            WindowState=FormWindowState.Maximized;KeyPreview=true;studio=o.Studio;
            var images=new Pictures();host=new Machine();
            scene=new Scene(o.Count,o.Delay,new Random(),host,images);
            board=new Studio(scene,LayoutStore.ForUser());
            if(studio)board.Enter();
            MenuItem("Controls   F1",()=>Command(Keys.F1));MenuItem("Classic / Windows Studio   F2",()=>Command(Keys.F2));
            MenuItem("Full screen   F11",()=>Command(Keys.F11));MenuItem("Pause / resume   Space",()=>Command(Keys.Space));
            MenuItem("New arrangement   R",()=>Command(Keys.R));MenuItem("Quit   Ctrl+C",Close);
            var pin=commands.Items.Add("Pin / unpin selected pane   P",null,delegate{Command(Keys.P);});
            var gpu=commands.Items.Add("Show GPU activity in selected pane   G",null,delegate{Command(Keys.G);});
            var disk=commands.Items.Add("Show disk activity in selected pane   D",null,delegate{Command(Keys.D);});
            var refresh=commands.Items.Add("Cycle monitor refresh (2s / 5s / 1s)   T",null,delegate{Command(Keys.T);});
            var processes=commands.Items.Add("Show Process Watch   C",null,delegate{Command(Keys.C);});
            var filter=commands.Items.Add("Filter processes   F",null,delegate{Command(Keys.F);});
            var sort=commands.Items.Add("Sort processes   O",null,delegate{Command(Keys.O);});
            var network=commands.Items.Add("Choose network adapter   N",null,delegate{Command(Keys.N);});
            var sizing=commands.Items.Add("Resize panes   B (or drag a divider)",null,delegate{Command(Keys.B);});
            var edit=commands.Items.Add("Edit panes   E",null,delegate{Command(Keys.E);});
            var typing=commands.Items.Add("Cycle code typing speed / scroll   Y",null,delegate{Command(Keys.Y);});
            var undo=commands.Items.Add("Undo pane edit   U",null,delegate{Command(Keys.U);});
            var save=commands.Items.Add("Save Studio layout   F5",null,delegate{Command(Keys.F5);});
            var open=commands.Items.Add("Open Studio layout   F6",null,delegate{Command(Keys.F6);});
            commands.Opening+=delegate{
                pin.Visible=gpu.Visible=disk.Visible=refresh.Visible=processes.Visible=network.Visible=sizing.Visible=edit.Visible=undo.Visible=save.Visible=open.Visible=studio && !board.Editing;
                undo.Enabled=board.Panes.CanUndo;
                typing.Visible=studio && !board.Editing && board.CodeSelected;
                filter.Visible=sort.Visible=studio && !board.Editing && board.ProcessSelected;
            };
            ContextMenuStrip=commands;Title();
            timer.Interval=80;timer.Tick+=delegate{
                double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.5,now-previous);previous=now;
                if(paused||help||board.Editing||WindowState==FormWindowState.Minimized)return;
                // A focused Studio pane stays in the current arrangement until released.
                if(focus!=null || dragging!=null || board.Resizing)scene.AdvanceFocused(dt);else scene.Advance(dt);
                Invalidate();
            };
            Shown+=delegate{if(o.Full)Fullscreen();previous=clock.Elapsed.TotalSeconds;timer.Start();};
        }
        void MenuItem(string name,Action a){commands.Items.Add(name,null,delegate{a();});}
        public string LoadStudioLayout(string name){var error=board.TryLoad(name);Title();return error;}
        public void ApplyRefresh(int seconds){if(seconds!=0)scene.SetRefresh(seconds);}
        void Title(){Text="Technobabble  |  "+(studio?"Windows Studio":"Classic")+(studio && board.ActiveName!=null?"  |  "+board.ActiveName:"")+(paused?"  |  Paused":"")+"  |  F1 Controls";}
        void Fullscreen(){
            if(!full){
                restoreState=WindowState;restore=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
                var bounds=Screen.FromControl(this).Bounds;
                WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;Bounds=bounds;full=true;
            }else{
                full=false;FormBorderStyle=FormBorderStyle.Sizable;Bounds=restore;WindowState=restoreState;
            }
        }
        bool Command(Keys key){
            if(studio && !help){
                if(board.ResizeKey((ConsoleKey)(key&Keys.KeyCode),(key&Keys.Shift)!=0)){Invalidate();return true;}
                if(board.Resizing && key!=Keys.F1 && key!=Keys.F2 && key!=Keys.F11 && key!=(Keys.Control|Keys.C))return true;
                if((key&Keys.Modifiers)==Keys.None && board.ProcessKey((ConsoleKey)key)){if(key==Keys.C)focus=null;Invalidate();return true;}
                switch(key){
                    case Keys.Tab:case Keys.Shift|Keys.Tab:
                        focus=null;board.Step(key==Keys.Tab?1:-1);Invalidate();return true;
                    case Keys.P:board.Pin();Invalidate();return true;
                    case Keys.G:focus=null;board.Gpu();Invalidate();return true;
                    case Keys.D:focus=null;board.Disk();Invalidate();return true;
                    case Keys.T:board.Refresh();Invalidate();return true;
                    case Keys.Y:board.CycleCodeStyle();Invalidate();return true;
                    case Keys.F:LayoutDialog.Filter(this,board);Invalidate();return true;
                    case Keys.N:LayoutDialog.Network(this,board);Invalidate();return true;
                    case Keys.B:focus=null;board.ToggleResize();Invalidate();return true;
                    case Keys.E:
                        focus=null;dragging=null;Capture=false;PaneEditorDialog.Show(this,board);Title();Invalidate();return true;
                    case Keys.U:
                        focus=null;dragging=null;Capture=false;board.UndoPaneEdit();Invalidate();return true;
                    case Keys.F5:case Keys.F6:
                        focus=null;LayoutDialog.Show(this,board,key==Keys.F5);
                        Title();Invalidate();return true;
                }
            }
            switch(key){
                case Keys.Control|Keys.C: Close();return true;
                case Keys.Escape:
                    if(help)help=false;else if(focus!=null)focus=null;else if(full)Fullscreen();else Close();break;
                case Keys.F1:help=!help;break;
                case Keys.F2:studio=!studio;focus=null;dragging=null;Capture=false;if(studio)board.Enter();else board.Leave();break;
                case Keys.F11:Fullscreen();break;
                case Keys.Space:paused=!paused;break;
                case Keys.R:focus=null;scene.Shuffle();break;
                case Keys.Add:case Keys.Oemplus:fontSize=Math.Min(26,fontSize+1);ReleaseTerminalFont();break;
                case Keys.Subtract:case Keys.OemMinus:fontSize=Math.Max(9,fontSize-1);ReleaseTerminalFont();break;
                default:return false;
            }
            Title();Invalidate();return true;
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){
            return Command(keys)||base.ProcessCmdKey(ref msg,keys);
        }
        protected override void OnMouseDown(MouseEventArgs e){
            base.OnMouseDown(e);
            if(!studio||term==null||help||board.Editing||focus!=null||e.Button!=MouseButtons.Left)return;
            Point pos=new Point(e.X/term.W,e.Y/term.H);
            var divider=scene.DividerAt(pos);
            if(divider!=null){dragging=divider;Capture=true;return;}
            var p=scene.Root.Leaves().FirstOrDefault(n=>n.Bounds.Contains(pos));
            if(p!=null){scene.Select(p.Widget);if(board.ProcessSelected){scene.UpdateProcesses();scene.Processes.ClickRow(pos.Y-p.Bounds.Y-(p.Bounds.Height>=4?1:0)-3);}Invalidate();}
        }
        protected override void OnMouseMove(MouseEventArgs e){
            base.OnMouseMove(e);if(term==null)return;
            Point cell=new Point(e.X/term.W,e.Y/term.H);
            if(dragging!=null){scene.DragDivider(dragging,cell);Invalidate();return;}
            var divider=studio && focus==null && !help && !board.Editing?scene.DividerAt(cell):null;
            Cursor=divider==null?Cursors.Default:divider.HorizontalCut?Cursors.HSplit:Cursors.VSplit;
        }
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=null;Capture=false;}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=null;}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(studio && !help && !board.Editing && !board.Resizing && board.ProcessSelected){scene.UpdateProcesses();scene.Processes.Move(e.Delta>0?-3:3);Invalidate();}}
        protected override void OnMouseDoubleClick(MouseEventArgs e){
            base.OnMouseDoubleClick(e);if(!studio||term==null||help||board.Editing||e.Button!=MouseButtons.Left)return;
            if(focus!=null)focus=null;
            else{
                Point p=new Point(e.X/term.W,e.Y/term.H);
                var pane=scene.Root.Leaves().FirstOrDefault(node=>node.Bounds.Contains(p));if(pane!=null)focus=pane.Widget;
            }
            Invalidate();
        }
        void ReleaseTerminalFont(){if(term!=null){term.Dispose();term=null;}}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.Clear(BackColor);
            if(scene==null || ClientSize.Width<1 || ClientSize.Height<1)return;
            int currentDpi=(int)Math.Round(g.DpiX);
            if(term==null || dpi!=currentDpi){ReleaseTerminalFont();dpi=currentDpi;term=new TerminalFont(g,(int)Math.Round(fontSize*dpi/96.0));}
            int cols=Math.Max(1,ClientSize.Width/term.W),rows=Math.Max(1,ClientSize.Height/term.H);
            scene.Resize(cols,rows-1);
            if(focus==null)DrawBorders(g,scene.Root);
            IntPtr dc=g.GetHdc(),old=term.Begin(dc);
            try{
                if(focus==null){foreach(var pane in scene.Root.Leaves())DrawPane(dc,pane.Widget,pane.Bounds);}
                else DrawPane(dc,focus,new Rectangle(0,0,cols,rows-1));
                var status=new Grid(cols,1);byte bg=studio?Ink.Blue:Ink.Green,fg=studio?Ink.Bright:Ink.Black;
                string left=studio?" STUDIO | "+board.Status()+(focus==null?"":" [FOCUS]"):"[technobabble] 0:technobabble* ";
                if(paused)left+=" [PAUSED]";
                string right=studio?"F1 controls   "+DateTime.Now.ToString("HH:mm:ss")+" ":" \"technobabble\" "+DateTime.Now.ToString("HH:mm  dd-MMM-yy")+" ";
                status.Band(0,left,fg,bg);
                if(cols-right.Length>left.Length)status.Put(cols-right.Length,0,right,fg,bg);
                term.Draw(dc,status,0,rows-1);
                if(help)DrawHelp(dc,cols,rows);
            }finally{term.End(dc,old);g.ReleaseHdc(dc);}
        }
        void DrawBorders(Graphics g,Pane p){
            if(p.Leaf || p.Bounds.IsEmpty)return;
            var r=p.Bounds;var a=p.A.Bounds;
            using(var pen=new Pen(studio?Ink.Colors[board.Resizing && board.ResizeDivider==p?Ink.Gold:Ink.Blue]:Ink.Colors[Ink.Gray],Math.Max(1,dpi/96))){
                if(p.HorizontalCut){int y=a.Bottom*term.H+term.H/2;g.DrawLine(pen,r.Left*term.W-term.W/2,y,r.Right*term.W+term.W/2,y);}
                else{int x=a.Right*term.W+term.W/2;g.DrawLine(pen,x,r.Top*term.H-term.H/2,x,r.Bottom*term.H+term.H/2);}
            }
            DrawBorders(g,p.A);DrawBorders(g,p.B);
        }
        void DrawPane(IntPtr dc,Widget widget,Rectangle r) {
            if(r.Width<=0||r.Height<=0)return;
            if(studio && r.Height>=4){
                Grid bar=new Grid(r.Width,1);bar.Band(0,scene.Label(widget),Ink.Aqua,Ink.Black);
                term.Draw(dc,bar,r.X,r.Y);r.Y++;r.Height--;
            }
            term.Draw(dc,scene.WidgetFrame(widget,r.Width,r.Height),r.X,r.Y);
        }
        void DrawHelp(IntPtr dc,int cols,int rows){
            string[] lines={"TECHNOBABBLE FOR WINDOWS","",
                "F1       Close controls","F2       Switch Classic / Windows Studio",
                "F11      Toggle full screen","Space    Pause / resume","R        Reshuffle panes",
                "+ / -    Change type size","Ctrl+C   Quit","Esc      Back / exit","",
                "Studio: double-click a pane to focus it.","Double-click again or press Esc to return."};
            if(studio)lines=lines.Concat(new[]{"Click / Tab  Pane     P  Pin/unpin","D Disk  G GPU  C Process Watch","Up/Down / PgUp/PgDn  Select process","O Sort processes    F Filter","N Choose network adapter","T Monitor refresh: 2s / 5s / 1s","B Resize: arrows move, Tab divider","0 Reset divider     Enter Done","Or drag a divider with the mouse","E Edit panes        U Undo pane edit","Y Code: scroll / slow / normal / fast","F5 Save layout      F6 Open layout"}).ToArray();
            int w=Math.Min(49,cols),h=Math.Min(lines.Length+2,rows);
            Grid panel=new Grid(w,h);
            for(int i=0;i<h;i++){panel.Band(i,"",Ink.White,Ink.Blue);if(i>0&&i<=lines.Length)panel.Put(2,i,lines[i-1],i==1?Ink.Gold:Ink.Bright,Ink.Blue);}
            term.Draw(dc,panel,Math.Max(0,(cols-w)/2),Math.Max(0,(rows-h)/2));
        }
        protected override void Dispose(bool disposing){
            if(disposing && !released){released=true;timer.Dispose();ReleaseTerminalFont();commands.Dispose();if(host!=null)host.Dispose();}
            base.Dispose(disposing);
        }
    }
}
