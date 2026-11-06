#!/bin/sh
# 사용: ./run.sh <시나리오> <일수> <시드> [보고 간격]
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
D=$HOME/Unity/Hub/Editor/6000.6.6f1/Editor/Data/DotNetSdk
exec "$D/dotnet" "$(dirname "$0")/bin/Release/net8.0/SimTest.dll" "$@"
