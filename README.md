# OpenPsPipeJack

## Build Instruction

```
git clone https://github.com/e-fin/OpenPsPipeJack.git

git remote add upstream https://github.com/PowerShell/PowerShell.git

git fetch upstream --tags

import-module .\build.psm1

install-dotnet

Start-PSBuild -Clean -PSModuleRestore -UseNuGetOrg

dotnet publish .\src\PsPipeJack\PsPipeJack.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```
