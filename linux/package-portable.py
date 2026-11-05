#!/usr/bin/env python3
"""Build the self-contained Linux x64 release archive and its SHA-256 checksum."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parent
PROJECT = ROOT / "Technobabble.csproj"
RID = "linux-x64"


def copy_runtime_notices(app, obj, dest):
    options = json.loads((app / "technobabble.runtimeconfig.json").read_text())["runtimeOptions"]
    version = next(f["version"] for f in options["includedFrameworks"] if f["name"] == "Microsoft.NETCore.App")
    assets = json.loads((obj / "project.assets.json").read_text())
    pack = "Microsoft.NETCore.App.Runtime." + RID
    candidates = [Path(path) / pack.lower() / version for path in assets["packageFolders"]]
    candidates.append(Path(shutil.which("dotnet")).resolve().parent / "packs" / pack / version)
    for directory in candidates:
        if all((directory / name).is_file() for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT")):
            dest.mkdir()
            for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
                shutil.copyfile(directory / name, dest / name)
            return
    raise RuntimeError(f"Missing redistribution notices for {pack} {version}")


def archive_metadata(entry):
    entry.uid = entry.gid = entry.mtime = 0
    entry.uname = entry.gname = ""
    entry.mode = 0o755 if entry.isdir() or entry.name.endswith("/lib/technobabble") else 0o644
    if entry.issym():
        entry.mode = 0o777
    return entry


def build(out):
    version = ET.parse(PROJECT).findtext(".//Version")
    name = f"technobabble-{version}-{RID}"
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryDirectory(dir=out) as output_tmp:
        work = Path(tmp)
        bundle = work / name
        app = bundle / "lib"
        obj = work / "obj"
        subprocess.run([
            "dotnet", "publish", str(PROJECT), "-c", "Release", "-r", RID, "--self-contained", "true",
            "--source", "https://api.nuget.org/v3/index.json", "--nologo", "-o", str(app),
            f"-p:BaseIntermediateOutputPath={obj}/", f"-p:BaseOutputPath={work}/bin/",
        ], check=True)
        for command in ("technobabble", "technobabble-terminal"):
            (bundle / command).symlink_to("lib/technobabble")
        for doc in ("README.md", "LICENSE.txt", "NOTICE.txt"):
            shutil.copyfile(ROOT / doc, bundle / doc)
        shutil.copyfile(ROOT / "source/assets/standard.flf", bundle / "standard.flf")
        copy_runtime_notices(app, obj, bundle / "dotnet-notices")

        archive = Path(output_tmp) / (name + ".tar.gz")
        with archive.open("wb") as stream, gzip.GzipFile(filename="", fileobj=stream, mode="wb", mtime=0) as gz:
            with tarfile.open(fileobj=gz, mode="w") as tar:
                tar.add(bundle, arcname=name, filter=archive_metadata)
        checksum = archive.with_suffix(archive.suffix + ".sha256")
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        checksum.write_text(f"{digest}  {archive.name}\n", encoding="ascii")
        for file in (archive, checksum):
            file.replace(out / file.name)
        print(f"{out / archive.name}\nSHA-256 {digest}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("out", nargs="?", type=Path, default=ROOT / "dist")
    build(parser.parse_args().out.resolve())
