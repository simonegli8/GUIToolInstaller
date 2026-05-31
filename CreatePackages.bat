SET PackageVersion=1.0.5
SET Configuration=Release

del nupkg\*.nupkg
del nupkg\*.snupkg

dotnet pack -c %Configuration% -p:Version=%PackageVersion% -p:FileVersion=%PackageVersion% -p:AssemblyVersion=%PackageVersion%