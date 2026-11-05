using System;

namespace Technobabble {
    internal static class RelayPanes {
        static readonly string[] RadarState = {"CONTACT DETECTED", "TRACK ACQUIRED", "TRANSPONDER LOCK", "HOLDING ESCORT", "BEACON STABLE", "FRIENDLY / HANDOFF"};
        static readonly string[] SignalState = {"BAND SEARCH", "TUNING CARRIER", "DECODING FRAME", "REQUEST DELIVERED", "SENDING ACK", "ACK CONFIRMED"};
        static readonly string[] EngineeringState = {"SERVICE BUS READY", "LISTENING", "FRAME INBOUND", "REPAIR IN PROGRESS", "VERIFYING RESULT", "CASE CLOSED"};

        public static Grid Draw(string name, int width, int height, RelayFrame s) {
            var g = new Grid(width, height);
            switch (name) {
                case "radar": DrawRadar(g, s); break;
                case "signal": DrawSignal(g, s); break;
                case "engineering": DrawEngineering(g, s); break;
            }
            return g;
        }

        static void Header(Grid g, RelayFrame s, string title, string state, byte color) {
            if (g.H < 3) {
                g.Put(0,0,s.Tag,color);
                g.Put(0,1,state,Ink.Bright);
                return;
            }
            g.Band(0," " + title + " / SIM",Ink.Black,color);
            g.Put(0,1,s.Tag,color);
            string clock = "T+" + ((int)s.Age).ToString("00") + "s";
            if (g.W > s.Tag.Length + clock.Length + 1) g.Put(g.W-clock.Length,1,clock,Ink.Gray);
            g.Put(0,2,state,Ink.Bright);
        }

        static int Text(Grid g, int row, string text, byte color) {
            int start = 0;
            while (start < text.Length && row < g.H) {
                int count = Math.Min(g.W, text.Length - start);
                if (start + count < text.Length) {
                    int space = text.LastIndexOf(' ', start + count - 1, count);
                    if (space > start) count = space - start;
                }
                g.Put(0, row++, text.Substring(start, count), color);
                start += count;
                while (start < text.Length && text[start] == ' ') start++;
            }
            return row;
        }

        static void DrawRadar(Grid g, RelayFrame s) {
            Header(g,s,"SENSOR SWEEP",s.SplitEcho ? "TRACK SPLIT / TWO RETURNS" : RadarState[(int)s.Phase],Ink.Lime);
            if (g.H < 4) return;
            g.Put(0,3,String.Format("BRG {0:000}   RNG {1:0} km",s.Bearing,s.Range),Ink.Green);
            int top = 4, bottom = g.H - (g.H >= 12 ? 2 : 1);
            if (bottom - top < 2 || g.W < 6) return;
            double cx = (g.W-1)*.5, cy = (bottom+top)*.5;
            double rx = cx, ry = (bottom-top)*.5;
            double angle = s.Age * Math.PI / 3;
            double east = Math.Sin(angle), north = Math.Cos(angle);
            char sweep = Math.Abs(east) < .38 ? '|' : Math.Abs(north) < .38 ? '-' : east*north > 0 ? '/' : '\\';
            for (int y = top; y <= bottom; y++) {
                double ny = (cy-y)/ry;
                for (int x = 0; x < g.W; x++) {
                    double nx = (x-cx)/rx, radius = Math.Sqrt(nx*nx + ny*ny);
                    if (radius > 1.03) continue;
                    double ring = radius*4;
                    if (Math.Abs(ring-Math.Round(ring)) < Math.Min(.22,2/ry))
                        g.Set(x,y,'.',Ink.Gray);
                    double ahead = nx*east + ny*north, side = nx*north - ny*east;
                    if (ahead > 0 && side < 0 && side > -ahead*.24) g.Set(x,y,':',Ink.Green);
                    if (ahead > 0 && Math.Abs(side) < .48/Math.Max(1,Math.Min(rx,ry)))
                        g.Set(x,y,sweep,Ink.Lime);
                }
            }
            double bearing = s.Bearing*Math.PI/180;
            int px = (int)Math.Round(cx + Math.Sin(bearing)*rx*s.Range/100);
            int py = (int)Math.Round(cy - Math.Cos(bearing)*ry*s.Range/100);
            g.Set((int)Math.Round(cx),(int)Math.Round(cy),'+',Ink.Aqua);
            if (s.SplitEcho) g.Set(px-3,py+1,'?',Ink.Gold);
            g.Set(px,py,'*',s.Phase>=RelayPhase.Verify ? Ink.Aqua : Ink.Gold);
            if (g.H >= 12) g.Put(0,g.H-1,"* contact   + origin   25 km/ring",Ink.Gray);
        }

        static double Carrier(RelayCase incident, double age) {
            double center = .28 + (incident.Channel%11)*.04;
            double drift = Math.Max(0,1-age/9) * Math.Sin(age*2.3) * .15;
            return center + drift;
        }

