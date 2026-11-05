#!/usr/bin/env python3
"""Ubuntu terminal adapter for the native Windows Technobabble executable (Python 3, stdlib only)."""
import os
from pathlib import Path
import re
import select
import signal
import subprocess
import sys
import termios
import time
import tty

class Keys:
    def __init__(self):
        self.pending = bytearray()
        self.since = 0

    @staticmethod
    def plain(ch, alt=0):
        n = ord(ch)
        if ch in ('\r', '\n'): return 13, alt, 13
        if ch in ('\x7f', '\b'): return 8, alt, 8
        if ch == '\t': return 9, alt, 9
        if ch == '\x1b': return 27, alt, 27
        if 1 <= n <= 26: return n+64, alt|4, n
        key = ord(ch.upper()) if len(ch.upper()) == 1 and ch.isascii() and ch.isalnum() else (32 if ch == ' ' else 0)
        return key, alt|(2 if ch.isupper() else 0), n if n <= 65535 else 0xfffd

    def feed(self, data, expired=False):
        if not self.pending: self.since=time.monotonic()
        self.pending.extend(data)
        result=[]
        while self.pending:
            b=bytes(self.pending)
            if b[0]==27:
                if len(b)==1:
                    if not expired: break
                    result.append((27,0,27));del self.pending[:1];continue
                if b[1] in (ord('['),ord('O')):
                    m=re.match(rb'\x1b(?:\[|O)([0-9;]*)([A-Za-z~])',b)
                    if not m:
                        if len(b)<32 and not expired: break
                        del self.pending[:1];continue
                    args,end=m[1],m[2];del self.pending[:m.end()]
                    numbers=[int(v) for v in args.split(b';') if v]
                    mod=numbers[-1]-1 if len(numbers)>1 else 0
                    mods=(2 if mod&1 else 0)|(1 if mod&2 else 0)|(4 if mod&4 else 0)
                    mapping={b'A':38,b'B':40,b'C':39,b'D':37,b'H':36,b'F':35,b'P':112,b'Q':113,b'R':114,b'S':115,b'Z':9}
                    key=mapping.get(end)
                    if end==b'~':key={1:36,2:45,3:46,4:35,5:33,6:34,11:112,12:113,13:114,14:115,15:116,17:117,18:118,19:119,20:120,21:121,23:122,24:123}.get(numbers[0] if numbers else 0)
                    if end==b'Z':mods|=2
                    if key is not None:result.append((key,mods,9 if key==9 else 0))
                    continue
                del self.pending[:1]
                # Alt+ASCII is enough for the application's shortcuts.
                if self.pending and self.pending[0]<128:result.append(self.plain(chr(self.pending.pop(0)),1))
                continue
            count=1 if b[0]<128 else (2 if b[0]<224 else (3 if b[0]<240 else 4))
            if len(b)<count and not expired:break
            ch=b[:count].decode('utf-8','replace');del self.pending[:count]
            for c in ch:result.append(self.plain(c))
        if not self.pending:self.since=0
        return result

def main():
    exe=Path(__file__).resolve().with_name('Technobabble-Terminal.exe')
    if not sys.stdin.isatty() or not sys.stdout.isatty():
        print('Run this launcher in an interactive Ubuntu terminal.',file=sys.stderr);return 2
    if not exe.is_file():
        print('Keep this launcher next to Technobabble-Terminal.exe.',file=sys.stderr);return 2
    saved=termios.tcgetattr(0)
    proc=None;decoder=Keys();size=None;buffer=bytearray()
    stopping=False
    def stop(signum,frame):
        nonlocal stopping
        stopping=True
    old_signals={s:signal.signal(s,stop) for s in (signal.SIGTERM,signal.SIGHUP,signal.SIGINT)}
    try:
        tty.setraw(0)
        os.write(1,b'\x1b[?1000l\x1b[?1002l\x1b[?1003l\x1b[?1006l')
        proc=subprocess.Popen([str(exe),'--terminal-stream',*sys.argv[1:]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,bufsize=0)
        outputs={proc.stdout.fileno():1,proc.stderr.fileno():2}
        while True:
            current=os.get_terminal_size(1)
            dims=(min(1000,max(1,current.columns)),min(500,max(1,current.lines)))
            if dims!=size:
                buffer.extend(('SIZE %d %d\n'%dims).encode('ascii'));size=dims
            if stopping:buffer.extend(b'KEY 81 0 113\n');stopping=False
            expired=decoder.since and time.monotonic()-decoder.since>.08
            for key,mods,ch in decoder.feed(b'',expired):buffer.extend(('KEY %d %d %d\n'%(key,mods,ch)).encode('ascii'))
            ready,writeable,_=select.select([0,*outputs], [proc.stdin.fileno()] if buffer else [], [], .05)
            if 0 in ready:
                data=os.read(0,4096)
                if not data:break
                for key,mods,ch in decoder.feed(data):buffer.extend(('KEY %d %d %d\n'%(key,mods,ch)).encode('ascii'))
            for fd in list(outputs):
                if fd in ready:
                    data=os.read(fd,65536)
                    if not data:del outputs[fd]
                    else:
                        view=memoryview(data)
                        while view:view=view[os.write(outputs[fd],view):]
            if writeable and buffer:
                n=os.write(proc.stdin.fileno(),buffer);del buffer[:n]
            if proc.poll() is not None and not outputs:return proc.returncode
    except BrokenPipeError:
        return proc.wait() if proc else 1
    finally:
        if proc and proc.poll() is None:
            proc.stdin.close()
            try:proc.wait(timeout=3)
            except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=3)
        termios.tcsetattr(0,termios.TCSADRAIN,saved)
        for sig,handler in old_signals.items():signal.signal(sig,handler)
        # Also restore the host after an interrupted child or failed launch.
        os.write(1,b'\x1b[0m\x1b[?7h\x1b[?25h\x1b[?1049l')

if __name__=='__main__':sys.exit(main())
