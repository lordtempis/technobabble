using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Technobabble {
    internal sealed partial class Widget {
        public const int ClassicCount=20;
        public static readonly string[] Names="apg atop bat bmon cmatrix code errno figlet hexdump htop jp2a logs man map mplayer pv speedometer sshart stat tree disk gpu process radar signal engineering".Split(' ');
        // Independent widgets use these intervals; relay panes use the shared feed's tick.
        static readonly double[] Intervals={.2,1,3,.75,.10,.24,.6,2,.7,1,.57,.5,.2,1,.10,.12,.75,3,.8,.5,.5,.5,.5};
        readonly int kind,seed;
        readonly Machine host;
        readonly Pictures pictures;
        readonly ProcessWatch processWatch;
        readonly double birth;
        int last=-1;
        Grid cached;
        RelayFrame lastRelay;
        bool cachedStudioCode;
        CodeStyle cachedCodeStyle;
        StudioCodeStream studioCode;
        public string Name { get{return Names[kind];} }
        public bool IsRelay { get { return Name=="radar" || Name=="signal" || Name=="engineering"; } }
        public Widget(int type,int seed,Machine machine,Pictures images,double now,ProcessWatch processes=null) {
            kind=type;this.seed=seed;host=machine;pictures=images;birth=now;
            if(Name=="disk")host.RequestDisks();
            if(Name=="gpu")host.RequestGpus();
            if(Name=="process"){processWatch=processes??new ProcessWatch();}
        }
        static string[] ReadText(string resource) {
            using(var r=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)))
                return r.ReadToEnd().Replace("\r","").Split('\n');
        }
        static readonly string[][] Code={ReadText("Terminal.cs"),ReadText("Machine.cs"),ReadText("Scene.cs")};
        static readonly string[] SourceNames={"Terminal.cs","Machine.cs","Scene.cs"};
        static readonly string[] Font=ReadText("standard.flf");
        static readonly string[] Words="HACKED ENCRYPTED DECRYPTING FIREWALL OVERRIDE SCANNING DOWNLOAD BYPASSING CRACKING INTERCEPT PAYLOAD ACCESS DENIED GRANTED LOCKED TRACING BREACH ENABLED SYNCING UPLINK".Split(' ');
        static readonly string[] Errors={
            "EPERM 1 Operation not permitted","ENOENT 2 No such file or directory","ESRCH 3 No such process","EINTR 4 Interrupted system call","EIO 5 Input/output error",
            "ENXIO 6 No such device or address","E2BIG 7 Argument list too long","ENOEXEC 8 Exec format error","EBADF 9 Bad file descriptor","ECHILD 10 No child processes",
            "EAGAIN 11 Resource temporarily unavailable","ENOMEM 12 Cannot allocate memory","EACCES 13 Permission denied","EFAULT 14 Bad address","EBUSY 16 Device or resource busy",
            "EEXIST 17 File exists","EXDEV 18 Invalid cross-device link","ENODEV 19 No such device","ENOTDIR 20 Not a directory","EISDIR 21 Is a directory",
            "EINVAL 22 Invalid argument","ENFILE 23 Too many open files in system","EMFILE 24 Too many open files","ENOTTY 25 Inappropriate ioctl for device","ENOSPC 28 No space left on device",
            "EPIPE 32 Broken pipe","ERANGE 34 Numerical result out of range","ELOOP 40 Too many levels of symbolic links","ECONNRESET 104 Connection reset by peer","ETIMEDOUT 110 Connection timed out"};
        static readonly string[][] Manuals={
            new[]{"TECHNOBABBLE(1)","NAME","    technobabble - split a screen into terminal widgets","SYNOPSIS","    technobabble [-s count] [-d seconds]","DESCRIPTION","    Randomly split the display into panes.","    Each pane runs its own visual program.","    The bottom-right pane survives each shuffle.","OPTIONS","    -s, --splits    maximum pane count","    -d, --delay     seconds between arrangements","SEE ALSO","    cmatrix, htop, figlet, ssh-keygen"},
            new[]{"HEXDUMP(1)","NAME","    hexdump - display bytes in hexadecimal","SYNOPSIS","    hexdump -C file","DESCRIPTION","    Show the byte offset, sixteen hex bytes","    and their printable character equivalents.","    A dot represents a nonprinting byte.","OUTPUT","    00000000  4d 5a 90 00  MZ..","SEE ALSO","    od, xxd, stat"},
            new[]{"TREE(1)","NAME","    tree - display a directory hierarchy","SYNOPSIS","    tree [directory]","DESCRIPTION","    Print the root directory followed by its","    descendants. Branches indicate nesting.","    The Linux display samples system directories.","FILES","    /etc","    /usr/share","SEE ALSO","    stat, find, ls"},
            new[]{"PV(1)","NAME","    pv - monitor a data stream","SYNOPSIS","    pv [options]","DESCRIPTION","    Report the amount transferred, elapsed time,","    transfer rate and estimated time remaining.","OPTIONS","    -p   draw a progress bar","    -r   show the transfer rate","    -s   set the expected size","SEE ALSO","    dd, cat, time"}
        };
        int Hash(int a,int b) {
            unchecked {
                uint n=(uint)seed+(uint)a*0x9e3779b9u+(uint)b*0x85ebca6bu;
                n^=n>>16;n*=0x7feb352du;n^=n>>15;n*=0x846ca68bu;n^=n>>16;
                return (int)(n&0x7fffffffu);
            }
        }
        public Grid Frame(int w,int h,double now,CodeStyle codeStyle=CodeStyle.Scroll,RelayFeed relay=null) {
            if (IsRelay) {
                if (!host.StudioActive || relay == null) return new Grid(w,h);
                var current = relay.At(now);
                if (lastRelay != current || cached.W != w || cached.H != h) {
                    cached = RelayPanes.Draw(Name,w,h,current);
                    lastRelay = current;
                }
                return cached;
            }
            if(Name=="disk")host.RequestDisks();
            if(Name=="gpu")host.RequestGpus();
            if(Name=="process"){var processGrid=new Grid(w,h);ProcessPane(processGrid);return processGrid;}
            double age=Math.Max(0,now-birth);
            int t=(int)(age/Intervals[kind]);
            bool studioCodeActive=host.StudioActive && Name=="code";
            if(!studioCodeActive)codeStyle=CodeStyle.Scroll;
            int frameTick=codeStyle==CodeStyle.Scroll?t:(int)(age/.04);
            if(cached!=null && cached.W==w && cached.H==h && last==frameTick && cachedStudioCode==studioCodeActive && cachedCodeStyle==codeStyle)return cached;
            bool flow=(Name=="code" && !studioCodeActive)||Name=="logs"||Name=="tree"||Name=="stat"||Name=="hexdump"||Name=="errno"||Name=="man";
            Grid g=new Grid(flow?Math.Max(100,w):w,h);
            switch(Name){
                case "apg": Passwords(g,t);break;
                case "atop": case "htop": Processes(g,Name=="htop");break;
                case "bat": Source(g,t,true);break;
                case "code":
                    if(studioCodeActive){
                        if(studioCode==null)studioCode=new StudioCodeStream(seed);
                        studioCode.Draw(g,t,age,codeStyle);
                    }else Source(g,t,false);
                    break;
                case "bmon": case "speedometer": Network(g,t,Name=="bmon"&&w>=48&&h>=11);break;
                case "cmatrix": Rain(g,t);break;
                case "errno": ErrorList(g,t);break;
                case "figlet": Banner(g,t);break;
                case "hexdump": Hex(g,t);break;
                case "jp2a": case "map": case "mplayer": Picture(g,t);break;
                case "logs": Logs(g,t);break;
                case "man": Manual(g,t);break;
                case "pv": DrawTransfer(g,age);break;
                case "sshart": Fingerprint(g,t);break;
                case "stat": Stats(g,t);break;
                case "tree": Tree(g,t);break;
                case "disk": Disk(g,host.Latest.Disks,t);break;
                case "gpu": Gpu(g,host.Latest.Gpus,t);break;
            }
            cachedStudioCode=studioCodeActive;
            cachedCodeStyle=codeStyle;
            cached=flow?g.Wrap(w,Name=="hexdump"||Name=="logs"||Name=="stat"):g;last=frameTick;return cached;
        }
        void Passwords(Grid g,int t) {
            const string c="bcdfghjklmnprstvwxz",v="aeiouy";
            for(int y=0;y<Math.Min(t+1,g.H);y++){
                int n=Math.Max(0,t-g.H+1)+y;StringBuilder s=new StringBuilder();
                for(int k=0;k<5+Hash(n,1)%6;k++){string bank=k%2==0?c:v;s.Append(bank[Hash(n,k+8)%bank.Length]);}
                if(Hash(n,2)%2==0)s.Append(Hash(n,3)%100);
                g.Put(0,y,s.ToString(),(byte)(1+Hash(n,4)%6));
            }
        }
        void Rain(Grid g,int t) {
            const string glyph="0123456789ABCDEF:<>+-=;@#$%&*abcdefghijklmnopqrstuvwxyz";
            for(int x=0;x<g.W;x++){
                int len=4+Hash(x,1)%19, period=Math.Max(g.H+len,14)+Hash(x,2)%24;
                int head=(Hash(x,3)%period+t/(1+Hash(x,4)%3))%period;
                for(int y=Math.Max(0,head-len);y<=Math.Min(head,g.H-1);y++){
                    int distance=head-y;byte fg=distance==0?Ink.Bright:distance<4?Ink.Lime:distance<len*2/3?Ink.Green:(byte)28;
                    g.Set(x,y,glyph[Hash(x,y+t/5)%glyph.Length],fg);
                }
            }
        }
        void ErrorList(Grid g,int t){
            for(int y=0;y<g.H;y++){
                string s=Errors[Hash(t,g.H-1-y)%Errors.Length];int p=s.IndexOf(' ');
                g.Put(0,y,s.Substring(0,p),Ink.Coral);g.Put(p,y,s.Substring(p),Ink.White);
            }
        }
        void Banner(Grid g,int t) {
            string word=Words[Hash(t,1)%Words.Length];int x=0,y=0;
            int skip=1+Int32.Parse(Font[0].Split(' ')[5]);
            foreach(char c in word){
                int start=skip+(c-32)*6;int width=Font[start].TrimEnd('@').Length;
                if(x+width>g.W && x>0){x=0;y+=6;}
                for(int r=0;r<6;r++)g.Put(x,y+r,Font[start+r].TrimEnd('@').Replace('$',' '),Ink.White);
                x+=Math.Max(1,width-1);
            }
        }
        void Picture(Grid g,int t) {
            if(Name=="mplayer"){
                int frame=(Hash(1,2)%100+t*10)%208;
                pictures.Wave.Paint(g,new Rectangle((frame%16)*96,(frame/16)*54,96,54),true,true);
            }else{
                Pixels p=Name=="map"?pictures.Map:pictures.Photos[Hash(t,7)%pictures.Photos.Count];
                p.Paint(g,new Rectangle(0,0,p.W,p.H),Name=="jp2a",false);
            }
        }
        void Source(Grid g,int t,bool numbered) {
            int page=numbered?t:t/12,which=Hash(page,1)%Code.Length;
            string[] lines=Code[which];int from=Hash(page,2)%Math.Max(1,lines.Length-g.H)+(numbered?0:2*(t%12));
            if(numbered){g.Put(0,0,"───────┬"+new string('─',g.W),Ink.Gray);g.Put(0,1,"       │ File: "+SourceNames[which],Ink.Aqua);g.Put(0,2,"───────┼"+new string('─',g.W),Ink.Gray);}
            for(int y=numbered?3:0;y<g.H;y++){
                int num=(from+y)%lines.Length;
                if(numbered)g.Put(0,y,(num+1).ToString().PadLeft(6)+" │ ",Ink.Gray);
                Syntax(g,numbered?9:0,y,lines[num]);
            }
        }
        static void Syntax(Grid g,int offset,int y,string s){
            g.Put(offset,y,s,Ink.White);
            for(int i=0;i<s.Length && offset+i<g.W;){
                int j=i+1;byte color=Ink.White;
                if(s[i]=='/' && j<s.Length && s[j]=='/'){g.Put(offset+i,y,s.Substring(i),Ink.Gray);break;}
                if(s[i]=='"'||s[i]=='\''){char quote=s[i];while(j<s.Length && s[j]!=quote)j++;j=Math.Min(j+1,s.Length);color=Ink.Gold;}
                else if(char.IsLetterOrDigit(s[i])||s[i]=='_'){
                    while(j<s.Length && (char.IsLetterOrDigit(s[j])||s[j]=='_'))j++;
                    string tok=s.Substring(i,j-i);
                    if(" public static readonly private internal void sealed return if else foreach for new using int double bool string class try catch override in ".Contains(" "+tok+" "))color=Ink.Pink;
                    else if(char.IsDigit(s[i]))color=Ink.Gold;
                    else if(j<s.Length && s[j]=='(')color=Ink.Aqua;
                    else if(char.IsUpper(s[i]))color=Ink.Sky;
                }
                if(color!=Ink.White)g.Put(offset+i,y,s.Substring(i,j-i),color);i=j;
            }
        }
        void Manual(Grid g,int t) {
            string[] doc=Manuals[Hash(t/7,5)%Manuals.Length];int offset=g.H<doc.Length?(t%7)*2:0;
            for(int y=0;y<g.H && offset+y<doc.Length;y++){
                string s=doc[y+offset];g.Put(0,y,s,s.StartsWith(" ")?Ink.White:Ink.Gold);
            }
        }
        void Tree(Grid g,int t) {
            string[] lines=host.Trees[Hash(t/5,1)%host.Trees.Length];
            int offset=lines.Length>g.H?Math.Min(lines.Length-g.H,(t%5)*4):0;
            for(int y=0;y<g.H && offset+y<lines.Length;y++)
                g.Put(0,y,lines[offset+y],lines[offset+y].Contains(".")?Ink.White:Ink.Sky);
        }
        void Logs(Grid g,int t){
            string[] lines=host.Latest.Logs;
            if(lines.Length==0){g.Put(0,0,"technobabble: waiting for readable Linux logs",Ink.Yellow);return;}
            for(int y=0;y<g.H;y++){
                string s=lines[(y+t*3)%lines.Length];
                g.Put(0,y,s,Ink.White);g.Put(0,y,s.Substring(0,Math.Min(15,s.Length)),Ink.Green);
                if(s.Length>17)g.Put(17,y,s.Substring(17,Math.Min(11,s.Length-17)),s.Contains("Error")?Ink.Coral:s.Contains("Warning")?Ink.Gold:Ink.Aqua);
            }
        }
        FileInfo FileAt(int t){return host.Files.Length==0?new FileInfo(Assembly.GetExecutingAssembly().Location):host.Files[Hash(t,8)%host.Files.Length];}
        void Stats(Grid g,int t) {
            int row=g.H;
            for(int i=t;row>0;i--){
                FileInfo f=FileAt(i);string[] lines;
                try{
                    lines=new[]{ "  File: "+f.FullName,"  Size: "+f.Length+"   Blocks: "+((f.Length+511)/512)+"   IO Block: 4096",
                        "  Mode: "+File.GetUnixFileMode(f.FullName),"Modify: "+f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff"),""};
                }catch(IOException){lines=new[]{"stat: "+f.Name+" unavailable"};}
                row-=lines.Length;for(int k=0;k<lines.Length;k++)g.Put(0,row+k,lines[k],k==0?Ink.Gold:Ink.Yellow);
            }
        }
        byte[] bytes=new byte[0];int hexTick=-1;
        readonly List<int> hexRows=new List<int>();
        void Hex(Grid g,int t) {
            if(hexTick!=t){
                try{using(var s=FileAt(t).Open(FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                    bytes=new byte[Math.Min(4096,s.Length)];int n=0;while(n<bytes.Length){int read=s.Read(bytes,n,bytes.Length-n);if(read==0)break;n+=read;}if(n<bytes.Length)Array.Resize(ref bytes,n);
                }}catch(IOException){bytes=new byte[0];}catch(UnauthorizedAccessException){bytes=new byte[0];}
                hexRows.Clear();bool suppressed=false;
                for(int p=0;p<bytes.Length;p+=16){
                    bool repeat=p>=16 && p+16<=bytes.Length;
                    for(int j=0;repeat && j<16;j++)repeat=bytes[p+j]==bytes[p+j-16];
                    if(!repeat)hexRows.Add(p);else if(!suppressed)hexRows.Add(-1);
                    suppressed=repeat;
                }
                hexTick=t;
            }
            if(bytes.Length==0){g.Put(0,0,"hexdump: waiting for input",Ink.Gray);return;}
            int start=Math.Max(0,hexRows.Count-g.H);
            for(int y=0;y<g.H;y++){
                if(start+y>=hexRows.Count)break;
                int p=hexRows[start+y];if(p<0){g.Put(0,y,"*",Ink.Green);continue;}
                g.Put(0,y,p.ToString("x8"),Ink.Lime);
                var hex=new StringBuilder();var ascii=new StringBuilder();
                for(int i=p;i<Math.Min(p+16,bytes.Length);i++){
                    hex.Append(bytes[i].ToString("x2")+" ");if(i==p+7)hex.Append(' ');
                    ascii.Append(bytes[i]>=32&&bytes[i]<127?(char)bytes[i]:'.');
                }
                g.Put(10,y,hex.ToString(),Ink.Green);g.Put(60,y,"|"+ascii+"|",Ink.Lime);
            }
        }
    }
}
