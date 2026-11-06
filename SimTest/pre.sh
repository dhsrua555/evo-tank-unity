#!/bin/bash
# 먹이 사슬 순서 탐침: ./pre.sh <태그> <바탕 일수> <PRE 일수> <탐침 일수> [시드]
[ -z "$HEAVY" ] && exec heavy "$0" "$@"
cd "$(dirname "$0")"; "$HOME/Unity/Hub/Editor/6000.6.6f1/Editor/Data/DotNetSdk/dotnet" build -c Release -v q 2>&1 | grep -E " error " | head
PATHP=$4 PRE=$3 TL=1 ./run.sh fish $2 ${5:-1} > $1.log 2>&1; grep -E "^# pre|^  [a-z]" $1.log | cut -c1-200; grep -E "t +$(( $4 / 10 * 10 - 10 ))d" $1.log | cut -c1-300
