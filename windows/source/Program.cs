using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace Technobabble {
    internal sealed class Options {
        public int Count=Math.Min(Widget.ClassicCount,2*Environment.ProcessorCount),Delay=10;
        public bool Studio,Full,Help;
        public int RefreshSeconds;
#if TERMINAL
        public bool TerminalStream;
#endif
        public string Layout;
        public static Options Parse(string[] args){
            Options options=new Options();bool classic=false;
            for(int i=0;i<args.Length;i++){
                switch(args[i]){
#if TERMINAL
                    case "--terminal-stream":options.TerminalStream=true;break;
#endif
                    case "-s":case "--splits":options.Count=Number(args,ref i,1,Widget.ClassicCount);break;
                    case "-d":case "--delay":options.Delay=Number(args,ref i,1,86400);break;
                    case "--studio":options.Studio=true;break;
                    case "--classic":options.Studio=false;classic=true;break;
                    case "--refresh":
                        options.RefreshSeconds=Number(args,ref i,1,5);
                        if(!Machine.ValidRefresh(options.RefreshSeconds))throw new ArgumentException("--refresh expects 1, 2 or 5 seconds.");
                        break;
                    case "--layout":
                        if(++i>=args.Length || String.IsNullOrWhiteSpace(args[i]))throw new ArgumentException("--layout requires a saved layout name.");
                        options.Layout=args[i];break;
                    case "--fullscreen":options.Full=true;break;
                    case "--help":case "-h":options.Help=true;break;
                    default:throw new ArgumentException("Unknown option: "+args[i]);
                }
            }
            if(options.Layout!=null || options.RefreshSeconds!=0){
                if(classic)throw new ArgumentException("Layouts and monitor refresh settings are Studio features. Omit --classic to use them.");
                options.Studio=true;
            }
            return options;
        }
        static int Number(string[] args,ref int i,int low,int high) {
            int value;string flag=args[i++];
            if(i>=args.Length || !int.TryParse(args[i],NumberStyles.None,CultureInfo.InvariantCulture,out value) || value<low || value>high)
                throw new ArgumentException(flag+" expects an integer from "+low+" to "+high+".");
            return value;
        }
        public const string Usage="Technobabble.exe [-s 1..20] [-d seconds] [--studio] [--layout NAME] [--refresh 1|2|5] [--fullscreen]\n\n"+
            "Classic is the default. Pane count defaults to twice the CPU count, up to 20. Delay defaults to 10 seconds after the last split.\n\n"+
            "F1  Controls     F2  Classic / Studio     F11  Full screen\nSpace  Pause     R  Reshuffle     Ctrl+C  Quit\n"+
            "Studio: Y code typing speed, E edit panes, U undo pane edit, C Process Watch, O sort, F filter, N network adapter, B resize panes (or drag a divider).\n"+
            "T cycles monitor refresh (2s / 5s / 1s). --refresh overrides the loaded layout.";
    }
    internal static class Program {
        [STAThread] static int Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Options options;
            try{options=Options.Parse(args);}catch(ArgumentException e){MessageBox.Show(e.Message+"\n\n"+Options.Usage,"Technobabble options",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 2;}
            if(options.Help){MessageBox.Show(Options.Usage,"Technobabble for Windows");return 0;}
            try{using(var p=Process.GetCurrentProcess())p.PriorityClass=ProcessPriorityClass.BelowNormal;}
            catch(System.ComponentModel.Win32Exception){}
            using(var form=new TechnobabbleWindow(options)){
                if(options.Layout!=null){
                    string error=form.LoadStudioLayout(options.Layout);
                    if(error!=null){MessageBox.Show(error,"Open Studio layout",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 2;}
                }
                form.ApplyRefresh(options.RefreshSeconds);
                Application.Run(form);
            }
            return 0;
        }
    }
}
