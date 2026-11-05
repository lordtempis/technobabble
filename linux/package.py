#!/usr/bin/env python3
"""Build a Debian package from the portable runtime, or the original small Ubuntu package."""
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

from manpage import install_manual

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT / "Technobabble.csproj"
VERSION = ET.parse(PROJECT).findtext(".//Version")
SYSTEM_LIBS = (
    "ca-certificates, libc6 (>= 2.27), libgcc-s1, libstdc++6, libgssapi-krb5-2, "
    "libicu78 | libicu76 | libicu74 | libicu72 | libicu70, libssl3t64 | libssl3, "
    "libbrotli1, tzdata, zlib1g"
)


def install_portable(archive, work, app, doc):
    name = f"technobabble-{VERSION}-linux-x64"
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    if (archive.name != name + ".tar.gz" or
            Path(str(archive) + ".sha256").read_text().split() != [digest, archive.name]):
        raise ValueError("Expected the matching portable archive and its SHA-256 file")
    with tarfile.open(archive, "r:gz") as tar:
        tar.extractall(work, filter="data")
    bundle = work / name
    shutil.move(bundle / "lib", app)
    shutil.move(bundle / "dotnet-notices", doc / "dotnet-notices")


def build(out, archive):
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryDirectory(dir=out) as scratch:
        work = Path(tmp)
        stage = work / "package"
        app = stage / "usr/lib/technobabble"
        commands = stage / "usr/bin"
        doc = stage / "usr/share/doc/technobabble"
        metadata = stage / "DEBIAN"
        for directory in (app.parent, commands, doc, metadata):
            directory.mkdir(parents=True)
        if archive:
            arch, revision, depends = "amd64", "2", SYSTEM_LIBS
            install_portable(archive, work, app, doc)
        else:
            arch = subprocess.check_output(["dpkg", "--print-architecture"], text=True).strip()
            revision, depends = "1", "dotnet-runtime-10.0"
            subprocess.run(["dotnet", "publish", str(PROJECT), "-c", "Release", "--no-self-contained",
                            "--nologo", "-o", str(app)], check=True)
        for file in app.iterdir():
            file.chmod(0o755 if file.name == "technobabble" else 0o644)
        for name in ("technobabble", "technobabble-terminal"):
            (commands / name).symlink_to("../lib/technobabble/technobabble")
        install_manual(stage / "usr/share/man/man1", compressed=True)
        for name in ("LICENSE.txt", "NOTICE.txt", "README.md"):
            shutil.copyfile(ROOT / name, doc / name)
        shutil.copyfile(ROOT / "source/assets/standard.flf", doc / "standard.flf")
        shutil.copyfile(ROOT / "NOTICE.txt", doc / "copyright")
        version = f"{VERSION}-{revision}"
        (metadata / "control").write_text(f"""Package: technobabble
Version: {version}
Architecture: {arch}
Maintainer: Technobabble Project <project@technobabble.invalid>
Section: games
Priority: optional
Homepage: https://github.com/lordtempis/technobabble
Depends: {depends}
Description: Animated Linux terminal dashboards and cinematic monitoring panes
 Classic and Studio modes, live Linux system data, editable layouts and
 synchronized fictional Relay Watch incidents.
""")
        deb = Path(scratch) / f"technobabble_{version}_{arch}.deb"
        subprocess.run(["dpkg-deb", "--root-owner-group", "-Zxz", "-z3", "--build", stage, deb], check=True)
        checksum = Path(str(deb) + ".sha256")
        checksum.write_text(f"{hashlib.sha256(deb.read_bytes()).hexdigest()}  {deb.name}\n")
        for file in (deb, checksum):
            file.replace(out / file.name)
        print(out / deb.name)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("out", nargs="?", type=Path, default=ROOT / "dist")
    parser.add_argument("--portable", type=Path, metavar="ARCHIVE", help="bundle the linux-x64 runtime")
    args = parser.parse_args()
    build(args.out.resolve(), args.portable)
