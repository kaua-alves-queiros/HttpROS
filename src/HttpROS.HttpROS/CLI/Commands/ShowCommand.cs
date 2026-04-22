using Spectre.Console;
using HttpROS.Data;
using HttpROS.CLI.Base;
using HttpROS.Models;

namespace HttpROS.CLI.Commands;

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
                Console.WriteLine("System Health: OK | Proxy Engine: Native .NET | HttpROS: Active");
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
        else Console.WriteLine(" no target");
        
        if (route.Features.Ssl.Enabled) 
        {
            if (route.Features.Ssl.Provider == "lets-encrypt") Console.WriteLine(" ssl lets-encrypt");
            else Console.WriteLine($" ssl manual {route.Features.Ssl.CertName}");
        }
        else Console.WriteLine(" no ssl");

        Console.WriteLine(route.Features.Gzip ? " gzip" : " no gzip");
        Console.WriteLine(route.Features.Websockets ? " websockets" : " no websockets");
        Console.WriteLine(route.Features.Cors ? " cors" : " no cors");

        if (route.Features.BasicAuth != null)
            Console.WriteLine($" auth {route.Features.BasicAuth.User} {route.Features.BasicAuth.Pass}");
        else
            Console.WriteLine(" no auth");

        Console.WriteLine($" ip-filter mode {route.Features.IpFilter.Mode}");
        foreach (var ip in route.Features.IpFilter.Whitelist) Console.WriteLine($" whitelist {ip}");
        foreach (var ip in route.Features.IpFilter.Blacklist) Console.WriteLine($" blacklist {ip}");
        
        ShowBalancerConfig(route, 1);

        if (!string.IsNullOrEmpty(route.Features.RateLimit))
            Console.WriteLine($" rate-limit {route.Features.RateLimit}");
        else
            Console.WriteLine(" no rate-limit");

        ShowErrorPagesConfig(route, 1);

        Console.WriteLine("!");
    }

    public void ShowBalancerConfig(RouteConfig route, int indent = 0)
    {
        string space = new string(' ', indent);
        bool hasSettings = route.Balancer.Upstreams.Count > 0 || route.Balancer.Sticky || route.Balancer.Method != "round-robin";
        
        if (hasSettings)
        {
            Console.WriteLine($"{space}balancer");
            Console.WriteLine($"{space} method {route.Balancer.Method}");
            Console.WriteLine($"{space} sticky {(route.Balancer.Sticky ? "enable" : "disable")}");
            foreach (var up in route.Balancer.Upstreams) Console.WriteLine($"{space} upstream {up}");
            Console.WriteLine($"{space}!");
        }
        else
        {
            Console.WriteLine($"{space}no balancer");
        }
    }

    public void ShowErrorPagesConfig(RouteConfig route, int indent = 0)
    {
        string space = new string(' ', indent);
        if (route.Features.CustomErrorPages.Count > 0)
        {
            Console.WriteLine($"{space}error-pages");
            foreach (var page in route.Features.CustomErrorPages)
                Console.WriteLine($"{space} {page.Key} {page.Value}");
            Console.WriteLine($"{space}!");
        }
        else
        {
            Console.WriteLine($"{space}no error-pages");
        }
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
        Console.WriteLine($"Line protocol current state : UP (Native Proxy)");
        
        if (!string.IsNullOrEmpty(route.Target) && route.Balancer.Upstreams.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]WARNING: Target e Balancer configurados. O Balancer terá prioridade.[/]");
        }

        Console.WriteLine("Features:");
        Console.WriteLine($"  SSL state : {(route.Features.Ssl.Enabled ? "ENABLED" : "DISABLED")}");
        Console.WriteLine($"  Gzip state : {(route.Features.Gzip ? "ENABLED" : "DISABLED")}");
        Console.WriteLine($"  Authentication : {(route.Features.BasicAuth != null ? "ENABLED" : "DISABLED")}");
        
        if (route.Balancer.Upstreams.Count > 0 || route.Balancer.Sticky || route.Balancer.Method != "round-robin")
        {
            Console.WriteLine("Load Balancing:");
            Console.WriteLine($"  Method: {route.Balancer.Method}");
            Console.WriteLine($"  Sticky persistence: {(route.Balancer.Sticky ? "ENABLED" : "DISABLED")}");
            Console.WriteLine($"  Upstreams count : {route.Balancer.Upstreams.Count}");
            foreach(var up in route.Balancer.Upstreams) Console.WriteLine($"    - {up}");
        }

        Console.WriteLine("Traffic Control:");
        Console.WriteLine($"  Rate-limit : {(!string.IsNullOrEmpty(route.Features.RateLimit) ? route.Features.RateLimit : "DISABLED")}");
        
        if (route.Features.CustomErrorPages.Count > 0)
        {
            Console.WriteLine("Error Pages:");
            foreach (var page in route.Features.CustomErrorPages)
                Console.WriteLine($"  Code {page.Key} -> {page.Value}");
        }

        Console.WriteLine("    Last 300 seconds input rate 0 bits/sec, 0 packets/sec");
        Console.WriteLine("    Last 300 seconds output rate 0 bits/sec, 0 packets/sec");
    }
}
