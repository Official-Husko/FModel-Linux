#!/usr/bin/env bash
# Builds Dumper-7 (https://github.com/Encryqed/Dumper-7) on Linux, no Windows or Visual Studio needed.
#
#   linux/tools/dumper7/build.sh            -> linux/out/Dumper-7.dll
#   DUMPER7_REF=<commit> .../build.sh       build another Dumper-7 commit
#
# Dumper-7 dumps an Unreal game's classes from inside the running game, including the .usmap mappings
# FModel needs for games with unversioned properties. It is a Windows DLL, which is exactly what runs
# inside a Windows game under Proton/Wine; this script only cross-compiles it.
#
# Needs: clang, lld, cmake, ninja and the MinGW-w64 toolchain (Arch: mingw-w64-gcc;
# Debian/Ubuntu: g++-mingw-w64-x86-64-posix).
#
# Dumper-7 is written against MSVC. linux-build.patch carries the few changes standard C++ needs
# (casts, a missing typename, a shadowed template parameter, an MSVC-only std::exception constructor).
# Everything else is handled here without touching its sources: case-insensitive include names,
# MSVC-style template parsing, and a fully static runtime so the DLL loads without extra files.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
WORK="$ROOT/linux/out/dumper7"
REF="${DUMPER7_REF:-2c1eae303f3d775f6f59aee6e95990d9ce2e1966}"

for tool in clang++ cmake ninja x86_64-w64-mingw32-gcc x86_64-w64-mingw32-strip; do
    command -v "$tool" >/dev/null || { echo "missing: $tool" >&2; exit 1; }
done

mkdir -p "$WORK"
if [ ! -d "$WORK/src/.git" ]; then
    git clone --quiet https://github.com/Encryqed/Dumper-7 "$WORK/src"
fi
git -C "$WORK/src" fetch --quiet origin
git -C "$WORK/src" checkout --quiet --force "$REF"
git -C "$WORK/src" clean -fdq
git -C "$WORK/src" apply "$HERE/linux-build.patch"

# CMakeLists.txt includes cmake/common.cmake, the file is Common.cmake (fine on Windows only)
ln -sf Common.cmake "$WORK/src/cmake/common.cmake"

# Windows headers spelled the MSVC way (Windows.h) forward to MinGW's lowercase names
mkdir -p "$WORK/include"
for header in $(grep -rhoE '#include\s*<[A-Za-z0-9_]+\.h>' "$WORK/src/Dumper" | sed -E 's/.*<(.*)>/\1/' | sort -u); do
    lower="$(echo "$header" | tr '[:upper:]' '[:lower:]')"
    if [ "$header" != "$lower" ]; then
        echo "#include <$lower>" > "$WORK/include/$header"
    fi
done

# Things MSVC provides implicitly
cat > "$WORK/include/msvc-compat.h" <<'EOF'
#pragma once
#ifdef __cplusplus
#include <cstring>
#include <cstdint>
#include <algorithm>
// windows.h gives MSVC max/min; the sources call them unqualified
template <class A, class B> constexpr auto max(A a, B b) { return a > b ? a : b; }
template <class A, class B> constexpr auto min(A a, B b) { return a < b ? a : b; }
#endif
EOF

# Dumper-7's CMake assumes clang-cl against the MSVC headers (-fms-compatibility, C++20). With MinGW
# headers and libstdc++ clang has to stay in GNU mode; MS extensions and delayed template parsing cover
# the MSVC-isms, and C++23 covers "deducing this", which MSVC accepts in C++20 mode.
cat > "$WORK/clang++" <<'EOF'
#!/bin/sh
exec clang++ "$@" -fno-ms-compatibility -U_MSC_VER -U_MSC_FULL_VER -std=c++23
EOF
chmod +x "$WORK/clang++"

cat > "$WORK/toolchain.cmake" <<EOF
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)
set(CMAKE_C_COMPILER clang)
set(CMAKE_CXX_COMPILER $WORK/clang++)
set(CMAKE_C_COMPILER_TARGET x86_64-w64-mingw32)
set(CMAKE_CXX_COMPILER_TARGET x86_64-w64-mingw32)
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)
set(CMAKE_FIND_ROOT_PATH /usr/x86_64-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
EOF

cmake -S "$WORK/src" -B "$WORK/build" -G Ninja --fresh \
    -DCMAKE_TOOLCHAIN_FILE="$WORK/toolchain.cmake" -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_CXX_FLAGS="-I$WORK/include -include $WORK/include/msvc-compat.h -fms-extensions -fdelayed-template-parsing -w" \
    -DCMAKE_C_FLAGS="-I$WORK/include -w" \
    -DCMAKE_SHARED_LINKER_FLAGS="-static -static-libgcc -static-libstdc++ -pthread" > "$WORK/configure.log"

# GNU ld warns about duplicate sections when clang and gcc built objects meet; harmless
cmake --build "$WORK/build" -j"$(nproc)" > "$WORK/build.log" 2>&1 || { grep -E 'error' "$WORK/build.log" | grep -v 'duplicate section' >&2; exit 1; }

x86_64-w64-mingw32-strip -o "$ROOT/linux/out/Dumper-7.dll" "$WORK/build/bin/libDumper-7.dll"

# Only system DLLs may be imported, anything else would keep the DLL from loading in the game
if x86_64-w64-mingw32-objdump -p "$ROOT/linux/out/Dumper-7.dll" | grep 'DLL Name' | grep -viE 'kernel32|user32|api-ms-win-crt'; then
    echo "Dumper-7.dll depends on non-system DLLs (listed above)" >&2
    exit 1
fi

echo "Built $ROOT/linux/out/Dumper-7.dll (Dumper-7 ${REF:0:7})"
