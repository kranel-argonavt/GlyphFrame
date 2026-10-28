#!/usr/bin/env bash
# Offline experimental recipe. Inputs are hash-checked by Build-ControlledFfmpeg.ps1.
set -euo pipefail
build_root=$(cygpath -u "$1")
jobs=$2
export SOURCE_DATE_EPOCH=$3
run_fate=$4
x264_revision=$5
[[ "$jobs" =~ ^([1-9]|1[0-6])$ ]]
[[ "$SOURCE_DATE_EPOCH" =~ ^[0-9]+$ ]]
[[ "$run_fate" =~ ^[01]$ ]]
[[ "$x264_revision" =~ ^[0-9a-f]{40}$ ]]
cd "$build_root"
prefix="$build_root/prefix"
sources="$build_root/sources"
work="$build_root/work"
test ! -e "$prefix"
test ! -e "$work"
mkdir -p "$prefix" "$work/zlib" "$work/x264" "$work/ffmpeg" "$build_root/empty-hooks"
export CC=gcc AR=ar RANLIB=ranlib
export PKG_CONFIG_LIBDIR="$prefix/lib/pkgconfig"
export PKG_CONFIG_PATH=
export LC_ALL=C TZ=UTC

# The bundle contains upstream x264 ancestry only, not GlyphFrame's private history.
git -c protocol.allow=never -c protocol.file.allow=always \
  -c core.autocrlf=false -c core.eol=lf -c core.hooksPath="$build_root/empty-hooks" \
  clone --no-checkout "$build_root/inputs/x264-upstream-history.bundle" "$sources/x264-git" > x264-clone.log 2>&1
git -C "$sources/x264-git" -c core.hooksPath="$build_root/empty-hooks" checkout --detach "$x264_revision" > x264-checkout.log 2>&1
test "$(git -C "$sources/x264-git" rev-parse HEAD)" = "$x264_revision"
test -z "$(git -C "$sources/x264-git" status --porcelain)"
diff -qr --exclude=.git "$sources/x264" "$sources/x264-git" > x264-archive-comparison.log
git -C "$sources/x264-git" bundle verify "$build_root/inputs/x264-upstream-history.bundle" > x264-bundle-verify.log 2>&1

cd "$work/zlib"
"$sources/zlib/configure" --prefix="$prefix" --static > configure.log 2>&1
make -r -j"$jobs" > build.log 2>&1
make -r test > test.log 2>&1
make -r install > install.log 2>&1

cd "$work/x264"
"$sources/x264-git/configure" --prefix="$prefix" --host=x86_64-w64-mingw32 \
  --enable-static --disable-cli --disable-opencl \
  --extra-cflags='-O2 -march=x86-64 -mtune=generic' > configure.log 2>&1
grep -Fx '#define X264_POINTVER "0.165.3223 0480cb0"' x264_config.h > version-check.txt
make -r -j"$jobs" > build.log 2>&1
make -r install-lib-static > install.log 2>&1
make -r -j"$jobs" checkasm > checkasm-build.log 2>&1
./checkasm8.exe 1 > checkasm8.log 2>&1
./checkasm10.exe 1 > checkasm10.log 2>&1

cd "$work/ffmpeg"
"$sources/ffmpeg/configure" \
  --prefix="$prefix" --arch=x86_64 --target-os=mingw32 --cpu=x86-64 \
  --enable-runtime-cpudetect --enable-gpl --enable-version3 \
  --disable-shared --enable-static --disable-autodetect \
  --enable-libx264 --enable-zlib --pkg-config-flags=--static \
  --disable-network --disable-ffplay --disable-doc --disable-debug \
  --disable-hwaccels --disable-d3d11va --disable-d3d12va --disable-dxva2 \
  --disable-mediafoundation --disable-vulkan --disable-vaapi --disable-vdpau \
  --disable-pthreads --enable-w32threads \
  --disable-indevs --enable-indev=lavfi --disable-outdevs \
  --extra-cflags='-O2 -march=x86-64 -mtune=generic' \
  --extra-ldflags='-static -static-libgcc' \
  --extra-version=glyphframe.2 > configure.log 2>&1
make -r -j"$jobs" > build.log 2>&1
make -r install > install.log 2>&1
"$prefix/bin/ffmpeg.exe" -version > ffmpeg-version.txt 2>&1
"$prefix/bin/ffprobe.exe" -version > ffprobe-version.txt 2>&1
objdump -p "$prefix/bin/ffmpeg.exe" > ffmpeg-pe-imports.txt
objdump -p "$prefix/bin/ffprobe.exe" > ffprobe-pe-imports.txt
if [[ "$run_fate" = 1 ]]; then
  make -r -j"$jobs" fate > fate.log 2>&1
fi
test -z "$(git -C "$sources/x264-git" status --porcelain)"
printf 'Offline recipe completed; tools remain experimental and unactivated.\n'
