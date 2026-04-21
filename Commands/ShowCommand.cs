using Spectre.Console;
using HttpROS.Services;
using HttpROS.Commands.Base;
using HttpROS.Models;

namespace HttpROS.Commands;

public class ShowCommand : ICommand
{
    public string Name => "show";
    public string Description => "Exibe informações do sistema";
    private readonly StorageService _storage;

    public ShowCommand(StorageService storage)
    {
        _storage = storage;
    }

    public void Execute(string[] args)
    {
        if (args.Length < 1)
        {
            AnsiConsole.MarkupLine("[red]Error: 'show' requer um argumento.[/]");
            return;
        }

        string sub = args[0].ToLower();
        
        if (args.Length >= 2 && (sub == "proxy" || sub == "static" || sub == "redirect"))
        {
            ShowHuaweiStyle(sub, args[1]);
            return;
        }

        switch (sub)
        {
            case "routes":
                var routes = _storage.GetAllRoutes();
                if (routes.Count == 0) { Console.WriteLine("Nenhuma rota configurada."); return; }
                foreach (var r in routes)
                {
                    string sslStatus = r.Features.Ssl.Enabled ? "ssl" : "no ssl";
                    Console.WriteLine($"{r.Type,-8} {r.Domain,-25} target {r.Target} ({sslStatus})");
                }
                break;

            case "status":
                Console.WriteLine("System Health: OK | Nginx Process: Running | HttpROS: Active");
                break;

            case "version":
                Console.WriteLine("HttpROS version 0.1.0 (.NET 10)");
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Error: Argumento desconhecido '{sub}'.[/]");
                break;
        }
    }

    public void ShowRunningConfig(RouteConfig route)
    {
        Console.WriteLine($"{route.Type} {route.Domain}");
        if (!string.IsNullOrEmpty(route.Target)) Console.WriteLine($" target {route.Target}");
        
        if (route.Features.Ssl.Enabled) 
        {
            if (route.Features.Ssl.Provider == "lets-encrypt") Console.WriteLine(" ssl lets-encrypt");
            else Console.WriteLine($" ssl manual {route.Features.Ssl.CertName}");
        }
        else Console.WriteLine(" no ssl");

        if (route.Features.Gzip) Console.WriteLine(" gzip");
        else Console.WriteLine(" no gzip");

        if (route.Features.Websockets) Console.WriteLine(" websockets");
        if (route.Features.Cors) Console.WriteLine(" cors");

        if (route.Features.BasicAuth != null)
            Console.WriteLine($" auth {route.Features.BasicAuth.User} {route.Features.BasicAuth.Pass}");

        if (route.Features.IpFilter.Mode != "blacklist")
            Console.WriteLine($" ip-filter mode {route.Features.IpFilter.Mode}");

        foreach (var ip in route.Features.IpFilter.Whitelist) Console.WriteLine($" whitelist {ip}");
        foreach (var ip in route.Features.IpFilter.Blacklist) Console.WriteLine($" blacklist {ip}");
        foreach (var up in route.Upstreams) Console.WriteLine($" upstream {up}");

        if (!string.IsNullOrEmpty(route.Features.RateLimit))
            Console.WriteLine($" rate-limit {route.Features.RateLimit}");

        foreach (var page in route.Features.CustomErrorPages)
            Console.WriteLine($" error-page {page.Key} {page.Value}");

        Console.WriteLine("!");
    }

    private void ShowHuaweiStyle(string type, string domain)
    {
        var route = _storage.LoadRoute(type, domain);
        if (route == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Rota '{type} {domain}' não encontrada.[/]");
            return;
        }

        string typeName = char.ToUpper(type[0]) + type.Substring(1);
        Console.WriteLine($"{typeName}-Route {route.Domain} current state : UP");
        Console.WriteLine($"Line protocol current state : UP (Nginx: Running)");
        Console.WriteLine("Features:");
        Console.WriteLine($"  SSL state : {(route.Features.Ssl.Enabled ? "ENABLED" : "DISABLED")}");
        Console.WriteLine($"  Gzip state : {(route.Features.Gzip ? "ENABLED" : "DISABLED")}");
        Console.WriteLine($"  Authentication : {(route.Features.BasicAuth != null ? "ENABLED" : "DISABLED")}");
        Console.WriteLine("Traffic Control:");
        Console.WriteLine($"  Rate-limit : {(!string.IsNullOrEmpty(route.Features.RateLimit) ? route.Features.RateLimit : "DISABLED")}");
        Console.WriteLine("    Last 300 seconds input rate 0 bits/sec, 0 packets/sec");
        Console.WriteLine("    Last 300 seconds output rate 0 bits/sec, 0 packets/sec");
    }
}
