set -euo pipefail
LOG_PATH="$(cygpath -u "$ESTUDIO_GCC_LOG")"
exec > >(tee -a "$LOG_PATH") 2>&1
echo "=== Instalando GCC $(date '+%Y-%m-%d %H:%M:%S') ==="
pacman -Sy --noconfirm msys2-keyring
pacman -Syuu --noconfirm
pacman -Syuu --noconfirm
pacman -S --needed --noconfirm mingw-w64-x86_64-gcc mingw-w64-x86_64-binutils mingw-w64-x86_64-crt-git
/mingw64/bin/gcc.exe --version
echo "=== GCC listo $(date '+%Y-%m-%d %H:%M:%S') ==="
