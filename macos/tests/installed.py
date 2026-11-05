"""Exercise the installed macOS payload, real counters, and interactive terminal."""
import ctypes
from contextlib import contextmanager
import os
from pathlib import Path
import runpy
import subprocess
import tempfile
import time

source = Path(__file__).resolve().parents[2]
Session = runpy.run_path(str(source/"linux/tests/terminal.py"))["Session"]
app = "/usr/local/bin/technobabble"
os.environ["DOTNET_ROOT"] = "/nonexistent-technobabble-test"
for name in (app, app+"-terminal"):
    assert subprocess.check_output([name,"--version"],text=True).strip() == "Technobabble 1.13.1 (macOS)"

sensors = ctypes.CDLL("/usr/local/lib/technobabble/libtechnobabble-sensors.dylib")
total, free = ctypes.c_uint64(), ctypes.c_uint64()
assert sensors.tb_memory(ctypes.byref(total),ctypes.byref(free)) == 1
assert total.value == int(subprocess.check_output(["sysctl","-n","hw.memsize"])) and 0 < free.value <= total.value
ticks = (ctypes.c_uint32 * 1024)()
cores = sensors.tb_cpu(ticks,256)
assert cores == int(subprocess.check_output(["sysctl","-n","hw.logicalcpu"]))
before = sum(ticks)
time.sleep(.3)
assert sensors.tb_cpu(ticks,256) == cores and sum(ticks) > before
print(f"Native Mach counters: {cores} CPUs, {total.value} bytes of memory",flush=True)

with tempfile.TemporaryDirectory() as tmp:
    layouts = Path(tmp)/"layouts"
    @contextmanager
    def launch(*args):
        with Session(app,tmp,list(args),layout_dir=layouts) as s:
            # First launch loads the bundled runtime and assets. Wait for a real
            # status line instead of assuming every host paints in 700 ms.
            deadline=time.monotonic()+15
            while time.monotonic()<deadline:
                footer=s.screen.display[-1]
                if "TECHNOBABBLE / STUDIO" in footer or "technobabble*" in footer:
                    break
                s.take(.2)
            else:
                subprocess.run(["ps","-o","pid,state,wchan,comm","-p",str(s.pid)])
                raise AssertionError(("No initial frame",args,[r.strip() for r in s.screen.display if r.strip()]))
            yield s
    with launch("--layout","Relay Watch") as s:
        assert all(title in s.body for title in ("SENSOR SWEEP","SIGNAL DECODER","ENGINEERING")), s.screen.display
        assert s.body.count("#001") == 3
        s.key(b" "); paused=s.body
        s.resize(50,15); s.take(.5); s.resize(120,40); s.take(.5)
        assert s.body == paused
        s.key(b"s"); assert "technobabble*" in s.screen.display[-1]
        s.key(b"s"); assert s.body == paused
        s.key(b"w"); s.key(b"Mac Preview\r")
        assert (layouts/"Mac Preview.layout").read_text().splitlines()[2] == "V *radar H *signal *engineering"
    with launch("--layout","Mac Preview") as s:
        s.key(b"e"); assert "PANE EDITOR" in s.body; s.key(b"\x1b")
    with launch("--layout","Process Watch","--refresh","1") as s:
        s.key(b"f"); s.key(b"technobabble\r",2.0)
        assert "Filter: technobabble" in s.body and str(s.pid) in s.body, s.screen.display
    with launch("--layout","Network Watch","--refresh","1") as s:
        s.take(2.5)
        assert "speedometer" in s.body and "RX " in s.body and "TX " in s.body,s.screen.display
    with launch("--layout","Disk Watch","--refresh","1") as s:
        s.take(2.2)
        assert "macOS storage devices" in s.body and ("/s" in s.body or "unavailable" in s.body),s.screen.display
    with launch("--layout","GPU Watch") as s:
        s.take(2.4)
        assert "GPU ACTIVITY / macOS" in s.body and "unavailable" in s.body,s.screen.display
    with launch() as s:
        assert "technobabble*" in s.screen.display[-1]
print("Installed macOS Classic/Studio, resize, save/load, editor, PID and device panes passed; tty restored.")
