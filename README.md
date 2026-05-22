# OpenPsPipeJack

please be patient im a C programmer, still learning C#...

I read about the OutFlank PsPipeJack tool, the name kinda gave away what it was doing, so I started messing around with the PowerShell repo (https://github.com/PowerShell/PowerShell/) and figured out how they were doing it.

Its pretty straight forward, in System.Management.Automation, the hostname is hardcoded to be localhost, some small modifications and I can now connect to PowerShell named pipes on remote hosts.

This requires local admin access on the remote host.

## Build Instruction

```
git clone https://github.com/e-fin/OpenPsPipeJack.git

git remote add upstream https://github.com/PowerShell/PowerShell.git

git fetch upstream --tags

import-module .\build.psm1

install-dotnet

Start-PSBuild -Clean -PSModuleRestore -UseNuGetOrg

dotnet publish .\src\PsPipeJack\PsPipeJack.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

# Executable is at .\src\PsPipeJack\bin\Release\net11.0\win-x64\publish\PsPipeJack.exe
```

## Usage

```

```
