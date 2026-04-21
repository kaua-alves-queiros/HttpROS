using Spectre.Console;
using HttpROS.Models;

namespace HttpROS.Commands;

public class HelpCommand
{
    public void ShowHelp(string mode)
    {
        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Cmd", "Desc");
        
        if (mode == "view")
        {
            table.AddRow("clear", "Clear screen")
                 .AddRow("show [[arg]]", "Display info")
                 .AddRow("configure", "Enter config mode");
        }
        else if (mode == "config")
        {
            table.AddRow("proxy [[domain]]", "Configure proxy")
                 .AddRow("static [[domain]]", "Configure static")
                 .AddRow("redirect [[domain]]", "Configure redirect")
                 .AddRow("backup", "Backup all")
                 .AddRow("restore", "Restore backup")
                 .AddRow("exit", "Back");
        }
        else if (mode == "route-config")
        {
            table.AddRow("target [[v]]", "Set main target")
                 .AddRow("upstream [[ip]]", "Add backend IP")
                 .AddRow("ssl [[e/d]]", "Toggle SSL")
                 .AddRow("gzip [[e/d]]", "Toggle Gzip")
                 .AddRow("websockets [[e/d]]", "Toggle Websockets")
                 .AddRow("cors [[e/d]]", "Toggle CORS")
                 .AddRow("auth [[u/p]]", "Set Basic Auth")
                 .AddRow("ip-filter [[m]]", "Set filter mode")
                 .AddRow("whitelist [[ip]]", "Allow IP")
                 .AddRow("blacklist [[ip]]", "Block IP")
                 .AddRow("rate-limit [[v]]", "Set rate limit")
                 .AddRow("error-page [[c]] [[p]]", "Custom error page")
                 .AddRow("no [[cmd]]", "Remove/Disable feature")
                 .AddRow("save", "Save and exit");
        }
        
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

        string cmd = parts[0];

        if (cmd == "show")
        {
            var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Arg", "Desc");
            table.AddRow("routes", "Summary of all routes")
                 .AddRow("proxy [[domain]]", "Proxy details")
                 .AddRow("static [[domain]]", "Static details")
                 .AddRow("status", "System health");
            AnsiConsole.Write(table);
        }
        else if (cmd == "no" && mode == "route-config")
        {
            var table = new Table().Border(TableBorder.None).HideHeaders().AddColumns("Cmd", "Desc");
            table.AddRow("target", "Remove target")
                 .AddRow("ssl", "Disable SSL")
                 .AddRow("gzip", "Disable Gzip")
                 .AddRow("auth", "Remove Basic Auth")
                 .AddRow("whitelist [[ip]]", "Remove allowed IP")
                 .AddRow("blacklist [[ip]]", "Remove blocked IP")
                 .AddRow("upstream [[ip]]", "Remove backend IP")
                 .AddRow("rate-limit", "Remove limit");
            AnsiConsole.Write(table);
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
                default: ShowHelp(mode); break;
            }
        }
        else
        {
            ShowHelp(mode);
        }
    }
}
