import json
from pathlib import Path
import shutil


def copy_runtime_notices(app, obj, dest, rid):
    options = json.loads((app / "technobabble.runtimeconfig.json").read_text())["runtimeOptions"]
    version = next(f["version"] for f in options["includedFrameworks"] if f["name"] == "Microsoft.NETCore.App")
    assets = json.loads((obj / "project.assets.json").read_text())
    pack = "Microsoft.NETCore.App.Runtime." + rid
    candidates = [Path(path) / pack.lower() / version for path in assets["packageFolders"]]
    candidates.append(Path(shutil.which("dotnet")).resolve().parent / "packs" / pack / version)
    for directory in candidates:
        if all((directory / name).is_file() for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT")):
            dest.mkdir()
            for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
                shutil.copyfile(directory / name, dest / name)
            return
    raise RuntimeError(f"Missing redistribution notices for {pack} {version}")
