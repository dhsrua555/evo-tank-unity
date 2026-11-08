#!/bin/bash
# 화면 점검: ./shots.sh <태그> [-gallery] → Logs/shots_<태그>/*.png + 로그 요약
[ -z "$HEAVY" ] && exec heavy "$0" "$@"
P=$(cd "$(dirname "$0")" && pwd); O=$P/Logs/shots_$1; mkdir -p "$O"; shift
prime-run "$P/Builds/Linux/EvoTank.x86_64" -screen-width ${SW:-2560} -screen-height ${SH:-1600} -screen-fullscreen ${FS:-1} -shots "$O" "$@" -logFile "$O/log.txt" >/dev/null 2>&1
grep -E "\[EvoTank\]|Exception|NullRef" "$O/log.txt" | sort -u | cut -c1-240 | head -20; ls "$O" | tr '\n' ' '
