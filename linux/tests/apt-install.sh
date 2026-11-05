#!/bin/sh
# Run only inside a disposable Debian/Ubuntu container.
set -eu
export DEBIAN_FRONTEND=noninteractive
if command -v dotnet; then echo "Expected a clean system without .NET" >&2; exit 1; fi
mode=$1
apt-get update -qq
apt-get install -y --no-install-recommends ca-certificates python3-pyte
case "$mode" in
    local)
        set -- /src/dist/apt/technobabble_*_amd64.deb
        test "$#" -eq 1
        # Minimal container images may tell dpkg to discard optional documentation.
        dpkg-deb --fsys-tarfile "$1" | tar -tf - > /tmp/technobabble-package-files
        grep -Fx './usr/share/doc/technobabble/dotnet-notices/LICENSE.TXT' /tmp/technobabble-package-files
        grep -Fx './usr/share/doc/technobabble/dotnet-notices/THIRD-PARTY-NOTICES.TXT' /tmp/technobabble-package-files
        apt install -y --no-install-recommends "$1"
        ;;
    public|upgrade)
        if [ "$mode" = upgrade ]; then
            apt install -y /src/dist/upgrade-from-v1.13/technobabble_1.13.0-1_amd64.deb
            test "$(dpkg-query -W -f='${Version}' technobabble)" = 1.13.0-1
        fi
        install -D -m 0644 /src/apt/technobabble.asc /etc/apt/keyrings/technobabble.asc
        install -m 0644 /src/apt/technobabble.sources /etc/apt/sources.list.d/technobabble.sources
        apt-get update
        apt-cache policy technobabble
        if [ "$mode" = upgrade ]; then
            apt upgrade -y
            dpkg --compare-versions "$(dpkg-query -W -f='${Version}' technobabble)" gt 1.13.0-1
        else
            apt install -y technobabble
        fi
        ;;
    *) exit 2 ;;
esac
if [ "$mode" != upgrade ] && command -v dotnet; then echo "Unexpected system .NET installation" >&2; exit 1; fi
dpkg-query -S /usr/bin/technobabble /usr/bin/technobabble-terminal
technobabble --version
technobabble-terminal --version
python3 /src/tests/terminal.py /usr/bin/technobabble
apt remove -y technobabble
test ! -e /usr/bin/technobabble && test ! -e /usr/lib/technobabble/libcoreclr.so
