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
        apt install -y --no-install-recommends "$1"
        ;;
    public|upgrade)
        if [ "$mode" = upgrade ]; then
            apt-get install -y --no-install-recommends curl
            cd /tmp
            old=technobabble_1.13.0-1_amd64.deb
            curl -fsSLO "https://github.com/lordtempis/technobabble/releases/download/v1.13/$old"
            echo "4e40bcad5de42941199e832c520cb910886006ac7d862e2718428a2974897995  $old" | sha256sum -c -
            apt install -y "./$old"
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
test -s /usr/share/doc/technobabble/dotnet-notices/LICENSE.TXT
python3 /src/tests/terminal.py /usr/bin/technobabble
apt remove -y technobabble
test ! -e /usr/bin/technobabble && test ! -e /usr/lib/technobabble/libcoreclr.so
