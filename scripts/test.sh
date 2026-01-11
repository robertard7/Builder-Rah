#!/usr/bin/env bash
set -euo pipefail

dotnet restore
dotnet build -c Debug --no-restore
dotnet test -c Debug --no-build
