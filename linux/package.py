#!/usr/bin/env python3
"""Build the Ubuntu package using a Linux .NET 10 SDK. No NuGet packages."""
from pathlib import Path
import shutil, subprocess, sys, tempfile

root=Path(__file__).resolve().parent
out=Path(sys.argv[1] if len(sys.argv)>1 else root/'dist').resolve()
out.mkdir(parents=True,exist_ok=True)
arch=subprocess.check_output(['dpkg','--print-architecture'],text=True).strip()
version='1.13.0-1'
with tempfile.TemporaryDirectory(prefix='technobabble-package-') as temp:
    stage=Path(temp)/'package'
    app=stage/'usr/lib/technobabble'
    commands=stage/'usr/bin'
    doc=stage/'usr/share/doc/technobabble'
    metadata=stage/'DEBIAN'
    for directory in (commands,doc,metadata):directory.mkdir(parents=True)
    subprocess.run(['dotnet','publish',str(root/'Technobabble.csproj'),'-c','Release',
                    '--no-self-contained','--nologo','-o',str(app)],check=True)
    # DrvFs may present build artifacts as 0777. Set the installed modes in
    # Linux staging so only root can modify the system-installed application.
    for file in app.iterdir():
        file.chmod(0o755 if file.name=='technobabble' else 0o644)
    for name in ('technobabble','technobabble-terminal'):
        (commands/name).symlink_to('../lib/technobabble/technobabble')
    for name in ('LICENSE.txt','NOTICE.txt','README.md'):
        shutil.copyfile(root/name,doc/name)
    shutil.copyfile(root/'source/assets/standard.flf',doc/'standard.flf')
    (metadata/'control').write_text(f'''Package: technobabble
Version: {version}
Architecture: {arch}
Maintainer: Technobabble local build <root@localhost>
Section: games
Priority: optional
Depends: dotnet-runtime-10.0
Description: Animated Linux terminal dashboards and cinematic monitoring panes
 Classic and Studio modes, live Linux system data, editable layouts and
 synchronized fictional Relay Watch incidents.
''')
    target=out/f'technobabble_{version}_{arch}.deb'
    subprocess.run(['dpkg-deb','--root-owner-group','--build',str(stage),str(target)],check=True)
    print(target)
