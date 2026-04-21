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

    public void HandleShowCommand(string[] parts, string mode, RouteConfig? activeRoute = null)
    {
        if (parts.Length == 1 && activeRoute != null)
        {
            if (mode == "balancer-config") _showCommand.ShowBalancerConfig(activeRoute);
            else if (mode == "error-page-config") _showCommand.ShowErrorPagesConfig(activeRoute);
            else _showCommand.ShowRunningConfig(activeRoute);
        }
        else
        {
            _showCommand.Execute(parts.Skip(1).ToArray());
        }
    }

    public void HandleBackup() => _backupCommand.Execute();
    public void HandleRestore() => AnsiConsole.MarkupLine("[yellow]Restoring configurations from backup... Done.[/]");

    public void HandleRouteConfig(string command, string[] args, RouteConfig activeRoute, bool isNo)
    {
        switch (command)
        {
            case "target":
                if (isNo) { activeRoute.Target = ""; _storage.SaveRoute(activeRoute); AnsiConsole.MarkupLine("[grey]Target removido.[/]"); }
                else _targetCommand.Execute(args, activeRoute);
                break;
            case "upstream":
                if (isNo && args.Length >= 1) activeRoute.Balancer.Upstreams.Remove(args[0]);
                else if (!isNo && args.Length >= 1 && !activeRoute.Balancer.Upstreams.Contains(args[0])) activeRoute.Balancer.Upstreams.Add(args[0]);
                _storage.SaveRoute(activeRoute);
                break;
            case "ssl":
                _sslCommand.Execute(args, activeRoute, isNo);
                break;
            case "gzip":
                activeRoute.Features.Gzip = !isNo;
                _storage.SaveRoute(activeRoute);
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
                if (isNo) activeRoute.Features.BasicAuth = null;
                else if (args.Length >= 2) activeRoute.Features.BasicAuth = new BasicAuth { User = args[0], Pass = args[1] };
                _storage.SaveRoute(activeRoute);
                break;
            case "ip-filter":
                if (args.Length >= 2 && args[0] == "mode")
                {
                    string mode = args[1].ToLower();
                    if (mode == "whitelist" || mode == "blacklist") activeRoute.Features.IpFilter.Mode = mode;
                }
                _storage.SaveRoute(activeRoute);
                break;
            case "whitelist":
                if (isNo && args.Length >= 1) activeRoute.Features.IpFilter.Whitelist.Remove(args[0]);
                else if (!isNo && args.Length >= 1) activeRoute.Features.IpFilter.Whitelist.Add(args[0]);
                _storage.SaveRoute(activeRoute);
                break;
            case "blacklist":
                if (isNo && args.Length >= 1) activeRoute.Features.IpFilter.Blacklist.Remove(args[0]);
                else if (!isNo && args.Length >= 1) activeRoute.Features.IpFilter.Blacklist.Add(args[0]);
                _storage.SaveRoute(activeRoute);
                break;
            case "rate-limit":
                activeRoute.Features.RateLimit = isNo ? null : (args.Length >= 1 ? args[0] : null);
                _storage.SaveRoute(activeRoute);
                break;
            case "error-page":
                if (isNo && args.Length >= 1) activeRoute.Features.CustomErrorPages.Remove(args[0]);
                else if (!isNo && args.Length >= 2) 
                { 
                    string code = args[0], page = args[1];
                    if (!page.EndsWith(".html")) page += ".html";
                    activeRoute.Features.CustomErrorPages[code] = page; 
                }
                _storage.SaveRoute(activeRoute);
                break;
        }
    }

    public void HandleBalancerConfig(string command, string[] args, RouteConfig activeRoute, bool isNo)
    {
        switch (command)
        {
            case "method":
                if (args.Length >= 1)
                {
                    string m = args[0].ToLower();
                    if (m == "round-robin" || m == "least-conn" || m == "ip-hash") activeRoute.Balancer.Method = m;
                }
                break;
            case "sticky":
                activeRoute.Balancer.Sticky = !isNo;
                break;
            case "upstream":
                if (isNo && args.Length >= 1) activeRoute.Balancer.Upstreams.Remove(args[0]);
                else if (!isNo && args.Length >= 1 && !activeRoute.Balancer.Upstreams.Contains(args[0])) activeRoute.Balancer.Upstreams.Add(args[0]);
                break;
        }
        _storage.SaveRoute(activeRoute);
    }

    public void HandleErrorPageConfig(string command, string[] args, RouteConfig activeRoute, bool isNo)
    {
        // Inside error-page mode, 'command' is the status code (e.g. 404)
        if (isNo)
        {
            activeRoute.Features.CustomErrorPages.Remove(command);
        }
        else if (args.Length >= 1)
        {
            string page = args[0];
            if (!page.EndsWith(".html")) page += ".html";
            activeRoute.Features.CustomErrorPages[command] = page;
        }
        _storage.SaveRoute(activeRoute);
    }
}
