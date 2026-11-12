#!/bin/bash
# 빌드: ./build.sh [Linux|Win|Android] → Builds/Linux/EvoTank.x86_64 · Builds/Windows/EvoTank.exe · Builds/Android/EvoTank.apk
[ -z "$HEAVY" ] && exec heavy "$0" "$@"
M=${1:-Linux}; P=$(cd "$(dirname "$0")" && pwd); L=$P/Logs/build-$M.log
T=Linux64; [ "$M" = Win ] && T=Win64; [ "$M" = Android ] && T=Android
# 리눅스 IL2CPP: Unity가 받은 clang 15·ld.lld는 libncurses.so.6·libxml2.so.2를 찾는데 아치(CachyOS)엔 libncursesw.so.6·libxml2.so.16만 있다 → 이름만 이어 준다
# (ld.lld는 ELF 링크에 libxml2를 안 쓴다: 윈도우 매니페스트용. 'no version information' 경고는 무해)
C=~/.local/lib/unity-compat; mkdir -p $C
for p in libncurses.so.6:libncursesw.so.6 libxml2.so.2:libxml2.so.16; do [ -e /usr/lib/${p%%:*} ] || [ -e $C/${p%%:*} ] || ln -s /usr/lib/${p##*:} $C/${p%%:*}; done
export LD_LIBRARY_PATH=$C${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}
~/Unity/Hub/Editor/6000.6.6f1/Editor/Unity -batchmode -quit -projectPath "$P" -buildTarget $T -executeMethod EvoBuild.$M -logFile "$L"
r=$?; [ $r = 0 ] && [ "$M" = Linux ] && rsync -a --delete --exclude icon.png "$P/Builds/Linux/" ~/Applications/EvoTank/ && echo "앱 메뉴 진화 수조 갱신"   # 런처용 설치본
grep -E "error CS|\[Build\]|Shader error|Exception" "$L" | sort -u | head -40; echo "exit=$r"
