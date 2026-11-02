#!/usr/bin/env bash
# Builds the native WebRTC library of NativeWebRtcPeer (libdatachannel, data channels only: no media, no websocket,
# TLS on mbedtls linked in statically) for the desktop players and editors. The binaries are committed into
# Runtime/Plugins/libdatachannel, so rebuild only to update the pinned versions below. They get a name of their own
# (framework_datachannel, see NativeWebRtcApi): an installed libdatachannel must not take their place.
#   ./build.sh            linux-x64 and win-x64
#   ./build.sh linux      one target
# Both build in docker: linux in the Steam Runtime sniper SDK (glibc 2.31, so the library loads in the Steam runtime and
# on older distributions), windows in a static mingw image.
set -euo pipefail

LIBDATACHANNEL_VERSION=v0.24.6
MBEDTLS_VERSION=v3.6.7
LINUX_IMAGE=registry.gitlab.steamos.cloud/steamrt/sniper/sdk:latest
WINDOWS_IMAGE=dockcross/windows-static-x64

cd "$(dirname "$0")"
root=$(pwd)
work="$root/build"
plugins="$root/../../Runtime/Plugins/libdatachannel"
targets=("${@:-linux windows}")
targets=(${targets[@]})

fetch() {
    local url=$1 version=$2 dir=$3

    if [ -d "$dir" ]; then
        return
    fi

    git clone --depth 1 --branch "$version" --recurse-submodules --shallow-submodules "$url" "$dir"
}

# The mbedtls of a target is given by its files: a cross toolchain searches its own root only. Its archives are repeated
# at the end of the link line: a single pass linker resolves x509 -> crypto only in that order.
# Runs inside the toolchain of a target (the host for linux, the docker image for windows): $1 = target name,
# $2 = extra cmake arguments (the toolchain), $3 = linker flags of the shared library, $4 = system libraries.
build_target() {
    local target=$1 cmake_args=$2 link_flags=$3 system_libraries=${4:-}
    local out="$work/$target"

    cmake -S "$work/mbedtls" -B "$out/mbedtls" -G Ninja $cmake_args \
        -DCMAKE_BUILD_TYPE=Release -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
        -DENABLE_TESTING=OFF -DENABLE_PROGRAMS=OFF -DUSE_SHARED_MBEDTLS_LIBRARY=OFF \
        -DCMAKE_INSTALL_PREFIX="$out/prefix"
    cmake --build "$out/mbedtls"
    cmake --install "$out/mbedtls"

    cmake -S "$work/libdatachannel" -B "$out/libdatachannel" -G Ninja $cmake_args \
        -DCMAKE_BUILD_TYPE=Release -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
        -DMbedTLS_INCLUDE_DIR="$out/prefix/include" -DMbedTLS_LIBRARY="$out/prefix/lib/libmbedtls.a" \
        -DMbedCrypto_LIBRARY="$out/prefix/lib/libmbedcrypto.a" -DMbedX509_LIBRARY="$out/prefix/lib/libmbedx509.a" \
        -DBUILD_SHARED_LIBS=ON -DUSE_MBEDTLS=ON -DNO_MEDIA=ON -DNO_WEBSOCKET=ON -DNO_EXAMPLES=ON -DNO_TESTS=ON \
        -DCMAKE_SHARED_LINKER_FLAGS="$link_flags" \
        -DCMAKE_CXX_STANDARD_LIBRARIES="$out/prefix/lib/libmbedx509.a $out/prefix/lib/libmbedcrypto.a $system_libraries"
    cmake --build "$out/libdatachannel" --target datachannel
}

if [ "${1:-}" = "--inside" ]; then
    shift
    build_target "$@"
    exit 0
fi

mkdir -p "$work"
fetch https://github.com/Mbed-TLS/mbedtls.git "$MBEDTLS_VERSION" "$work/mbedtls"
# The DTLS transport of libdatachannel negotiates SRTP profiles even without media.
python3 "$work/mbedtls/scripts/config.py" -f "$work/mbedtls/include/mbedtls/mbedtls_config.h" set MBEDTLS_SSL_DTLS_SRTP
fetch https://github.com/paullouisageneau/libdatachannel.git "$LIBDATACHANNEL_VERSION" "$work/libdatachannel"

# The script itself runs inside the image with --inside.
run_in() {
    local image=$1
    shift
    docker run --rm -u "$(id -u):$(id -g)" -v "$root:/work/src" -w /work/src "$image" bash ./build.sh --inside "$@"
}

for target in "${targets[@]}"; do
    case "$target" in
        linux)
            run_in "$LINUX_IMAGE" linux "-DCMAKE_C_COMPILER=gcc-14 -DCMAKE_CXX_COMPILER=g++-14" \
                "-s -static-libstdc++ -static-libgcc -Wl,--exclude-libs,ALL"
            mkdir -p "$plugins/linux-x64"
            cp -L "$work/linux/libdatachannel/libdatachannel.so.${LIBDATACHANNEL_VERSION#v}" "$plugins/linux-x64/libframework_datachannel.so"
            ;;
        windows)
            run_in "$WINDOWS_IMAGE" windows \
                "-DCMAKE_TOOLCHAIN_FILE=/usr/src/mxe/usr/x86_64-w64-mingw32.static/share/cmake/mxe-conf.cmake" \
                "-s -static -static-libgcc -static-libstdc++" "-lbcrypt -lws2_32 -liphlpapi"
            mkdir -p "$plugins/win-x64"
            cp "$work/windows/libdatachannel/libdatachannel.dll" "$plugins/win-x64/framework_datachannel.dll"
            ;;
        *)
            echo "Unknown target: $target" >&2
            exit 1
            ;;
    esac
done

echo "Built into: $(realpath "$plugins")"
