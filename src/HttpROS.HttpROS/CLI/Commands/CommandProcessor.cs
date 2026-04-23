using Spectre.Console;
using HttpROS.Models;
using HttpROS.Data;
using HttpROS.Engine;

namespace HttpROS.CLI.Commands;

public class CommandProcessor
{
    private readonly StorageService _storage;
    private readonly ValidationService _validator;
    private readonly ProxyEngine? _proxyEngine;
    private readonly ShowCommand _showCommand;
    private readonly TargetCommand _targetCommand;
    private readonly SslCommand _sslCommand;

    public CommandProcessor(StorageService storage, ValidationService validator, ProxyEngine? proxyEngine = null)
    {
        _storage = storage;
        _validator = validator;
        _proxyEngine = proxyEngine;
        _showCommand = new ShowCommand(storage);
        _targetCommand = new TargetCommand(storage);
        _sslCommand = new SslCommand(storage);
    }

    private bool EnsureProxyOnly(string command, HttpROS.Models.RouteConfig route)
    {
        if (route.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]Error: Command '{command}' is only available for proxy/static routes.[/]");
            return false;
        }
        return true;
    }

    public void HandleShowCommand(string[] parts, string mode, HttpROS.Models.RouteConfig? activeRoute = null)
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

    public async Task HandleEngineCommand(string[] args)
    {
        if (_proxyEngine == null)
        {
            AnsiConsole.MarkupLine("[red]Error: CLI is running in standalone mode. Use --engine flag to control the engine from this process.[/]");
            return;
        }

        if (args.Length < 1) { AnsiConsole.MarkupLine("[red]Usage: engine <start|stop|status>[/]"); return; }
        
        string sub = args[0].ToLower();
        switch (sub)
        {
            case "start":
                if (_proxyEngine.IsRunning) AnsiConsole.MarkupLine("[yellow]Engine is already running.[/]");
                else 
                {
                    _proxyEngine.Start(Array.Empty<string>());
                    AnsiConsole.MarkupLine("[green]Engine started.[/]");
                }
                break;
            case "stop":
                if (!_proxyEngine.IsRunning) AnsiConsole.MarkupLine("[yellow]Engine is not running.[/]");
                else 
                {
                    await _proxyEngine.Stop();
                    AnsiConsole.MarkupLine("[red]Engine stopped.[/]");
                }
                break;
            case "status":
                string status = _proxyEngine.IsRunning ? "[green]RUNNING[/]" : "[red]STOPPED[/]";
                AnsiConsole.MarkupLine($"Engine Status: {status}");
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown engine command '{sub}'.[/]");
                break;
        }
    }

    public void HandleBackup() 
    {
        AnsiConsole.Status().Start("Creating backup...", ctx => {
            string ts = _storage.CreateBackup();
            AnsiConsole.MarkupLine($"[green]Backup created successfully: {ts}[/]");
        });
    }

    public void HandleRestore(string[] args) 
    {
        if (args.Length == 0)
        {
            var backups = _storage.GetBackups();
            if (backups.Count == 0) { AnsiConsole.MarkupLine("[red]No backups available.[/]"); return; }
            AnsiConsole.MarkupLine("[yellow]Available backups:[/]");
            foreach (var b in backups) Console.WriteLine($"  {b}");
            return;
        }

        string ts = args[0];
        try {
            _storage.RestoreBackup(ts);
            AnsiConsole.MarkupLine($"[green]System restored to backup {ts}.[/]");
        }
        catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]Restore failed: {ex.Message}[/]");
        }
    }

    public void HandleRouteConfig(string command, string[] args, HttpROS.Models.RouteConfig activeRoute, bool isNo)
    {
        switch (command)
        {
            case "target":
                if (isNo) { activeRoute.Target = ""; _storage.SaveRoute(activeRoute); AnsiConsole.MarkupLine("[grey]Target removido.[/]"); }
                else 
                {
                    if (args.Length > 0 && !_validator.IsValidIpOrTarget(args[0]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Target '{args[0]}' inválido. Use IP ou IP:Port.[/]");
                        return;
                    }
                    _targetCommand.Execute(args, activeRoute);
                }
                break;
            case "code":
                {
                    if (!activeRoute.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase))
                    {
                        AnsiConsole.MarkupLine("[red]Error: O comando 'code' só é válido para rotas do tipo redirect.[/]");
                        return;
                    }
                    if (args.Length >= 1 && int.TryParse(args[0], out int c))
                    {
                        if (new[] { 301, 302, 307, 308 }.Contains(c))
                        {
                            activeRoute.RedirectCode = c;
                            _storage.SaveRoute(activeRoute);
                            AnsiConsole.MarkupLine($"[green]Código de redirecionamento definido para {c}.[/]");
                        }
                        else AnsiConsole.MarkupLine("[red]Error: Código inválido. Use 301, 302, 307 ou 308.[/]");
                    }
                    break;
                }
            case "balancer":
                if (!EnsureProxyOnly("balancer", activeRoute)) return;
                break;
            case "upstream":
                if (!EnsureProxyOnly("upstream", activeRoute)) return;
                if (isNo && args.Length >= 1) activeRoute.Balancer.Upstreams.Remove(args[0]);
                else if (!isNo && args.Length >= 1)
                {
                    if (!_validator.IsValidIpOrTarget(args[0]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Upstream '{args[0]}' inválido.[/]");
                        return;
                    }
                    if (!activeRoute.Balancer.Upstreams.Contains(args[0])) activeRoute.Balancer.Upstreams.Add(args[0]);
                }
                _storage.SaveRoute(activeRoute);
                break;
            case "ssl":
                if (!isNo && args.Length >= 2 && args[0] == "manual")
                {
                    if (!_validator.ManualCertificateExists(args[1]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Certificado '{args[1]}' não encontrado em Data/certs/manual/.[/]");
                        return;
                    }
                }
                _sslCommand.Execute(args, activeRoute, isNo);
                break;
            case "gzip":
                if (!EnsureProxyOnly("gzip", activeRoute)) return;
                activeRoute.Features.Gzip = !isNo;
                _storage.SaveRoute(activeRoute);
                break;
            case "websockets":
                if (!EnsureProxyOnly("websockets", activeRoute)) return;
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
            case "ip":
                HandleIpCommand(args, activeRoute, isNo);
                break;
            case "rate-limit":
                if (!isNo && args.Length >= 1)
                {
                    if (!_validator.IsValidRateLimit(args[0]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Formato de rate-limit '{args[0]}' inválido. Use ex: 10r/s.[/]");
                        return;
                    }
                    activeRoute.Features.RateLimit = args[0];
                }
                else if (isNo) activeRoute.Features.RateLimit = null;
                _storage.SaveRoute(activeRoute);
                break;
            case "error-page":
                if (!EnsureProxyOnly("error-page", activeRoute)) return;
                {
                    if (isNo && args.Length >= 1) activeRoute.Features.CustomErrorPages.Remove(args[0]);
                    else if (!isNo && args.Length >= 2) 
                    { 
                        string code = args[0], page = args[1];
                        if (!_validator.ErrorPageExists(page))
                        {
                            AnsiConsole.MarkupLine($"[red]Error: Arquivo de página de erro '{page}' não encontrado em Data/error-pages/.[/]");
                            return;
                        }
                        if (!page.EndsWith(".html")) page += ".html";
                        activeRoute.Features.CustomErrorPages[code] = page; 
                    }
                    _storage.SaveRoute(activeRoute);
                    break;
                }
        }
    }

    public void HandleIpCommand(string[] args, HttpROS.Models.RouteConfig activeRoute, bool isNo)
    {
        if (args.Length < 1) return;
        string sub = args[0].ToLower();

        switch (sub)
        {
            case "mode":
                if (args.Length >= 2)
                {
                    string m = args[1].ToLower();
                    if (m == "whitelist" || m == "blacklist") activeRoute.Features.IpFilter.Mode = m;
                }
                break;
            case "whitelist":
            case "blacklist":
                if (args.Length >= 2)
                {
                    if (!_validator.IsValidIpOrTarget(args[1]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: IP '{args[1]}' inválido.[/]");
                        return;
                    }
                    var list = sub == "whitelist" ? activeRoute.Features.IpFilter.Whitelist : activeRoute.Features.IpFilter.Blacklist;
                    if (isNo) list.Remove(args[1]);
                    else if (!list.Contains(args[1])) list.Add(args[1]);
                }
                break;
        }
        _storage.SaveRoute(activeRoute);
    }

    public void HandleBalancerConfig(string command, string[] args, HttpROS.Models.RouteConfig activeRoute, bool isNo)
    {
        if (!EnsureProxyOnly("balancer-config", activeRoute)) return;
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
                else if (!isNo && args.Length >= 1)
                {
                    if (!_validator.IsValidIpOrTarget(args[0]))
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Upstream '{args[0]}' inválido.[/]");
                        return;
                    }
                    if (!activeRoute.Balancer.Upstreams.Contains(args[0])) activeRoute.Balancer.Upstreams.Add(args[0]);
                }
                break;
            case "health-check":
                HandleHealthCheckCommand(args, activeRoute, isNo);
                break;
        }
        _storage.SaveRoute(activeRoute);
    }

    private void HandleHealthCheckCommand(string[] args, HttpROS.Models.RouteConfig activeRoute, bool isNo)
    {
        if (isNo) { activeRoute.Balancer.HealthCheck.Enabled = false; return; }
        activeRoute.Balancer.HealthCheck.Enabled = true;

        if (args.Length >= 2)
        {
            string sub = args[0].ToLower();
            string val = args[1];
            if (sub == "interval") { if (int.TryParse(val, out int i)) activeRoute.Balancer.HealthCheck.Interval = i; }
            else if (sub == "timeout") { if (int.TryParse(val, out int t)) activeRoute.Balancer.HealthCheck.Timeout = t; }
            else if (sub == "path") activeRoute.Balancer.HealthCheck.Path = val;
        }
    }

    public void HandleErrorPageConfig(string command, string[] args, HttpROS.Models.RouteConfig activeRoute, bool isNo)
    {
        if (!EnsureProxyOnly("error-page-config", activeRoute)) return;
        if (isNo)
        {
            activeRoute.Features.CustomErrorPages.Remove(command);
        }
        else if (args.Length >= 1)
        {
            string page = args[0];
            if (!_validator.ErrorPageExists(page))
            {
                AnsiConsole.MarkupLine($"[red]Error: Arquivo '{page}' não encontrado.[/]");
                return;
            }
            if (!page.EndsWith(".html")) page += ".html";
            activeRoute.Features.CustomErrorPages[command] = page;
        }
        _storage.SaveRoute(activeRoute);
    }

    public void HandleMonitorLogs()
    {
        string logFile = Path.Combine(_storage.GetDataRoot(), "logs", "access.log");
        if (!File.Exists(logFile))
        {
            AnsiConsole.MarkupLine("[red]Error: Log file not found. Ensure the Engine is running.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[yellow]Streaming access.log... Press Ctrl+C to stop.[/]");
        
        using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        stream.Seek(0, SeekOrigin.End);

        try {
            while (true)
            {
                string? line = reader.ReadLine();
                if (line != null) Console.WriteLine(line);
                else Thread.Sleep(500);
            }
        } catch (ThreadInterruptedException) { }
    }
}
