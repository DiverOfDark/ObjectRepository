@echo off

pushd src

dotnet restore
dotnet build -c Release
dotnet test OutCode.EscapeTeams.ObjectRepository.Tests
dotnet pack -c Release

popd