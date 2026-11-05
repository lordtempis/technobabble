using System;
using System.Diagnostics;
using System.Threading;
namespace Technobabble {
    internal static partial class LinuxProgram {
        static void Play(Scene scene,Studio board,Options o,Func<bool> stopped){
            if(o.Studio)board.Enter();
            if(o.RefreshSeconds!=0)scene.SetRefresh(o.RefreshSeconds);
            bool paused=false,help=false,studio=o.Studio;
            var clock=Stopwatch.StartNew();double prior=clock.Elapsed.TotalSeconds,nextRepaint=0;
            Grid previous=null;
            while(true){
                if(stopped())return;
                bool changed=false,redraw=false;
                while(Console.KeyAvailable){
                    ConsoleKeyInfo key=Console.ReadKey(true);
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
                w=Math.Clamp(Console.WindowWidth,1,1000);h=Math.Clamp(Console.WindowHeight,1,500);
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
                    Console.Write(TerminalScreen.Update(frame,previous,repaint));previous=frame;
                    if(repaint || resized)nextRepaint=now+2;
                }
                Thread.Sleep(80);
            }
        }
    }
}
