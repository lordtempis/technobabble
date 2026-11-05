#!/bin/sh
# Run only inside a disposable Debian/Ubuntu container.
set -eu
export DEBIAN_FRONTEND=noninteractive
if command -v dotnet; then echo "Expected a clean system without .NET" >&2; exit 1; fi
mode=$1
set -- /src/dist/apt/technobabble_*_amd64.deb
test "$#" -eq 1
package=$1
expected=$(dpkg-deb -f "$package" Version)
apt-get update -qq
# Minimal images normally exclude manual pages. Test a system with man installed.
printf 'path-include=/usr/share/man/*\n' > /etc/dpkg/dpkg.cfg.d/zz-technobabble-test-man
apt-get install -y --no-install-recommends ca-certificates python3-pyte man-db bsdextrautils
case "$mode" in
    local)
        # Minimal container images may tell dpkg to discard optional documentation.
        dpkg-deb --fsys-tarfile "$package" | tar -tf - > /tmp/technobabble-package-files
        grep -Fx './usr/share/man/man1/technobabble.1.gz' /tmp/technobabble-package-files
        grep -Fx './usr/share/man/man1/technobabble-terminal.1.gz' /tmp/technobabble-package-files
        grep -Fx './usr/share/doc/technobabble/dotnet-notices/LICENSE.TXT' /tmp/technobabble-package-files
        grep -Fx './usr/share/doc/technobabble/dotnet-notices/THIRD-PARTY-NOTICES.TXT' /tmp/technobabble-package-files
        apt install -y --no-install-recommends "$package"
        ;;
    public|upgrade|upgrade-bundled)
        if [ "$mode" = upgrade ]; then
            apt install -y /src/dist/upgrade-from-v1.13/technobabble_1.13.0-1_amd64.deb
            test "$(dpkg-query -W -f='${Version}' technobabble)" = 1.13.0-1
        fi
        install -D -m 0644 /src/apt/technobabble.asc /etc/apt/keyrings/technobabble.asc
        install -m 0644 /src/apt/technobabble.sources /etc/apt/sources.list.d/technobabble.sources
        apt-get update
        apt-cache policy technobabble
        if [ "$mode" = upgrade-bundled ]; then
            apt install -y technobabble=1.13.1-2
            test "$(dpkg-query -W -f='${Version}' technobabble)" = 1.13.1-2
        fi
        if [ "$mode" != public ]; then
            apt upgrade -y
        else
            apt install -y technobabble
        fi
        ;;
    *) exit 2 ;;
esac
test "$(dpkg-query -W -f='${Version}' technobabble)" = "$expected"
test "$(technobabble --version)" = "Technobabble ${expected%-*} (Linux)"
if [ "$mode" != upgrade ] && command -v dotnet; then echo "Unexpected system .NET installation" >&2; exit 1; fi
dpkg-query -S /usr/bin/technobabble /usr/bin/technobabble-terminal
technobabble --version
technobabble-terminal --version
# Ubuntu's minimized image diverts /usr/bin/man to a message even after
# installing man-db. Use the reader that dpkg actually installed.
reader=$(dpkg-divert --truename /usr/bin/man)
for name in technobabble technobabble-terminal; do
    "$reader" -w 1 "$name"
    MANPAGER=cat "$reader" 1 "$name" | col -bx > "/tmp/$name.manual"
    grep -Fq TECHNOBABBLE_LAYOUT_DIR "/tmp/$name.manual"
done
python3 /src/tests/terminal.py /usr/bin/technobabble
apt remove -y technobabble
test ! -e /usr/bin/technobabble && test ! -e /usr/lib/technobabble/libcoreclr.so
test ! -e /usr/share/man/man1/technobabble.1.gz && test ! -L /usr/share/man/man1/technobabble-terminal.1.gz
