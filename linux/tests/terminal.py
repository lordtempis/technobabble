#!/usr/bin/env python3
"""Real Linux PTY integration checks. Requires python3-pyte (tests only)."""
import errno, fcntl, os, pty, select, signal, struct, sys, tempfile, termios, time
from pathlib import Path
import pyte

class Session:
    def __init__(self, app, data, args, width=120, height=40, layout_dir=None):
        self.master,self.slave=pty.openpty()
        self.original=termios.tcgetattr(self.slave)
        self.screen=pyte.Screen(width,height)
        self.stream=pyte.ByteStream(self.screen)
        self.resize(width,height)
        # Darwin revokes a controlling tty when its session leader exits. Keep
        # a shell-like leader alive to check tty settings after the app quits.
        pid_pipe=os.pipe() if sys.platform=="darwin" else None
        self.leader=os.fork()
        if self.leader==0:
            os.close(self.master);os.setsid();fcntl.ioctl(self.slave,termios.TIOCSCTTY,0)
            for fd in (0,1,2):os.dup2(self.slave,fd)
            if self.slave>2:os.close(self.slave)
            if pid_pipe is not None:
                os.close(pid_pipe[0])
                child=os.fork()
                if child:
                    os.write(pid_pipe[1],str(child).encode())
                    os.close(pid_pipe[1])
                    status=os.waitpid(child,0)[1]
                    if termios.tcgetattr(0)!=self.original:
                        os.write(2,b"Application did not restore terminal modes\n")
                        os._exit(1)
                    os._exit(os.waitstatus_to_exitcode(status) % 256)
                os.close(pid_pipe[1])
            env=dict(os.environ,TERM='xterm-256color',PATH='/usr/bin:/bin',XDG_DATA_HOME=data)
            for key in ('TECHNOBABBLE_LAYOUT_DIR','HOLLYWOOD_LAYOUT_DIR','WSL_INTEROP','WSLENV'):env.pop(key,None)
            if layout_dir is not None:env['TECHNOBABBLE_LAYOUT_DIR']=str(layout_dir)
            os.execve(app,[app]+args,env)
        self.pid=self.leader
        if pid_pipe is not None:
            os.close(pid_pipe[1])
            self.pid=int(os.read(pid_pipe[0],32))
            os.close(pid_pipe[0])
        self.take(.7)
    def resize(self,w,h):
        fcntl.ioctl(self.slave,termios.TIOCSWINSZ,struct.pack('HHHH',h,w,0,0))
        self.screen.resize(lines=h,columns=w)
    def take(self,seconds=.35):
        end=time.monotonic()+seconds
        while time.monotonic()<end:
            if select.select([self.master],[],[],max(0,end-time.monotonic()))[0]:
                try:chunk=os.read(self.master,65536)
                except OSError as e:
                    if e.errno==errno.EIO:break
                    raise
                if not chunk:break
                self.stream.feed(chunk)
    def key(self,data,seconds=.35):
        os.write(self.master,data);self.take(seconds)
    @property
    def body(self):return '\n'.join(self.screen.display[:-1])
    def __enter__(self):return self
    def __exit__(self,kind,exc,tb):
        try:
            if kind is None:self.key(b'q',.15)
            else:os.kill(self.pid,signal.SIGTERM)
            deadline=time.monotonic()+5
            while time.monotonic()<deadline:
                pid,status=os.waitpid(self.leader,os.WNOHANG)
                if pid:break
                self.take(.1)
            else:
                os.kill(self.pid,signal.SIGKILL);os.waitpid(self.leader,0)
                raise AssertionError('Terminal did not exit')
            if kind is None:
                assert os.waitstatus_to_exitcode(status)==0,status
                if self.pid==self.leader:
                    assert termios.tcgetattr(self.slave)==self.original,'Linux tty modes were not restored'
        finally:os.close(self.master);os.close(self.slave)

def check(app):
    with tempfile.TemporaryDirectory(prefix='technobabble-linux-test-') as data:
        with Session(app,data,['--layout','Relay Watch']) as s:
            assert 'TECHNOBABBLE / STUDIO' in s.screen.display[-1] and s.body.count('#001')==3,s.screen.display[:6]
            assert 'SENSOR SWEEP' in s.body and 'SIGNAL DECODER' in s.body and 'ENGINEERING' in s.body
            assert Path('/proc',str(s.pid),'exe').read_bytes()[:4]==b'\x7fELF'
            s.key(b' ');paused=s.body;s.take(.4);assert paused==s.body
            s.resize(45,12);s.take(.5);s.resize(120,40);s.take(.5);assert paused==s.body
            s.key(b's');assert 'technobabble*' in s.screen.display[-1] and '/ SIM' not in s.body
            s.key(b's');assert paused==s.body
            s.key(b'w');assert 'SAVE STUDIO LAYOUT' in s.body
            s.key(b'Linux Relay\r')
            layout=Path(data)/'technobabble/layouts/Linux Relay.layout'
            assert layout.read_text().splitlines()[2]=='V *radar H *signal *engineering'
        with Session(app,data,['--layout','Linux Relay']) as s:
            assert s.body.count('#001')==3
            s.key(b'e');assert 'PANE EDITOR' in s.body
            s.key(b'r\x1b[F\r');assert 'engineering' in s.screen.display[0],s.screen.display[0]
            s.key(b'u');assert 'radar' in s.screen.display[0]
        with Session(app,data,['--layout','Process Watch','--refresh','1']) as s:
            s.key(b'f');s.key(b'technobabble\r',1.4)
            assert 'Filter: technobabble' in s.body and str(s.pid) in s.body,(s.pid,s.screen.display[:8])
        with Session(app,data,['--layout','Disk Watch','--refresh','1']) as s:
            s.take(2.2)
            assert 'Linux block devices' in s.body and ('/s' in s.body or 'No Linux block device' in s.body),s.screen.display[:8]
        with Session(app,data,['--layout','GPU Watch','--refresh','1']) as s:
            s.take(2.2)
            assert 'GPU ACTIVITY / Linux' in s.body and ('Adapter 1' in s.body or 'unavailable' in s.body),s.screen.display[:8]
        with Session(app,data,[]) as s:
            assert 'technobabble*' in s.screen.display[-1]
            s.key(b'\x03')
        with Session(app,data,['--layout','Relay Watch']) as s:
            os.kill(s.pid,signal.SIGTERM)
            s.take(.4)
        print('Linux PTY: Classic, Relay Watch, save/load, editor, keys, PID filter, disk/GPU, resize, pause, exit and tty restoration passed.')

if __name__=='__main__':check(os.path.abspath(sys.argv[1]))
