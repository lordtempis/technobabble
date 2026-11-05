using System;
using System.Globalization;
namespace Technobabble {
    internal sealed class Options {
        public int Count=Math.Min(Widget.ClassicCount,2*Environment.ProcessorCount),Delay=10;
        public bool Studio,Help,Version;
        public int RefreshSeconds;
        public string Layout;
        public static Options Parse(string[] args){
            Options options=new Options();bool classic=false;
            for(int i=0;i<args.Length;i++){
                switch(args[i]){
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
                    case "--version":options.Version=true;break;
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
    }
}
