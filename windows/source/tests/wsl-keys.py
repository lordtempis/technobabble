"""Run with python3 under Ubuntu; no third-party packages required."""
import importlib.util
from pathlib import Path
spec=importlib.util.spec_from_file_location('launcher',Path(__file__).resolve().parents[2]/'technobabble-terminal-wsl.py')
launcher=importlib.util.module_from_spec(spec);spec.loader.exec_module(launcher)
k=launcher.Keys()
assert k.feed(b'\x1b')==[]
assert k.feed(b'[')==[]
assert k.feed(b'A')==[(38,0,0)]
assert k.feed(b'\x1b[Z')==[(9,2,9)]
assert k.feed(b'\x1b[15~\x1b[17~')==[(116,0,0),(117,0,0)]
assert k.feed(b'\x1b[1;5D')==[(37,4,0)]
assert k.feed(b'\x0c\x03\x15')==[(76,4,12),(67,4,3),(85,4,21)]
assert k.feed(b'\x7f\r\t')==[(8,0,8),(13,0,13),(9,0,9)]
assert k.feed(b'\xc3')==[]
assert k.feed(b'\xa9')==[(0,0,233)]
assert k.feed(b'\x1b')==[]
assert k.feed(b'',True)==[(27,0,27)]
assert k.feed(b'\x1bx')==[(88,1,120)]
print('Ubuntu key decoding, split escape sequences, Unicode and shortcuts passed.')
