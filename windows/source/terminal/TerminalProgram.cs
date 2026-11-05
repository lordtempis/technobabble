using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Technobabble {
    internal static class TerminalProgram {
        const string Usage="Technobabble-Terminal [-s 1..20] [-d seconds] [--studio | --classic] [--layout NAME] [--refresh 1|2|5]\n"+
            "Run in a PowerShell or CMD console, or a Windows Terminal tab.\n"+
            "Space pauses, R reshuffles, S toggles Studio, ? opens controls. Ctrl+L redraws. Q, Esc or Ctrl+C exits.\n"+
            "Studio: Tab selects, P pins, D disk, G GPU, C Process Watch, O sort, F filter, N network adapter, B resize panes.\n"+
            "Y on the code pane cycles scroll, slow typing, normal typing, fast typing.\n"+
            "E edits panes: R replace/swap, H split top/bottom, V split left/right, X remove, U undo, Enter apply.\n"+
            "Process Watch: Up/Down or PgUp/PgDn select. Resize: arrows move, Tab picks divider, 0 resets, Enter/Esc finishes.\n"+
            "T cycles monitor refresh (2s / 5s / 1s), W saves and L opens a layout. --refresh overrides the loaded layout.";
        [DllImport("kernel32")] static extern IntPtr GetStdHandle(int id);
        [DllImport("kernel32",SetLastError=true)] static extern bool GetConsoleMode(IntPtr handle,out uint mode);
        [DllImport("kernel32",SetLastError=true)] static extern bool SetConsoleMode(IntPtr handle,uint mode);
        [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool WriteConsole(IntPtr handle,string text,uint length,out uint written,IntPtr reserved);
        static Stream streamOutput;
        static void Output(IntPtr handle,string text){
            if(streamOutput!=null){byte[] bytes=Encoding.UTF8.GetBytes(text);streamOutput.Write(bytes,0,bytes.Length);streamOutput.Flush();return;}
            for(int pos=0;pos<text.Length;){
                int n=Math.Min(8192,text.Length-pos);
                string part=n==text.Length?text:text.Substring(pos,n);
                uint written;
                if(!WriteConsole(handle,part,(uint)n,out written,IntPtr.Zero) || written==0)
                    throw new IOException("Could not write to the terminal.",new Win32Exception());
                pos+=(int)written;
            }
        }
        static int Main(string[] args){
            try{
                Options o=Options.Parse(args);
                if(o.Help){Console.WriteLine(Usage);return 0;}
                if(o.Full)throw new ArgumentException("Use the terminal host's full-screen control.");
                if(o.TerminalStream)return RunStream(o);
                IntPtr input=GetStdHandle(-10),output=GetStdHandle(-11);
                uint inMode,outMode;
                if(!GetConsoleMode(input,out inMode) || !GetConsoleMode(output,out outMode)){
                    Console.Error.WriteLine("Open a PowerShell or Command Prompt console in Windows Terminal and run technobabble-terminal there. The animation needs interactive input and output. PowerShell ISE can run technobabble for the desktop version.");
                    return 2;
                }
                try{using(var p=Process.GetCurrentProcess())p.PriorityClass=ProcessPriorityClass.BelowNormal;}
                catch(Win32Exception){}
                var images=new Pictures();
                using(var machine=new Machine()){
                    var scene=new Scene(o.Count,o.Delay,new Random(),machine,images);
                    var board=new Studio(scene,LayoutStore.ForUser());
                    if(o.Layout!=null){
                        string error=board.TryLoad(o.Layout);
                        if(error!=null){Console.Error.WriteLine(error);return 2;}
                    }
                    bool cursor=Console.CursorVisible;
                    // Prepare the primary buffer for VT output; the alternate buffer gets its own mode.
                    if(!SetConsoleMode(output,(outMode|1u|4u)&~(2u|8u)))throw new IOException("This host does not support Windows virtual-terminal output.");
                    try{
                        // Read Ctrl+C as a key so the shell can regain its original screen and input mode.
                        if(!SetConsoleMode(input,(inMode|0x80u)&~(0x40u|1u|0x200u)))throw new IOException("This host does not support interactive console input.");
                        try{
                            // Entering the alternate buffer resets console modes, including wrap.
                            // Set DECAWM on that active buffer, after switching, so right-edge writes
                            // cannot wrap into the next row or scroll at the bottom-right cell.
                            Output(output,"\x1b[?1049h\x1b[?7l\x1b[?25l");
                            Play(scene,board,o,output);
                        }finally{
                            try{Output(output,"\x1b[0m\x1b[?1049l"+(cursor?"\x1b[?25h":"\x1b[?25l"));}
                            finally{SetConsoleMode(input,inMode);}
                        }
                    }finally{SetConsoleMode(output,outMode);}
                }
                return 0;
            }catch(ArgumentException e){Console.Error.WriteLine(e.Message+"\n"+Usage);return 2;}
            catch(IOException e){Console.Error.WriteLine(e.Message);return 1;}
        }
        static int RunStream(Options o){
            if(!Console.IsInputRedirected || !Console.IsOutputRedirected)throw new ArgumentException("Use the Ubuntu launcher for --terminal-stream.");
            var input=new StreamInput(Console.In);
            for(int i=0;i<100 && !input.Ready && !input.Closed;i++)Thread.Sleep(50);
            if(!input.Ready)throw new IOException("The terminal launcher did not send its window size.");
            using(var machine=new Machine()){
                var scene=new Scene(o.Count,o.Delay,new Random(),machine,new Pictures());
                var board=new Studio(scene,LayoutStore.ForUser());
                if(o.Layout!=null){string error=board.TryLoad(o.Layout);if(error!=null)throw new ArgumentException(error);}
                using(var output=Console.OpenStandardOutput()){
                    streamOutput=output;
                    try{Output(IntPtr.Zero,"\x1b[?1049h\x1b[?7l\x1b[?25l");Play(scene,board,o,IntPtr.Zero,input);}
                    finally{
                        try{Output(IntPtr.Zero,"\x1b[0m\x1b[?7h\x1b[?25h\x1b[?1049l");}
                        finally{streamOutput=null;}
                    }
                }
            }
            return 0;
        }
        static void Play(Scene scene,Studio board,Options o,IntPtr output,StreamInput input=null){
            if(o.Studio)board.Enter();
            if(o.RefreshSeconds!=0)scene.SetRefresh(o.RefreshSeconds);
            bool paused=false,help=false,studio=o.Studio;
            var clock=Stopwatch.StartNew();double prior=clock.Elapsed.TotalSeconds,nextRepaint=0;
            Grid previous=null;
            while(true){
                if(input!=null && input.Closed)return;
                bool changed=false,redraw=false;
                while(input!=null?input.KeyAvailable:Console.KeyAvailable){
                    ConsoleKeyInfo key=input!=null?input.ReadKey():Console.ReadKey(true);
                    changed=true;
                    if(key.KeyChar==3 || (key.Key==ConsoleKey.C && (key.Modifiers&ConsoleModifiers.Control)!=0))return;
                    if(key.KeyChar==12 || (key.Key==ConsoleKey.L && (key.Modifiers&ConsoleModifiers.Control)!=0)){redraw=true;continue;}
                    if(board.Panes.IsOpen){board.PaneKey(key);continue;}
                    if(board.Editing){
                        switch(key.Key){
                            case ConsoleKey.Escape:board.Cancel();break;
                            case ConsoleKey.Enter:board.Submit();break;
                            case ConsoleKey.Backspace:board.Backspace();break;
                            case ConsoleKey.UpArrow:board.Cycle(-1);break;
                            case ConsoleKey.DownArrow:board.Cycle(1);break;
                            default:
                                if(key.KeyChar==21)board.Clear();else board.Letter(key.KeyChar);
                                break;
                        }
                        continue;
                    }
                    if(studio && !help && board.ResizeKey(key.Key,(key.Modifiers&ConsoleModifiers.Shift)!=0))continue;
                    if(key.Key==ConsoleKey.Q)return;
                    if(board.Resizing && !help && key.Key!=ConsoleKey.S && key.Key!=ConsoleKey.F2 && key.Key!=ConsoleKey.F1 && key.KeyChar!='?')continue;
                    if(key.Key==ConsoleKey.Escape){if(!help)return;help=false;}
                    if(key.Key==ConsoleKey.Spacebar)paused=!paused;
                    if(key.Key==ConsoleKey.R)scene.Shuffle();
                    if(key.Key==ConsoleKey.S || key.Key==ConsoleKey.F2){studio=!studio;if(studio)board.Enter();else board.Leave();}
                    if(studio && !help){
                        if(board.ProcessKey(key.Key))continue;
                        if(key.Key==ConsoleKey.Tab)board.Step((key.Modifiers&ConsoleModifiers.Shift)!=0?-1:1);
                        if(key.Key==ConsoleKey.P)board.Pin();
                        if(key.Key==ConsoleKey.D)board.Disk();
                        if(key.Key==ConsoleKey.G)board.Gpu();
                        if(key.Key==ConsoleKey.T)board.Refresh();
                        if(key.Key==ConsoleKey.Y)board.CycleCodeStyle();
                        if(key.Key==ConsoleKey.F)board.OpenFilter();
                        if(key.Key==ConsoleKey.N)board.OpenNetwork();
                        if(key.Key==ConsoleKey.B)board.ToggleResize();
                        if(key.Key==ConsoleKey.E)board.OpenPaneEditor();
                        if(key.Key==ConsoleKey.U)board.UndoPaneEdit();
                        if(key.Key==ConsoleKey.W || key.Key==ConsoleKey.F5)board.Open(true);
                        if(key.Key==ConsoleKey.L || key.Key==ConsoleKey.F6)board.Open(false);
                    }
                    if(key.KeyChar=='?' || key.Key==ConsoleKey.F1)help=!help;
                    changed=true;
                }
                int w,h;
                if(input!=null)input.Size(out w,out h);
                else{w=Math.Max(1,Console.WindowWidth);h=Math.Max(1,Console.WindowHeight);}
                bool resized=previous==null || previous.W!=w || previous.H!=h;
                if(resized)scene.Resize(w,h-1);
                double now=clock.Elapsed.TotalSeconds;
                if(!paused && !help && !board.Editing){if(board.Resizing)scene.AdvanceFocused(Math.Min(.5,now-prior));else scene.Advance(Math.Min(.5,now-prior));}
                prior=now;
                // Hosts can reflow or invalidate the screen during an out-and-back resize that
                // occurs between polls. Repair it periodically, even while the animation is paused.
                bool repaint=redraw || now>=nextRepaint;
                if(changed || resized || repaint || (!paused && !help && !board.Editing)){
                    Grid frame=TerminalScreen.Paint(scene,w,h,studio,paused,help,board);
                    Output(output,TerminalScreen.Update(frame,previous,repaint));previous=frame;
                    if(repaint || resized)nextRepaint=now+2;
                }
                Thread.Sleep(80);
            }
        }
    }
}
