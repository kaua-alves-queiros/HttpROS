using Spectre.Console;
using HttpROS.Models;

namespace HttpROS.CLI.Commands;

public class HelpCommand
{
    public void ShowHelp(string mode, string filter = "")
    {
        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Cmd", "Desc");
        var rows = new List<(string cmd, string desc)>();
        
        if (mode == "view")
        {
            rows.Add(("clear", "Clear screen"));
            rows.Add(("show [[arg]]", "Display info"));
            rows.Add(("configure", "Enter config mode"));
            rows.Add(("monitor logs", "Real-time log streaming"));
        }
        else if (mode == "config")
        {
            rows.Add(("proxy [[domain]]", "Configure proxy"));
            rows.Add(("static [[domain]]", "Configure static"));
            rows.Add(("redirect [[domain]]", "Configure redirect"));
            rows.Add(("backup", "Backup all"));
            rows.Add(("restore [[ts]]", "Restore backup"));
            rows.Add(("exit", "Exit session"));
            rows.Add(("return", "Back to operational mode"));
            rows.Add(("top", "Back to operational mode"));
        }
        else if (mode == "route-config")
        {
            rows.Add(("target [[v]]", "Set main target"));
            rows.Add(("balancer [[m/u]]", "Configure load balancer"));
            rows.Add(("ssl [[e/d]]", "Toggle SSL"));
            rows.Add(("gzip [[e/d]]", "Toggle Gzip"));
            rows.Add(("websockets [[e/d]]", "Toggle Websockets"));
            rows.Add(("cors [[e/d]]", "Toggle CORS"));
            rows.Add(("auth [[u/p]]", "Set Basic Auth"));
            rows.Add(("ip [[sub]]", "Configure IP filtering"));
            rows.Add(("rate-limit [[v]]", "Set rate limit"));
            rows.Add(("error-page [[c]] [[p]]", "Custom error page"));
            rows.Add(("no [[cmd]]", "Remove/Disable feature"));
            rows.Add(("exit", "Back to global config"));
            rows.Add(("return", "Back to global config"));
            rows.Add(("top", "Back to home"));
        }
        else if (mode == "balancer-config")
        {
            rows.Add(("method [[m]]", "round-robin, least-conn, ip-hash"));
            rows.Add(("sticky [[e/d]]", "Enable/Disable session persistence"));
            rows.Add(("upstream [[ip]]", "Add backend server IP"));
            rows.Add(("health-check [[sub]]", "Configure backend monitoring"));
            rows.Add(("no upstream [[ip]]", "Remove backend server IP"));
            rows.Add(("exit", "Back to route config"));
            rows.Add(("return", "Back to global config"));
            rows.Add(("top", "Back to home"));
        }
        else if (mode == "error-page-config")
        {
            rows.Add(("[[code]] [[file]]", "Set custom page for HTTP code"));
            rows.Add(("no [[code]]", "Remove custom page"));
            rows.Add(("exit", "Back to route config"));
            rows.Add(("return", "Back to global config"));
            rows.Add(("top", "Back to home"));
        }

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(filter) || row.cmd.StartsWith(filter.ToLower()))
            {
                table.AddRow(row.cmd, row.desc);
            }
        }
        
        if (table.Rows.Count > 0)
            AnsiConsole.Write(table);
    }

    public void HandleHelpContextual(string currentLine, string mode)
    {
        var parts = currentLine.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool endsWithSpace = currentLine.EndsWith(" ");

        if (parts.Length == 0)
        {
            ShowHelp(mode);
            return;
        }

        if (!endsWithSpace && parts.Length == 1)
        {
            ShowHelp(mode, parts[0].Trim().ToLower());
            return;
        }

        string cmd = parts[0];

        if (cmd == "show")
        {
            if (parts.Length == 1 || (parts.Length == 2 && !endsWithSpace))
            {
                var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Arg", "Desc");
                table.AddRow("routes", "Summary of all routes")
                     .AddRow("proxy [[domain]]", "Proxy details")
                     .AddRow("static [[domain]]", "Static details")
                     .AddRow("redirect [[domain]]", "Redirect details")
                     .AddRow("status", "System health")
                     .AddRow("version", "Display system version");
                AnsiConsole.Write(table);
            }
            else
            {
                Console.WriteLine("  <cr>  Execute show command");
            }
        }
        else if (cmd == "monitor" && mode == "view")
        {
            if (parts.Length == 1 || (parts.Length == 2 && !endsWithSpace))
                Console.WriteLine("  logs    Real-time log streaming");
            else
                Console.WriteLine("  <cr>  Start log monitoring");
        }
        else if (cmd == "configure" && mode == "view")
        {
             Console.WriteLine("  <cr>  Enter configuration mode");
        }
        else if (cmd == "no" && mode == "route-config")
        {
            if (parts.Length > 1 && parts[1] == "balancer")
            {
                Console.WriteLine("  upstream <ip>    Remove backend server IP");
                Console.WriteLine("  sticky          Disable sticky session");
            }
            else if (parts.Length > 1 && parts[1] == "ip")
            {
                Console.WriteLine("  whitelist <ip>   Remove allowed IP");
                Console.WriteLine("  blacklist <ip>   Remove blocked IP");
            }
            else
            {
                var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Cmd", "Desc");
                table.AddRow("target", "Remove target")
                     .AddRow("ssl", "Disable SSL")
                     .AddRow("gzip", "Disable Gzip")
                     .AddRow("auth", "Remove Basic Auth")
                     .AddRow("ip", "Remove IP filtering sub-settings")
                     .AddRow("balancer", "Remove balancer settings")
                     .AddRow("rate-limit", "Remove limit");
                AnsiConsole.Write(table);
            }
        }
        else if (mode == "config" && (cmd == "proxy" || cmd == "static" || cmd == "redirect"))
        {
            if (parts.Length >= 2)
            {
                Console.WriteLine("  <cr>  Enter route configuration mode");
            }
            else
            {
                Console.WriteLine("  <domain>  Target domain name");
            }
        }
        else if (mode == "config" && cmd == "restore")
        {
             Console.WriteLine("  <ts>  Timestamp of the backup to restore");
        }
        else if (mode == "route-config")
        {
            switch (cmd)
            {
                case "target": Console.WriteLine("  <v>  Target IP:Port or Path"); break;
                case "ssl": 
                    Console.WriteLine("  lets-encrypt      Automatic certificate via Let's Encrypt");
                    Console.WriteLine("  manual <name>     Use local certificate from /Data/certs/manual/");
                    break;
                case "auth": Console.WriteLine("  <user> <pass>   Set login credentials"); break;
                case "ip":
                    var ipTable = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Sub", "Desc");
                    ipTable.AddRow("mode whitelist", "Default block (only allow whitelist)")
                           .AddRow("mode blacklist", "Default allow (only block blacklist)")
                           .AddRow("whitelist <ip>", "Add allowed IP address")
                           .AddRow("blacklist <ip>", "Add blocked IP address");
                    AnsiConsole.Write(ipTable);
                    break;
                case "rate-limit": Console.WriteLine("  <v>             Set limit (e.g. 10r/s)"); break;
                case "balancer": Console.WriteLine("  (Enter sub-mode)  Configure load balancing methods and nodes"); break;
                case "error-page": Console.WriteLine("  (Enter sub-mode)  Configure custom error pages"); break;
                case "save": Console.WriteLine("  <cr>  Save and exit to global config"); break;
                default: Console.WriteLine("  <cr>  Execute command"); break;
            }
        }
        else if (mode == "balancer-config")
        {
            switch (cmd)
            {
                case "method": 
                    Console.WriteLine("  round-robin    Sequential distribution");
                    Console.WriteLine("  least-conn     Sends to node with fewest active connections");
                    Console.WriteLine("  ip-hash        Sticky sessions based on client IP");
                    break;
                case "sticky": Console.WriteLine("  enable/disable  Toggle session persistence"); break;
                case "upstream": Console.WriteLine("  <ip:port>      Add backend server node"); break;
                case "health-check":
                    var hcTable = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Sub", "Desc");
                    hcTable.AddRow("interval <sec>", "Check interval (default 30s)")
                           .AddRow("timeout <sec>", "Check timeout (default 5s)")
                           .AddRow("path <url>", "Health endpoint path (default /)");
                    AnsiConsole.Write(hcTable);
                    break;
                case "no":
                    if (parts.Length > 1 && parts[1] == "upstream") Console.WriteLine("  <ip:port>      Remove backend server node");
                    else if (parts.Length > 1 && parts[1] == "health-check") Console.WriteLine("  <cr>  Disable health monitoring");
                    else Console.WriteLine("  upstream <ip>   Remove node\n  sticky          Disable persistence\n  health-check    Disable monitoring");
                    break;
                default: Console.WriteLine("  <cr>  Execute command"); break;
            }
        }
        else if (mode == "error-page-config")
        {
            if (cmd == "no") Console.WriteLine("  <code>        Remove custom page for specific HTTP status");
            else Console.WriteLine("  <file>        Set custom HTML page for this code");
        }
        else
        {
            Console.WriteLine("  <cr>  Execute command");
        }
    }
}
