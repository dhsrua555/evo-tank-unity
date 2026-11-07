#!/bin/sh
# 사용: ./run.sh <일수> <시드> [보고 간격] [돌연변이율] [csv]
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
D=$HOME/Unity/Hub/Editor/6000.6.6f1/Editor/Data/DotNetSdk
X="$D/dotnet"; [ -x "$X" ] || X=dotnet
exec "$X" "$(dirname "$0")/bin/Release/net8.0/LifeTest.dll" "$@"
