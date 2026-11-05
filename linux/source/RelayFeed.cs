using System;

namespace Technobabble {
    internal enum RelayPhase { Detect, Tune, Decode, Repair, Verify, Clear }

    internal sealed class RelayCase {
        public readonly string Contact, Request, Command, Result, Meter, Unit;
        public readonly double Fault, Nominal;
        public readonly int Bearing, Channel;
        public readonly bool SplitEcho;
        readonly CodeTyping keystrokes;
        public RelayCase(string contact, string request, string command, string result,
                         string meter, string unit, double fault, double nominal, int bearing, int channel, bool splitEcho = false) {
            Contact = contact; Request = request; Command = command; Result = result;
            Meter = meter; Unit = unit; Fault = fault; Nominal = nominal; Bearing = bearing; Channel = channel;
            SplitEcho = splitEcho;
            keystrokes = new CodeTyping(new[] {command},channel);
        }
        public string TypedAction(double seconds) {
            int chars = keystrokes.FirstLinePrefix(seconds,CodeStyle.Slow);
            return Command.Substring(0,chars) + (chars < Command.Length ? "_" : "");
        }
    }

    // Everything displayed in a pane is a projection of this same scene-time sample.
    // Rendering, resizing, and adding panes cannot advance the incident.
    internal sealed class RelayFrame {
        public const int CycleSeconds = 36;
        public readonly long Number;
        public readonly double Age;
        public readonly RelayCase Incident;
        public readonly RelayPhase Phase;

        public RelayFrame(long tick, RelayCase incident) {
            long cycle = tick / (10 * CycleSeconds);
            Number = cycle + 1;
            Age = (tick % (10 * CycleSeconds)) / 10.0;
            Incident = incident;
            Phase = Age < 4 ? RelayPhase.Detect : Age < 9 ? RelayPhase.Tune :
                    Age < 15 ? RelayPhase.Decode : Age < 24 ? RelayPhase.Repair :
                    Age < 30 ? RelayPhase.Verify : RelayPhase.Clear;
        }
        public string Tag { get { return Incident.Contact + "  #" + Number.ToString("000"); } }
        public string Request { get { return Incident.Contact + " / " + Incident.Request; } }
        public double Bearing { get { return (Incident.Bearing + Age * 1.2) % 360; } }
        public double Range { get { return 81 - Age * .65; } }
        public bool SplitEcho { get { return Incident.SplitEcho && Phase == RelayPhase.Tune; } }
        public double RepairFraction { get { return Math.Max(0, Math.Min(1, (Age - 17) / 7)); } }
        public string Received {
            get {
                if (Phase < RelayPhase.Decode) return "";
                int chars = Math.Min(Request.Length, (int)((Age - 9) * Request.Length / 4));
                return Request.Substring(0, chars) + (chars < Request.Length ? "_" : "");
            }
        }
        public string Action {
            get {
                if (Phase < RelayPhase.Repair) return "";
                return Incident.TypedAction(Age - 15);
            }
        }
    }

    internal sealed class RelayFeed {
        readonly RelayDeck deck = new RelayDeck(Guid.NewGuid().GetHashCode());
        double origin;
        double? suspended;
        long lastTick = -1;
        RelayFrame lastFrame;

        public RelayFeed(double now) { origin = now; }
        public void Suspend(double now) { if (!suspended.HasValue) suspended = now; }
        public void Resume(double now) {
            if (suspended.HasValue) {
                origin += now - suspended.Value;
                suspended = null;
            }
        }
        public RelayFrame At(double now) {
            long tick = (long)Math.Floor(Math.Max(0, (suspended ?? now) - origin) * 10 + 0.000001);
            if (tick != lastTick) {
                lastTick = tick;
                var incident = deck.ForCycle(tick / (10 * RelayFrame.CycleSeconds));
                lastFrame = new RelayFrame(tick, incident);
            }
            return lastFrame;
        }
    }

    internal sealed partial class Scene {
        RelayFeed relay;
        public void ResetRelay() { relay = null; }
        public Grid WidgetFrame(Widget widget, int width, int height) {
            if (IsStudio && widget.IsRelay && relay == null) relay = new RelayFeed(Time);
            return widget.Frame(width, height, Time, CodePlayback, relay);
        }
    }
}
