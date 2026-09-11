#!/usr/bin/env bash
#
# Builds self-contained Linkora binaries for macOS and Linux.
#
# The output needs no .NET runtime installed on the target machine. Results land in dist/.
#
# Usage:  ./build-release.sh [rid ...]
#         ./build-release.sh                 # all default targets
#         ./build-release.sh osx-arm64       # just one
#
set -euo pipefail

cd "$(dirname "$0")"

DEFAULT_RIDS=(osx-arm64 osx-x64 linux-x64 linux-arm64 portable)
RIDS=("${@:-}")

if [ -z "${RIDS[0]:-}" ]; then
    RIDS=("${DEFAULT_RIDS[@]}")
fi

DIST="dist"

# Finder can recreate .DS_Store while the tree is being deleted, which makes a single
# rm -rf fail with "Directory not empty"; sweep those out and retry before giving up.
rm -rf "$DIST" 2>/dev/null || true

if [ -d "$DIST" ]; then
    find "$DIST" -name '.DS_Store' -delete 2>/dev/null || true
    rm -rf "$DIST" 2>/dev/null || true
fi

if [ -d "$DIST" ]; then
    echo "Could not clear $DIST - close anything using it and try again." >&2
    exit 1
fi

mkdir -p "$DIST"

APP_NAME="Linkora"
BUNDLE_ID="net.linkora.client"
VERSION="1.1.0"

# Stamped into the binaries and shown at the foot of the client's window.
BUILD_STAMP="$(date +%Y%m%d)"
INFORMATIONAL_VERSION="${VERSION}+build.${BUILD_STAMP}"

publish() {
    local project="$1" rid="$2" out="$3"

    dotnet publish "$project" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -p:InformationalVersion="$INFORMATIONAL_VERSION" \
        --output "$out" \
        --nologo --verbosity quiet
}

# One build that runs on macOS, Linux and Windows alike. There is no such thing as a single
# native executable for several operating systems, so this is framework-dependent: it needs the
# .NET runtime installed on the target and is started with "dotnet WLMServer.dll".
publish_portable() {
    local project="$1" out="$2"

    dotnet publish "$project" \
        --configuration Release \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -p:InformationalVersion="$INFORMATIONAL_VERSION" \
        --output "$out" \
        --nologo --verbosity quiet
}

# Wraps a published client folder into a double-clickable macOS .app bundle.
make_app_bundle() {
    local publish_dir="$1" bundle="$2" icon_source="$3"

    rm -rf "$bundle"
    mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"

    cp -R "$publish_dir"/* "$bundle/Contents/MacOS/"

    if [ -f "$icon_source" ]; then
        cp "$icon_source" "$bundle/Contents/Resources/AppIcon.png"
    fi

    cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key><string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key><string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key><string>${VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleExecutable</key><string>WLMClient</string>
    <key>CFBundleIconFile</key><string>AppIcon.png</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

    chmod +x "$bundle/Contents/MacOS/WLMClient"
}

for rid in "${RIDS[@]}"; do
    echo "==> Building for $rid"

    target="$DIST/$rid"
    mkdir -p "$target"

    if [ "$rid" = "portable" ]; then
        echo "    server (portable)..."
        publish_portable WLMServer/WLMServer.csproj "$target/server"

        cat > "$target/README-portable.txt" <<'TXT'
One server build for every platform.

This is the same server as the per-platform folders, but it is not tied to an
operating system: the same files run on macOS, Linux and Windows. It needs the
.NET 8 runtime (or newer) installed, which the self-contained builds do not.

    Install the runtime:  https://dotnet.microsoft.com/download
    Start the server:     dotnet WLMServer.dll

Edit Messenger.config next to WLMServer.dll first.

On Linux the runtime also needs OpenSSL for the MySQL connection:

    Debian/Ubuntu:  sudo apt install libssl3
    Fedora:         sudo dnf install openssl-libs

A single running server already serves macOS, Linux and Windows clients at the
same time - they all speak the same protocol, so you never need more than one.
TXT

        echo "    done: $target"
        continue
    fi

    echo "    server..."
    publish WLMServer/WLMServer.csproj "$rid" "$target/server"

    echo "    client..."
    publish WLMClient/WLMClient.csproj "$rid" "$target/client"

    case "$rid" in
        osx-*)
            echo "    packaging .app bundle..."
            make_app_bundle "$target/client" "$target/${APP_NAME}.app" \
                "WLMClient/Content/Icons/33.png"
            rm -rf "$target/client"

            # macOS quarantines unsigned downloads; note the workaround next to the build.
            cat > "$target/README-macos.txt" <<'TXT'
The app is not code signed, so macOS Gatekeeper will refuse to open it on first run.

Clear the quarantine flag once, then open it normally:

    xattr -dr com.apple.quarantine "Linkora.app"

Alternatively: right click the app, choose Open, then confirm.

Settings live in Contents/MacOS/Messenger.config inside the bundle.
TXT
            ;;
        linux-*)
            chmod +x "$target/client/WLMClient" "$target/server/WLMServer"

            cat > "$target/README-linux.txt" <<'TXT'
Run the client with ./client/WLMClient and the server with ./server/WLMServer.

The server needs OpenSSL, which the MySQL connector uses for TLS:

    Debian/Ubuntu:  sudo apt install libssl3
    Fedora:         sudo dnf install openssl-libs

The client additionally needs a desktop session and the usual X11/Wayland client
libraries plus a font:

    Debian/Ubuntu:  sudo apt install libx11-6 libice6 libsm6 libfontconfig1 fonts-dejavu-core
    Fedora:         sudo dnf install libX11 libICE libSM fontconfig dejavu-sans-fonts

Notification sounds use paplay, aplay, pw-play or ffplay, whichever is present.
TXT
            ;;
    esac

    echo "    done: $target"
done

echo
echo "Build complete. Output:"
du -sh "$DIST"/* 2>/dev/null || true
