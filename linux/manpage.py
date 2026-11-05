"""Install the terminal manual and its alternate command name into a package tree."""
import gzip
from pathlib import Path


def install_manual(directory, *, compressed=False):
    directory.mkdir(parents=True, exist_ok=True)
    data = (Path(__file__).resolve().parent / "man/technobabble.1").read_bytes()
    suffix = ".1.gz" if compressed else ".1"
    page = directory / ("technobabble" + suffix)
    page.write_bytes(gzip.compress(data, mtime=0) if compressed else data)
    (directory / ("technobabble-terminal" + suffix)).symlink_to(page.name)
