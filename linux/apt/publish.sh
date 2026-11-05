#!/usr/bin/env bash
# Run on a clean checkout of the apt branch, then commit and push the result.
set -euo pipefail
if (( $# != 3 )); then
    echo "Usage: bash publish.sh APT_CHECKOUT PACKAGE.deb FULL_SIGNING_FINGERPRINT" >&2
    exit 2
fi
repo=$(realpath "$1")
deb=$(realpath "$2")
fingerprint=${3^^}
[[ $fingerprint =~ ^[A-F0-9]{40}$ ]]
[[ $(git -C "$repo" symbolic-ref --short HEAD) == apt ]]
[[ $(dpkg-deb -f "$deb" Package) == technobabble && $(dpkg-deb -f "$deb" Architecture) == amd64 ]]
version=$(dpkg-deb -f "$deb" Version)
target="pool/main/t/technobabble/technobabble_${version}_amd64.deb"
cd "$repo"
mkdir -p "$(dirname "$target")"
if [[ -e $target ]]; then cmp "$deb" "$target"; else install -m 0644 "$deb" "$target"; fi

tmp=$(mktemp -d)
trap 'rm -rf -- "$tmp"' EXIT
index=main/binary-amd64
mkdir -p "$tmp/stable/$index"
apt-ftparchive packages pool > "$tmp/stable/$index/Packages"
gzip -n -c "$tmp/stable/$index/Packages" > "$tmp/stable/$index/Packages.gz"
apt-ftparchive -o APT::FTPArchive::Release::Origin=Technobabble \
    -o APT::FTPArchive::Release::Label=Technobabble -o APT::FTPArchive::Release::Suite=stable \
    -o APT::FTPArchive::Release::Codename=stable -o APT::FTPArchive::Release::Architectures=amd64 \
    -o APT::FTPArchive::Release::Components=main -o APT::FTPArchive::Release::Acquire-By-Hash=yes \
    release "$tmp/stable" > "$tmp/Release"
sign=(gpg --local-user "$fingerprint" --digest-algo SHA256)
"${sign[@]}" --output "$tmp/InRelease" --clearsign "$tmp/Release"
"${sign[@]}" --output "$tmp/Release.gpg" --armor --detach-sign "$tmp/Release"
gpg --export-options export-minimal --export "$fingerprint" > "$tmp/key.gpg"
gpgv --keyring "$tmp/key.gpg" "$tmp/InRelease"
gpgv --keyring "$tmp/key.gpg" "$tmp/Release.gpg" "$tmp/Release"

dest="dists/stable/$index"
mkdir -p "$dest/by-hash/"{SHA256,SHA512}
for file in Packages Packages.gz; do
    install -m 0644 "$tmp/stable/$index/$file" "$dest/$file"
    for bits in 256 512; do
        digest=$(sha${bits}sum "$dest/$file")
        install -m 0644 "$dest/$file" "$dest/by-hash/SHA$bits/${digest%% *}"
    done
done
install -m 0644 "$tmp/"{Release,InRelease,Release.gpg} dists/stable/
