$ErrorActionPreference = 'Stop'
dotnet restore
dotnet build -c Debug --no-restore
dotnet test -c Debug --no-build
