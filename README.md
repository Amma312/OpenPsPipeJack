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
Usage: PsPipeJack.exe <command> [arguments]

Commands:
  --enumerate   (-e)   List all PowerShell named pipes on a remote machine
  --connect     (-c)   Connect to a PowerShell named pipe on a remote machine

──────────────────────────────────────────────────────────────
  --enumerate <server> <domain\user> <password>

  Arguments:
    <server>          [required]  IP address or hostname of the remote machine
    <domain\user>     [required]  Credentials to authenticate with (e.g. CORP\jdoe)
    <password>        [required]  Password for the specified user

  Notes:
    Enumerate always requires explicit credentials. It authenticates
    directly to the remote machine over SMB/IPC$ and will not fall
    back to the current user's access token.

──────────────────────────────────────────────────────────────
  --connect <server> <pipe> [domain\user] [password]

  Arguments:
    <server>          [required]  IP address or hostname of the remote machine
    <pipe>            [required]  Full name of the named pipe to connect to
    <domain\user>     [optional]  Credentials to authenticate with (e.g. CORP\jdoe)
    <password>        [optional]  Password for the specified user

  Notes:
    Connect supports two authentication modes:
      Current token:   Omit domain\user and password. The current user's
                       Windows access token is used automatically via SSPI.
      Credentials:     Supply domain\user and password. An authenticated
                       IPC$ session is established first, then the pipe
                       connection rides that session.

──────────────────────────────────────────────────────────────
Examples:
  PsPipeJack --enumerate 192.168.1.100 CORP\jdoe Passw0rd!
  PsPipeJack --connect   192.168.1.100 PSHost.134214327970160016.3492.DefaultAppDomain.powershell
  PsPipeJack --connect   192.168.1.100 PSHost.134214327970160016.3492.DefaultAppDomain.powershell CORP\jdoe Passw0rd!

```
