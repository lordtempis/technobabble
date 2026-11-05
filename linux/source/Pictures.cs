using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace Technobabble {
    internal sealed class Pixels {
        public readonly int W,H;
        readonly int[] Rgb;
        public Pixels(string resource) {
            using var bytes = typeof(Pixels).Assembly.GetManifestResourceStream(resource);
            using var reader = new BinaryReader(new GZipStream(bytes, CompressionMode.Decompress));
            W=reader.ReadInt32(); H=reader.ReadInt32(); Rgb=new int[checked(W*H)];
            for(int i=0;i<Rgb.Length;i++)
                Rgb[i]=(reader.ReadByte()<<16)|(reader.ReadByte()<<8)|reader.ReadByte();
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
        public readonly Pixels Map=new Pixels("map.rgb.gz"), Wave=new Pixels("wave.rgb.gz");
        public readonly List<Pixels> Photos;
        public Pictures(){Photos=new List<Pixels>{Map};}
    }
}