        static void DrawSignal(Grid g, RelayFrame s) {
            Header(g,s,"SIGNAL DECODER",SignalState[(int)s.Phase],Ink.Aqua);
            if (g.H < 4) return;
            g.Put(0,3,String.Format("CH {0:00}   {1:0.00} MHz",s.Incident.Channel,140+s.Incident.Channel*.825),Ink.Sky);
            string message;
            if (s.Phase == RelayPhase.Detect) message = "RX: scanning for carrier";
            else if (s.Phase == RelayPhase.Tune)
                message = String.Format("PLL: drift {0:+0.0;-0.0;0} kHz",(Carrier(s.Incident,s.Age)-Carrier(s.Incident,9))*40);
            else if (s.Phase < RelayPhase.Verify) message = "RX: " + s.Received;
            else if (s.Phase == RelayPhase.Verify) message = "TX: ACK / " + s.Incident.Result;
            else message = "RX: " + s.Incident.Contact + " / ACK CONFIRMED";
            int top = Text(g,4,message,Ink.Gold);
            if (g.H-top >= 5) g.Put(0,top++,"WATERFALL / newest first",Ink.Gray);
            const string glyphs = " .:-=+*#@";
            for (int y = top; y < g.H; y++) {
                double time = s.Age - (y-top)*.28;
                if (time < 0) break;
                double center = Carrier(s.Incident,time);
                int sweep = (int)(time*7);
                for (int x = 0; x < g.W; x++) {
                    uint hash = unchecked((uint)(x*73856093 ^ sweep*19349663 ^ s.Incident.Channel));
                    hash ^= hash >> 13; hash *= 1274126177u;
                    int noise = (int)(hash >> 29) / 2;
                    double offset = Math.Abs(x/(double)Math.Max(1,g.W-1)-center);
                    int strength = (int)(Math.Max(0,1-offset/.075) * Math.Min(5,time*.85));
                    int level = Math.Min(8,noise+strength);
                    g.Set(x,y,glyphs[level],level>=6 ? Ink.Gold : level>=4 ? Ink.Aqua : level>=2 ? Ink.Blue : Ink.Gray);
                }
            }
        }

        static void DrawEngineering(Grid g, RelayFrame s) {
            Header(g,s,"ENGINEERING",EngineeringState[(int)s.Phase],Ink.Gold);
            if (g.H < 4) return;
            if (s.Phase < RelayPhase.Repair) {
                int row = Text(g,3,"> listen / service channel " + s.Incident.Channel,Ink.Aqua);
                string[] activity = {"Standby. No work order.", "Radar track linked. Waiting for carrier.", "Decoder receiving service request."};
                row = Text(g,row,activity[(int)s.Phase],Ink.White);
                if (row+2 < g.H) DrawLink(g,row+1,s);
                return;
            }
            var incident = s.Incident;
            double value = incident.Fault + (incident.Nominal-incident.Fault)*s.RepairFraction;
            int next = Text(g,3,incident.Meter + "  " + value.ToString("0.0") + " " + incident.Unit,Ink.Bright);
            next = Text(g,next,"> " + s.Action,Ink.Aqua);
            string result = s.Phase == RelayPhase.Repair ? "Work order: " + incident.Request :
                            s.Phase == RelayPhase.Verify ? "Check: " + incident.Result : "Confirmed: " + incident.Result;
            next = Text(g,next,result,s.Phase>=RelayPhase.Verify ? Ink.Lime : Ink.Gold);
            if (s.Phase == RelayPhase.Clear) next = Text(g,next,"ACK received / service restored",Ink.Aqua);
            if (g.H-next >= 4) DrawTelemetry(g,next+1,s);
        }

        static void DrawLink(Grid g, int top, RelayFrame s) {
            g.Put(0,top,"SERVICE LINK / heartbeat",Ink.Gray);
            int h = g.H-top-1;
            for (int x = 0; x < g.W; x++) {
                int p = (x - (int)(s.Age*8)) % 24;
                if (p < 0) p += 24;
                double pulse = p==5 ? .75 : p==6 ? .1 : p==7 ? .45 : .26;
                g.Set(x,g.H-1-(int)((h-1)*pulse),p==5?'|':'-',Ink.Green);
            }
        }

        static void DrawTelemetry(Grid g, int top, RelayFrame s) {
            // Graph the same measurement as the live number, starting at dispatch.
            g.Put(0,top,s.Incident.Meter.ToUpperInvariant()+" / last 12s",Ink.Gray);
            int h = g.H-top-1;
            for (int x=0; x<g.W; x++) {
                double t = s.Age-12+12*x/(double)Math.Max(1,g.W-1);
                if (t < 15) continue;
                double repair = Math.Max(0,Math.Min(1,(t-17)/7));
                double value = s.Incident.Fault+(s.Incident.Nominal-s.Incident.Fault)*repair;
                int y = g.H-1-(int)(value/Math.Max(s.Incident.Fault,s.Incident.Nominal)*(h-1));
                g.Set(x,y,'*',repair>=1 ? Ink.Lime : Ink.Gold);
            }
        }
    }
}
