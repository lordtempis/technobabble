using System;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Technobabble {
    internal static class TerminalScreen {
        public static Grid Paint(Scene scene,int cols,int rows,bool studio,bool paused,bool help,Studio board=null) {
            Grid screen=new Grid(cols,rows);
            PaintNode(screen,scene.Root,scene,studio,board!=null && board.Resizing?board.ResizeDivider:null);
            string left=studio?" TECHNOBABBLE / STUDIO ":"[technobabble] 0:technobabble* ";
            if(paused)left+="[PAUSED] ";
            if(studio && board!=null)left+=board.Status(cols-left.Length);
            string hint="? help  Q quit ";
            byte bg=studio?Ink.Blue:Ink.Green,fg=studio?Ink.Bright:Ink.Black;
            screen.Band(rows-1,left,fg,bg);
            if(cols>=left.Length+hint.Length)screen.Put(cols-hint.Length,rows-1,hint,fg,bg);
            if(help){
                string[] lines={" TECHNOBABBLE TERMINAL ",""," Space    Pause / resume"," R        Reshuffle panes",
                    " S / F2   Classic / Studio"," ? / F1   Close controls"," Ctrl+L   Redraw the screen"," Q / Esc  Quit"," Ctrl+C   Quit"};
                if(studio)lines=lines.Concat(new[]{" Tab      Select pane   P Pin/unpin"," D Disk   G GPU   C Process Watch"," Up/Down / PgUp/PgDn  Select process"," O Sort processes   F Filter"," N        Choose network adapter"," T        Refresh: 2s / 5s / 1s"," B Resize: arrows move, Tab divider"," 0 Reset divider    Enter/Esc Done"," E Edit panes   U Undo pane edit"," Y Code: scroll / slow / normal / fast"," W / L    Save / open layout"}).ToArray();
                int w=Math.Min(40,cols),h=Math.Min(lines.Length,rows);
                int x=(cols-w)/2,y=(rows-h)/2;
                for(int i=0;i<h;i++)screen.Put(x,y+i,lines[i].PadRight(w).Substring(0,w),i==0?Ink.Gold:Ink.Bright,Ink.Blue);
            }
            if(board!=null && board.Editing){
                Grid panel=board.Panel(cols,rows);
                int x=(cols-panel.W)/2,y=(rows-panel.H)/2;
                for(int j=0;j<panel.H;j++)for(int i=0;i<panel.W;i++)
                    screen.Set(x+i,y+j,panel.Ch[j][i],panel.Fg[j][i],panel.Bg[j][i]);
            }
            return screen;
        }
        static void PaintNode(Grid screen,Pane p,Scene scene,bool studio,Pane divider) {
            Rectangle r=p.Bounds;
            if(r.Width<=0 || r.Height<=0)return;
            if(!p.Leaf){
                byte color=p==divider?Ink.Gold:Ink.Gray;
                if(p.HorizontalCut)screen.Put(r.X,p.A.Bounds.Bottom,new string('─',r.Width),color);
                else for(int y=r.Top;y<r.Bottom;y++)screen.Set(p.A.Bounds.Right,y,'│',color);
                PaintNode(screen,p.A,scene,studio,divider);PaintNode(screen,p.B,scene,studio,divider);
                return;
            }
            if(studio && r.Height>=4){
                screen.Put(r.X,r.Y,scene.Label(p.Widget).PadRight(r.Width).Substring(0,r.Width),Ink.Aqua);
                r.Y++;r.Height--;
            }
            Grid frame=scene.WidgetFrame(p.Widget,r.Width,r.Height);
            for(int y=0;y<r.Height;y++){
                Array.Copy(frame.Ch[y],0,screen.Ch[r.Y+y],r.X,r.Width);
                Array.Copy(frame.Fg[y],0,screen.Fg[r.Y+y],r.X,r.Width);
                Array.Copy(frame.Bg[y],0,screen.Bg[r.Y+y],r.X,r.Width);
            }
        }
        static readonly string[] foreground=Colors(38),background=Colors(48);
        static string[] Colors(int role){
            var values=new string[Ink.Colors.Length];
            for(int i=0;i<values.Length;i++){
                Color c=Ink.Colors[i];values[i]="\x1b["+role+";2;"+c.R+";"+c.G+";"+c.B+"m";
            }
            return values;
        }
        static bool Same(Grid a,Grid b,int x,int y){
            return a.Ch[y][x]==b.Ch[y][x] && a.Fg[y][x]==b.Fg[y][x] && a.Bg[y][x]==b.Bg[y][x];
        }
        public static string Update(Grid screen,Grid prev,bool repaint=false){
            bool resized=prev==null || screen.H!=prev.H || screen.W!=prev.W;
            bool full=resized || repaint;
            // Repair in place: overwriting cells avoids a visible clear-screen flash.
            var s=new StringBuilder(resized?"\x1b[2J":"");int fg=-1,bg=-1;
            for(int y=0;y<screen.H;y++){
                int x=0,end=screen.W;
                if(!full){
                    while(x<end && Same(screen,prev,x,y))x++;
                    while(end>x && Same(screen,prev,end-1,y))end--;
                }
                if(x==end)continue;
                s.Append("\x1b[").Append(y+1).Append(';').Append(x+1).Append('H');
                for(;x<end;x++){
                    if(fg!=screen.Fg[y][x]){fg=screen.Fg[y][x];s.Append(foreground[fg]);}
                    if(bg!=screen.Bg[y][x]){bg=screen.Bg[y][x];s.Append(background[bg]);}
                    char ch=screen.Ch[y][x];
                    // Widget data is text, including OS-provided names. Never emit embedded controls.
                    s.Append(char.IsControl(ch)||char.IsSurrogate(ch)?' ':ch);
                }
            }
            return s.ToString();
        }
    }
}
