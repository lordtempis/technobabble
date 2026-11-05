using System;

namespace Technobabble {
    internal sealed class RelayDeck {
        static readonly RelayCase[] Catalog = {
            new RelayCase("RELAY 07", "AUX POWER REQUEST", "route AUX-B to relay bus", "AUX POWER ONLINE",
                          "Bus voltage", "V", 7.2, 28, 42, 7),
            new RelayCase("KESTREL 12", "COOLANT PRESSURE LOW", "open secondary bypass", "PRESSURE NOMINAL",
                          "Coolant", "kPa", 19, 85, 246, 12, splitEcho: true),
            new RelayCase("BEACON 31", "CLOCK SYNC REQUEST", "sync beacon to master", "CLOCK SYNCHRONIZED",
                          "Clock error", "ms", 84, 0.2, 305, 31),
            new RelayCase("ORBITER 04", "SOLAR ARRAY JAMMED", "release array latch", "ARRAY DEPLOYED",
                          "Array output", "kW", 1.1, 14.8, 118, 4),
            new RelayCase("HARBOR 22", "DOCK CLAMP MISALIGNED", "align capture collar", "DOCK ALIGNED",
                          "Clamp offset", "cm", 8.4, 0.3, 193, 22),
            new RelayCase("MIRAGE 18", "MULTIPATH ECHO", "gate delayed radar return", "SINGLE TRACK CONFIRMED",
                          "Echo delay", "us", 64, 2, 267, 18, splitEcho: true),
            new RelayCase("RIME 09", "PROPELLANT VALVE ICED", "warm inlet manifold", "VALVE RESPONDING",
                          "Valve travel", "%", 4, 100, 16, 9),
            new RelayCase("ATLAS 55", "FLYWHEEL VIBRATION", "trim wheel balance", "ROTOR BALANCED",
                          "Vibration", "mm/s", 18.3, 1.1, 87, 55),
            new RelayCase("LANTERN 03", "OPTICAL LINK FADED", "align optical bench", "DOWNLINK RESTORED",
                          "Optical power", "mW", 0.7, 12.8, 218, 3),
            new RelayCase("KEPLER 46", "ATTITUDE DRIFT", "pulse port thruster", "ATTITUDE STABLE",
                          "Yaw drift", "deg/s", 3.8, 0.1, 339, 46),
            new RelayCase("VAULT 29", "ARCHIVE PARITY FAULT", "rebuild parity stripe", "ARCHIVE VERIFIED",
                          "Parity health", "%", 41, 100, 154, 29),
            new RelayCase("SUNDIAL 63", "RADIATOR LOOP HOT", "unfold reserve radiator", "LOOP TEMPERATURE SAFE",
                          "Loop temp", "K", 367, 292, 286, 63)
        };

        readonly int seed;
        long currentRound = -1;
        RelayCase[] order;
        public static int Count { get { return Catalog.Length; } }

        public RelayDeck(int seed) { this.seed = seed; }

        RelayCase[] Shuffle(long round) {
            var random = new Random(unchecked(seed + round.GetHashCode() * 486187739));
            var cards = (RelayCase[])Catalog.Clone();
            for (int i = cards.Length - 1; i > 0; i--) {
                int other = random.Next(i + 1);
                var card = cards[i];
                cards[i] = cards[other];
                cards[other] = card;
            }
            return cards;
        }

        public RelayCase ForCycle(long cycle) {
            long round = cycle / Count;
            if (round != currentRound) {
                order = Shuffle(round);
                // Only repair the head. The previous round's final card can
                // therefore be reconstructed directly, even after a long skip.
                if (round > 0 && order[0] == Shuffle(round - 1)[Count - 1]) {
                    var first = order[0];
                    order[0] = order[1];
                    order[1] = first;
                }
                currentRound = round;
            }
            return order[(int)(cycle % Count)];
        }
    }
}
