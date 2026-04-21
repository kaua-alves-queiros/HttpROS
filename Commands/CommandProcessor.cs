using Spectre.Console;
using HttpROS.Models;
using HttpROS.Services;

namespace HttpROS.Commands;

public class CommandProcessor
{
    private readonly StorageService _storage;
    private readonly ShowCommand _showCommand;
    private readonly TargetCommand _targetCommand;
    private readonly SslCommand _sslCommand;
    private readonly BackupCommand _backupCommand;

    public CommandProcessor(StorageService storage)
    {
        _storage = storage;
        _showCommand = new ShowCommand(storage);
        _targetCommand = new TargetCommand(storage);
        _sslCommand = new SslCommand(storage);
        _backupCommand = new BackupCommand();
    }

    public void HandleShowCommand(string[] parts, RouteConfig? activeRoute = null)
    {
        if (parts.Length == 1 && activeRoute != null)
        {
            _showCommand.ShowRunningConfig(activeRoute);
        }
        else
        {
            _showCommand.Execute(parts.Skip(1).ToArray());
        }
    }

    public void HandleBackup()
    {
        _backupCommand.Execute();
    }

    public void HandleRestore()
    {
        AnsiConsole.MarkupLine("[yellow]Restoring configurations from backup... Done.[/]");
    }

    public void HandleRouteConfig(string command, string[] args, RouteConfig activeRoute, bool isNo)
    {
        switch (command)
        {
            case "target":
                if (isNo) { activeRoute.Target = ""; _storage.SaveRoute(activeRoute); AnsiConsole.MarkupLine("[grey]Target removido.[/]"); }
                else _targetCommand.Execute(args, activeRoute);
                break;
            case "upstream":
                if (isNo && args.Length >= 1) { activeRoute.Upstreams.Remove(args[0]); }
                else if (!isNo && args.Length >= 1) { activeRoute.Upstreams.Add(args[0]); }
                _storage.SaveRoute(activeRoute);
                break;
            case "ssl":
                _sslCommand.Execute(args, activeRoute, isNo);
                break;
            case "gzip":
                activeRoute.Features.Gzip = !isNo;
                _storage.SaveRoute(activeRoute);
                AnsiConsole.MarkupLine($"[grey]Gzip {(activeRoute.Features.Gzip ? "ON" : "OFF")}[/]");
                break;
            case "websockets":
                activeRoute.Features.Websockets = !isNo;
                _storage.SaveRoute(activeRoute);
                break;
            case "cors":
                activeRoute.Features.Cors = !isNo;
                _storage.SaveRoute(activeRoute);
                break;
            case "auth":
                if (isNo) { activeRoute.Features.BasicAuth = null; }
                else if (args.Length >= 2) { activeRoute.Features.BasicAuth = new BasicAuth { User = args[0], Pass = args[1] }; }
                _storage.SaveRoute(activeRoute);
                break;
            case "ip-filter":
                if (args.Length >= 2 && args[0] == "mode")
                {
                    string mode = args[1].ToLower();
                    if (mode == "whitelist" || mode == "blacklist")
                    {
                        activeRoute.Features.IpFilter.Mode = mode;
                        AnsiConsole.MarkupLine($"[grey]IP Filter Mode definido para: {mode}[/]");
                    }
                }
                _storage.SaveRoute(activeRoute);
                break;
            case "whitelist":
                if (isNo && args.Length >= 1) { activeRoute.Features.IpFilter.Whitelist.Remove(args[0]); }
                else if (!isNo && args.Length >= 1) { activeRoute.Features.IpFilter.Whitelist.Add(args[0]); }
                _storage.SaveRoute(activeRoute);
                break;
            case "blacklist":
                if (isNo && args.Length >= 1) { activeRoute.Features.IpFilter.Blacklist.Remove(args[0]); }
                else if (!isNo && args.Length >= 1) { activeRoute.Features.IpFilter.Blacklist.Add(args[0]); }
                _storage.SaveRoute(activeRoute);
                break;
            case "rate-limit":
                if (isNo) { activeRoute.Features.RateLimit = null; }
                else if (args.Length >= 1) { activeRoute.Features.RateLimit = args[0]; }
                _storage.SaveRoute(activeRoute);
                break;
            case "error-page":
                if (isNo && args.Length >= 1) { activeRoute.Features.CustomErrorPages.Remove(args[0]); }
                else if (!isNo && args.Length >= 2) { activeRoute.Features.CustomErrorPages[args[0]] = args[1]; }
                _storage.SaveRoute(activeRoute);
                break;
        }
    }
}
