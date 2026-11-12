#!/bin/bash
# 빌드: ./build.sh [Linux|Win|Android] → Builds/Linux/EvoTank.x86_64 · Builds/Windows/EvoTank.exe · Builds/Android/EvoTank.apk
[ -z "$HEAVY" ] && exec heavy "$0" "$@"
M=${1:-Linux}; P=$(cd "$(dirname "$0")" && pwd); L=$P/Logs/build-$M.log
T=Linux64; [ "$M" = Win ] && T=Win64; [ "$M" = Android ] && T=Android
~/Unity/Hub/Editor/6000.6.6f1/Editor/Unity -batchmode -quit -projectPath "$P" -buildTarget $T -executeMethod EvoBuild.$M -logFile "$L"
r=$?; [ $r = 0 ] && [ "$M" = Linux ] && rsync -a --delete --exclude icon.png "$P/Builds/Linux/" ~/Applications/EvoTank/ && echo "앱 메뉴 진화 수조 갱신"   # 런처용 설치본
grep -E "error CS|\[Build\]|Shader error|Exception" "$L" | sort -u | head -40; echo "exit=$r"
