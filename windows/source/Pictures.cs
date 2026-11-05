using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Technobabble {
    internal sealed class Pixels {
        public readonly int W,H;
        public readonly int[] Rgb;
        public Pixels(Image source,int width,int height) {
            W=width;H=height;Rgb=new int[W*H];
            using(var b=new Bitmap(W,H,PixelFormat.Format32bppRgb)){
                using(var g=Graphics.FromImage(b)){
                    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    g.DrawImage(source,0,0,W,H);
                }
                var bits=b.LockBits(new Rectangle(0,0,W,H),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
                try{Marshal.Copy(bits.Scan0,Rgb,0,Rgb.Length);}finally{b.UnlockBits(bits);}
            }
        }
        public void Paint(Grid g,Rectangle src,bool fit,bool video) {
            int w=g.W,h=g.H;
            if(fit){
                double scale=Math.Min((double)w/src.Width,2.0*h/src.Height);
                w=Math.Max(1,(int)(src.Width*scale));h=Math.Max(1,(int)(src.Height*scale/2));
            }
            int ox=(g.W-w)/2,oy=(g.H-h)/2;
            const string ramp=" .,:;irsXA253hMHGS#9B&@";
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                int rgb=Rgb[(src.Y+y*src.Height/h)*W+src.X+x*src.Width/w];
                int r=(rgb>>16)&255,b=rgb&255,green=(rgb>>8)&255;
                int lum=(r*3+green*6+b)/10;
                int max=Math.Max(r,Math.Max(green,b));
                if(max<12)continue;
                // Terminal foreground covers only part of a cell. Lift it to retain image detail.
                int gain=video?60:38;
                int ri=Math.Min(5,(r+gain)/51),gi=Math.Min(5,(green+gain)/51),bi=Math.Min(5,(b+gain)/51);
                byte fg=(byte)(16+36*ri+6*gi+bi);
                int level=Math.Min(ramp.Length-1,(lum+(video?max:0))*ramp.Length/256);
                g.Set(ox+x,oy+y,ramp[level],fg);
            }
        }
    }
    internal sealed class Pictures {
        public readonly Pixels Map,Wave;
        public readonly List<Pixels> Photos=new List<Pixels>();
        public Pictures(){
            Map=Resource("map.jpg",320,160); Wave=Resource("wave.jpg",1536,702);
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Web","Wallpaper");
            foreach(string name in new[]{@"Windows\img0.jpg",@"ThemeA\img20.jpg",@"ThemeB\img25.jpg",@"ThemeC\img30.jpg",@"Spotlight\img50.jpg"}){
                string path=Path.Combine(dir,name); if(!File.Exists(path))continue;
                try{using(var pic=Image.FromFile(path))Photos.Add(new Pixels(pic,192,Math.Max(1,192*pic.Height/pic.Width)));}
                catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}
            }
            if(Photos.Count==0)Photos.Add(Map);
        }
        static Pixels Resource(string name,int w,int h){
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(var image=Image.FromStream(stream))return new Pixels(image,w,h);
        }
    }
}
