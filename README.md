# OpenPsPipeJack

please be patient im a C programmer, still learning C#...

I read about the OutFlank PsPipeJack tool, the name kinda gave away what it was doing, so I started messing around with the PowerShell repo (https://github.com/PowerShell/PowerShell/) and figured out how they were doing it.

Its pretty straight forward, in System.Management.Automation, the hostname is hardcoded to be localhost, some small modifications and I can now connect to PowerShell named pipes on remote hosts.

This requires local admin access on the remote host.

## Description of Modifications

The modified files are:

```
src\System.Management.Automation\engine\remoting\common\RemoteSessionNamedPipe.cs
src\System.Management.Automation\engine\remoting\common\RunspaceConnectionInfo.cs
src\System.Management.Automation\engine\remoting\fanin\OutOfProcTransportManager.cs
src\System.Management.Automation\System.Management.Automation.csproj
```

The new project added (PsPipeJack):
```
src\PsPipeJack
```


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
Usage: PsPipeJack <serverName> <pipeName>

Arguments:
  serverName   IP address or hostname of the remote machine
  pipeName     Name of the PowerShell named pipe on the remote machine

Example:
  PsPipeJack 192.168.1.100 PSHost.134214327377452265.6880.DefaultAppDomain.powershell

```
