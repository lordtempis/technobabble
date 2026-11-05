using System;
using System.Drawing;

namespace Technobabble {
    // Colors 0..15 form the terminal palette. 16..231 are a 6x6x6 color cube.
    internal static class Ink {
        public const byte Black=0, Red=1, Green=2, Yellow=3, Blue=4, Purple=5, Cyan=6, White=7,
            Gray=8, Coral=9, Lime=10, Gold=11, Sky=12, Pink=13, Aqua=14, Bright=15;
        public static readonly Color[] Colors = Palette();
        static Color[] Palette() {
            string[] hex = { "080a0b","c54c46","50ad66","cba64f","5075b8","a272b6","419b9d","c3c7c5",
                "657171","f26b60","69d979","f1d17c","83a4e0","ce92d1","78d6d2","eff6ed" };
            Color[] palette = new Color[232];
            for (int i=0;i<16;i++) palette[i]=Color.FromArgb(unchecked((int)0xff000000) | Convert.ToInt32(hex[i],16));
            for (int i=0;i<216;i++) palette[16+i]=Color.FromArgb(i/36*51,(i/6)%6*51,i%6*51);
            return palette;
        }
    }
    internal sealed class Grid {
        public readonly int W,H;
        public readonly char[][] Ch;
        public readonly byte[][] Fg,Bg;
        public Grid(int w,int h) {
            W=Math.Max(1,w); H=Math.Max(1,h); Ch=new char[H][]; Fg=new byte[H][]; Bg=new byte[H][];
            for(int y=0;y<H;y++) {
                Ch[y]=new string(' ',W).ToCharArray(); Fg[y]=new byte[W]; Bg[y]=new byte[W];
            }
        }
        public void Put(int x,int y,string s,byte fg=Ink.White,byte bg=Ink.Black) {
            if(y<0||y>=H||x>=W||s==null)return;
            for(int i=Math.Max(0,-x);i<s.Length&&x+i<W;i++) Set(x+i,y,s[i],fg,bg);
        }
        public void Set(int x,int y,char c,byte fg,byte bg=Ink.Black) {
            if(x<0||x>=W||y<0||y>=H)return;
            Ch[y][x]=c; Fg[y][x]=fg; Bg[y][x]=bg;
        }
        public void Band(int y,string s,byte fg,byte bg) { Put(0,y,s.PadRight(W),fg,bg); }
        // Reflow terminal output at the pane edge. Full-screen widgets draw at their exact viewport size.
        public Grid Wrap(int width,bool tail){
            if(width>=W)return this;
            var rows=new System.Collections.Generic.List<Point>();
            for(int y=0;y<H;y++){
                int length=W;
                while(length>0 && Ch[y][length-1]==' ')length--;
                for(int x=0;x<Math.Max(1,length);x+=width)rows.Add(new Point(x,y));
            }
            Grid result=new Grid(width,H);
            int first=tail?Math.Max(0,rows.Count-H):0;
            for(int y=0;y<H && first+y<rows.Count;y++){
                Point source=rows[first+y];int n=Math.Min(width,W-source.X);
                Array.Copy(Ch[source.Y],source.X,result.Ch[y],0,n);
                Array.Copy(Fg[source.Y],source.X,result.Fg[y],0,n);
                Array.Copy(Bg[source.Y],source.X,result.Bg[y],0,n);
            }
            return result;
        }
    }

}
