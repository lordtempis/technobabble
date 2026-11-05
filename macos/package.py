#!/usr/bin/env python3
"""Build the self-contained preview installer on a Mac of the target architecture."""
import argparse
import hashlib
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
LINUX = ROOT.parent / "linux"
sys.path.insert(0, str(LINUX))
from manpage import install_manual
from runtime_notices import copy_runtime_notices


def build(out):
    arch = platform.machine()
    if platform.system() != "Darwin" or arch not in ("arm64", "x86_64"):
        raise SystemExit("Build this installer on an Apple Silicon or Intel Mac.")
    rid = "osx-arm64" if arch == "arm64" else "osx-x64"
    project = ROOT / "Technobabble.csproj"
    version = ET.parse(project).findtext(".//Version")
    name = f"Technobabble-{version}-{rid}-preview.pkg"
    identifier = "io.github.lordtempis.technobabble.cli"
    out.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryDirectory(dir=out) as output_tmp:
        work = Path(tmp)
        stage = work / "payload"
        app = stage / "usr/local/lib/technobabble"
        doc = stage / "usr/local/share/doc/technobabble"
        commands = stage / "usr/local/bin"
        doc.mkdir(parents=True)
        commands.mkdir(parents=True)
        obj = work / "obj"
        subprocess.run(["dotnet", "publish", project, "-c", "Release", "-r", rid,
                        "--self-contained", "true", "--source", "https://api.nuget.org/v3/index.json",
                        f"-p:BaseIntermediateOutputPath={obj}/", f"-p:BaseOutputPath={work}/bin/", "-o", app], check=True)
        for command in ("technobabble", "technobabble-terminal"):
            (commands / command).symlink_to("../lib/technobabble/technobabble")
        install_manual(stage / "usr/local/share/man/man1")
        for file in ("LICENSE.txt", "NOTICE.txt"):
            shutil.copyfile(LINUX / file, doc / file)
        shutil.copyfile(LINUX / "source/assets/standard.flf", doc / "standard.flf")
        shutil.copyfile(ROOT / "README.md", doc / "README.md")
        copy_runtime_notices(app, obj, doc / "dotnet-notices", rid)
        subprocess.run([app / "technobabble", "--version"], check=True)
        subprocess.run(["pkgbuild", "--root", stage, "--identifier", identifier, "--version", version,
                        "--install-location", "/", "--ownership", "recommended", work/"cli.pkg"], check=True)
        distribution = work / "Distribution.xml"
        distribution.write_text(f'''<installer-gui-script minSpecVersion="2">
<title>Technobabble macOS Preview</title>
<options customize="never" hostArchitectures="{arch}" />
<volume-check><allowed-os-versions><os-version min="15.0"/></allowed-os-versions></volume-check>
<choices-outline><line choice="cli"/></choices-outline>
<choice id="cli" visible="false"><pkg-ref id="{identifier}"/></choice>
<pkg-ref id="{identifier}" version="{version}">cli.pkg</pkg-ref>
</installer-gui-script>''')
        package = Path(output_tmp) / name
        subprocess.run(["productbuild", "--distribution", distribution, "--package-path", work, package], check=True)
        digest = hashlib.sha256(package.read_bytes()).hexdigest()
        checksum = Path(str(package)+".sha256")
        checksum.write_text(f"{digest}  {name}\n")
        for file in (package, checksum):
            file.replace(out / file.name)
        print(out / name, digest)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("out", nargs="?", type=Path, default=ROOT / "dist")
    build(parser.parse_args().out.resolve())
