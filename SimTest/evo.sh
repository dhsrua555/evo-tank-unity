#!/bin/bash
# 진화 속도 비교: ./evo.sh <태그> <시나리오> <일수> <시드…> → <태그>-<시드>.log (한 번에 하나씩, heavy)
[ -z "$HEAVY" ] && exec heavy "$0" "$@"
cd "$(dirname "$0")"; T=$1 S=$2 D=$3; shift 3
D0=$HOME/Unity/Hub/Editor/6000.6.6f1/Editor/Data/DotNetSdk; "$D0/dotnet" build -c Release -v q 2>&1 | grep -E " error |rror\(s\)" | head -5
for s in "$@"; do ./run.sh $S $D $s $((D/10)) > $T-$s.log 2>&1; echo "== $T seed $s"; grep -E "처음으로|ALL EXTINCT" $T-$s.log | head -8; grep "^d " $T-$s.log | tail -1 | cut -c1-120; grep "^# species" $T-$s.log; grep "^d " $T-$s.log | awk '$10 > 1e7 {print "BLOWUP at d " $2; exit}'; echo "wall $(grep -o 'wall [0-9.]*s' $T-$s.log)"; done
