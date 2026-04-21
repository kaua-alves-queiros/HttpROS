using Spectre.Console;
using HttpROS.Models;

namespace HttpROS.Commands;

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
        }
        else if (mode == "config")
        {
            rows.Add(("proxy [[domain]]", "Configure proxy"));
            rows.Add(("static [[domain]]", "Configure static"));
            rows.Add(("redirect [[domain]]", "Configure redirect"));
            rows.Add(("backup", "Backup all"));
            rows.Add(("restore", "Restore backup"));
            rows.Add(("exit", "Back"));
            rows.Add(("top", "Back to home"));
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
            rows.Add(("ip-filter [[m]]", "Set filter mode"));
            rows.Add(("whitelist [[ip]]", "Allow IP"));
            rows.Add(("blacklist [[ip]]", "Block IP"));
            rows.Add(("rate-limit [[v]]", "Set rate limit"));
            rows.Add(("error-page [[c]] [[p]]", "Custom error page"));
            rows.Add(("no [[cmd]]", "Remove/Disable feature"));
            rows.Add(("save", "Save and exit"));
            rows.Add(("top", "Back to home"));
        }
        else if (mode == "balancer-config")
        {
            rows.Add(("method [[m]]", "round-robin, least-conn, ip-hash"));
            rows.Add(("sticky [[e/d]]", "Enable/Disable session persistence"));
            rows.Add(("upstream [[ip]]", "Add backend server IP"));
            rows.Add(("no upstream [[ip]]", "Remove backend server IP"));
            rows.Add(("exit", "Back to route config"));
            rows.Add(("top", "Back to home"));
        }
        else if (mode == "error-page-config")
        {
            rows.Add(("[[code]] [[file]]", "Set custom page for HTTP code"));
            rows.Add(("no [[code]]", "Remove custom page"));
            rows.Add(("exit", "Back to route config"));
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
            var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Arg", "Desc");
            table.AddRow("routes", "Summary of all routes")
                 .AddRow("proxy [[domain]]", "Proxy details")
                 .AddRow("static [[domain]]", "Static details")
                 .AddRow("redirect [[domain]]", "Redirect details")
                 .AddRow("status", "System health")
                 .AddRow("version", "Display system version");
            AnsiConsole.Write(table);
        }
        else if (cmd == "no" && mode == "route-config")
        {
            if (parts.Length > 1 && parts[1] == "balancer")
            {
                Console.WriteLine("  upstream <ip>    Remove backend server IP");
                Console.WriteLine("  sticky          Disable sticky session");
            }
            else
            {
                var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Cmd", "Desc");
                table.AddRow("target", "Remove target")
                     .AddRow("ssl", "Disable SSL")
                     .AddRow("gzip", "Disable Gzip")
                     .AddRow("auth", "Remove Basic Auth")
                     .AddRow("whitelist [[ip]]", "Remove allowed IP")
                     .AddRow("blacklist [[ip]]", "Remove blocked IP")
                     .AddRow("balancer", "Remove balancer settings")
                     .AddRow("rate-limit", "Remove limit");
                AnsiConsole.Write(table);
            }
        }
        else if (cmd == "balancer" && mode == "route-config")
        {
            Console.WriteLine("  (Enter sub-mode)  Configure load balancing methods and nodes");
        }
        else if (mode == "route-config")
        {
            switch (cmd)
            {
                case "target": Console.WriteLine("  <v>  Target IP:Port or Path"); break;
                case "ssl": 
                    Console.WriteLine("  lets-encrypt      Automatic certificate via Let's Encrypt");
                    Console.WriteLine("  manual <name>     Use local certificate from /certs/manual/");
                    break;
                case "auth": Console.WriteLine("  <user> <pass>   Set login credentials"); break;
                case "whitelist": Console.WriteLine("  <ip>            Add allowed IP address"); break;
                case "blacklist": Console.WriteLine("  <ip>            Add blocked IP address"); break;
                case "rate-limit": Console.WriteLine("  <v>             Set limit (e.g. 10r/s)"); break;
                case "ip-filter": 
                    Console.WriteLine("  mode whitelist  Default block (only allow whitelist)");
                    Console.WriteLine("  mode blacklist  Default allow (only block blacklist)");
                    break;
                case "error-page":
                    Console.WriteLine("  (Enter sub-mode)  Configure custom error pages");
                    break;
                default: ShowHelp(mode); break;
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
                case "no":
                    if (parts.Length > 1 && parts[1] == "upstream") Console.WriteLine("  <ip:port>      Remove backend server node");
                    else Console.WriteLine("  upstream <ip>   Remove node\n  sticky          Disable persistence");
                    break;
                default: ShowHelp(mode); break;
            }
        }
        else if (mode == "error-page-config")
        {
            if (cmd == "no") Console.WriteLine("  <code>        Remove custom page for specific HTTP status");
            else Console.WriteLine("  <file>        Set custom HTML page for this code");
        }
        else
        {
            ShowHelp(mode);
        }
    }
}
