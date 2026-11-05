using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Technobabble;

internal static partial class Checks {
    static string RelayView(Scene scene, string name, int width = 56, int height = 22) {
        var widget = scene.Root.Leaves().Single(p => p.Widget.Name == name).Widget;
        return Contents(scene.WidgetFrame(widget,width,height));
    }
    static RelayCase[] CheckRelayDeck() {
        var reference = new RelayDeck(2026);
        var catalog = Enumerable.Range(0, RelayDeck.Count).Select(i => reference.ForCycle(i)).ToArray();
        Check(catalog.Length == 12 && catalog.Select(c => c.Contact).Distinct().Count() == 12 &&
            catalog.Select(c => c.Request).Distinct().Count() == 12, "Relay catalog missing distinct incidents");
        var openings = new HashSet<string>();
        var permutations = new HashSet<string>();
        for (int seed = 0; seed < 64; seed++) {
            var deck = new RelayDeck(seed);
            openings.Add(deck.ForCycle(0).Contact);
            RelayCase previous = null;
            for (int round = 0; round < 12; round++) {
                var cards = Enumerable.Range(0, catalog.Length).Select(i => deck.ForCycle(round * catalog.Length + i)).ToArray();
                Check(new HashSet<RelayCase>(cards).SetEquals(catalog), "Missing or repeated incident in shuffled round");
                Check(cards[0] != previous, "Repeated incident on round boundary");
                permutations.Add(String.Join(",",cards.Select(c => c.Contact)));
                previous = cards.Last();
            }
            const long skipped = 10000000000L;
            var target = deck.ForCycle(skipped);
            deck.ForCycle(3);
            Check(target == deck.ForCycle(skipped) && target == new RelayDeck(seed).ForCycle(skipped),
                "Skipping or revisiting an incident changed its identity");
        }
        Check(openings.Count > 1 && permutations.Count > 64, "Shuffles did not vary by session and round");
        return catalog;
    }
    static RelayCase ContactIn(string frame, long number, RelayCase[] catalog) {
        return catalog.Single(c => frame.Contains(c.Contact + "  #" + number.ToString("000")));
    }
    static void RelayChecks(Machine host, Pictures images) {
        var catalog = CheckRelayDeck();
        var store = new LayoutStore(Path.Combine(Path.GetTempPath(), "relay-" + Guid.NewGuid().ToString("N")));
        var scene = new Scene(20,10,new Random(112),host,images);
        var board = new Studio(scene,store);
        scene.Resize(160,52);
        try {
            board.Load(LayoutStore.RelayExample);
            Check(store.Names().Contains("Relay Watch") && LayoutStore.IsExample("relay watch"),"Missing relay preset");
            Check(scene.Root.Leaves().Select(p=>p.Widget.Name).SequenceEqual(new[]{"radar","signal","engineering"}) &&
                scene.Root.Leaves().All(p=>p.Pinned),"Relay arrangement incomplete");
            string layout = scene.LayoutText();
            string radar = RelayView(scene,"radar");
            RelayCase first = ContactIn(radar,1,catalog);
            Check(radar.Contains("CONTACT DETECTED"),"First radar detection missing");
            scene.Advance(4.5);
            Check(RelayView(scene,"signal").Contains("TUNING CARRIER") && RelayView(scene,"engineering").Contains("LISTENING"),"Acquisition stages disagree");
            scene.Advance(5.5);
            string partial = RelayView(scene,"signal");
            Check(partial.Contains("DECODING FRAME") && partial.Contains("_") && !partial.Contains(first.Request),"Frame did not decode progressively");
            string held = RelayView(scene,"radar");
            board.Leave();scene.Advance(88);scene.Shuffle();
            Check(!scene.IsStudio && scene.Root.Leaves().All(p=>!p.Widget.IsRelay && !p.Pinned),"Simulation leaked into Classic");
            board.Enter();
            Check(RelayView(scene,"radar")==held && RelayView(scene,"signal")==partial,"Classic elapsed time advanced the incident");
            scene.Advance(3.5);
            Check(RelayView(scene,"signal").Contains(first.Contact+" / "+first.Request) &&
                  !RelayView(scene,"engineering").Contains(first.Command),"Repair began before dispatch");
            scene.Advance(2.5);
            string action = RelayView(scene,"engineering");
            Check(action.Contains("REPAIR IN PROGRESS") && action.Contains("> "+first.Command.Substring(0,3)) && !action.Contains(first.Command),"Command did not type gradually");
            scene.Advance(8);
            Check(RelayView(scene,"signal").Contains("TX: ACK / "+first.Result) &&
                RelayView(scene,"engineering").Contains("Check: "+first.Result),"Repair and acknowledgment disagree");
            scene.Advance(6);
            Check(RelayView(scene,"engineering").Contains("CASE CLOSED") && RelayView(scene,"signal").Contains("ACK CONFIRMED"),"Incident did not close");
            scene.Advance(6);
            var seen = new HashSet<RelayCase> {first};
            for (int number = 2; number <= catalog.Length; number++) {
                var next = ContactIn(RelayView(scene,"radar"),number,catalog);
                Check(seen.Add(next),"Shared dashboard repeated a contact before completing its round");
                foreach (var pane in scene.Root.Leaves())
                    Check(ContactIn(RelayView(scene,pane.Widget.Name),number,catalog) == next,"Panes drew different contacts");
                scene.Advance(4);
                Check(RelayView(scene,"radar").Contains("TWO RETURNS") == next.SplitEcho,"Incorrect split track");
                scene.Advance(10);
                Check(RelayView(scene,"signal").Contains(next.Contact+" / "+next.Request),"Decoded message belongs to another contact");
                scene.Advance(10);
                Check(RelayView(scene,"engineering").Contains("Check: "+next.Result) &&
                      RelayView(scene,"signal").Contains("TX: ACK / "+next.Result),"Incorrect case resolution");
                scene.Advance(12);
            }
            Check(seen.SetEquals(catalog) && scene.LayoutText()==layout,"Rotation or pin stability failed");
            board.Load(LayoutStore.BlankExample);
            Edit(board,PaneEditAction.Replace,"signal");
            RelayCase solo = ContactIn(RelayView(scene,"signal"),1,catalog);
            scene.Advance(19);
            Edit(board,PaneEditAction.SplitColumns,"engineering");
            Check(RelayView(scene,"engineering").Contains("REPAIR IN PROGRESS") &&
                  ContactIn(RelayView(scene,"engineering"),1,catalog)==solo,"New pane restarted the shared incident");
            Edit(board,PaneEditAction.SplitRows,"radar");
            string same = RelayView(scene,"radar");
            foreach (var p in scene.Root.Leaves()) {
                RelayView(scene,p.Widget.Name,1,1);
                RelayView(scene,p.Widget.Name,11,6);
            }
            Check(RelayView(scene,"radar")==same && same.Contains("HOLDING ESCORT"),"Rendering or resizing advanced the feed");
            board.OpenPaneEditor();Check(board.RemovePane() && board.UndoPaneEdit(),"Editor remove/undo failed");
            Check(RelayView(scene,"radar")==same,"Undo restarted the incident");
            foreach(string name in new[]{"radar","signal","engineering"}) {
                board.Load(LayoutStore.BlankExample);Edit(board,PaneEditAction.Replace,name);
                var contact = ContactIn(RelayView(scene,name),1,catalog);
                scene.Advance(31);
                Check(ContactIn(RelayView(scene,name),1,catalog)==contact && RelayView(scene,name).Contains("T+31s"),"Standalone incident stalled");
            }
            Console.WriteLine("Relay Watch: shuffled rounds, boundary repeats, all twelve incidents, shared messages and repairs, solo/late panes, pause and Classic isolation passed.");
        } finally { board.Leave(); }
    }
}
