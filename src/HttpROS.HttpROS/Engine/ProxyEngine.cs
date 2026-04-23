using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using HttpROS.Data;
using HttpROS.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;
using System.Net;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace HttpROS.Engine;

public class ProxyEngine
{
    private readonly StorageService _storage;
    private readonly ValidationService _validator;
    private InMemoryConfigProvider? _configProvider;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private WebApplication? _app;

    public bool IsRunning => _app != null;

    public ProxyEngine(StorageService storage, ValidationService validator)
    {
        _storage = storage;
        _validator = validator;
    }

    private void LogToFile(string message)
    {
        try {
            string logDir = Path.Combine(_storage.GetDataRoot(), "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "access.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        } catch { }
    }

    public void Start(string[] args)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.None);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(80);
            options.ListenAnyIP(443, listenOptions =>
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                    {
                        if (string.IsNullOrEmpty(name)) return null;
                        var allRoutes = _storage.GetAllRoutes();
                        var route = FindBestMatch(name, allRoutes);
                        if (route == null || route.Features.Ssl == null || !route.Features.Ssl.Enabled) return null;
                        string certFolder = route.Features.Ssl.Provider == "manual" ? "manual" : "lets-encrypt";
                        string certName = route.Domain.StartsWith("*.") ? $"wildcard_{route.Domain.Substring(2)}" : name;
                        string certPath = Path.Combine(_storage.GetDataRoot(), "certs", certFolder, $"{certName}.pfx");
                        if (File.Exists(certPath)) return X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
                        return null;
                    };
                });
            });
        });

        builder.WebHost.SuppressStatusMessages(true);
        builder.Services.AddResponseCompression(options => { options.EnableForHttps = true; });

        var (routes, clusters) = MapConfigs();
        _configProvider = new InMemoryConfigProvider(routes, clusters);

        builder.Services.AddSingleton<IProxyConfigProvider>(_configProvider);
        builder.Services.AddReverseProxy();

        builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; });
        builder.Services.AddCors(options => { options.AddPolicy("DefaultCors", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()); });

        _app = builder.Build();
        _app.UseResponseCompression();
        
        _app.Use(async (context, next) =>
        {
            var host = context.Request.Host.Host;
            var path = context.Request.Path;
            var ip = context.Connection.RemoteIpAddress?.ToString();

            var allRoutes = _storage.GetAllRoutes();
            var route = FindBestMatch(host, allRoutes);
            
            if (route != null)
            {
                if (!HandleIpFilter(context, route)) { LogToFile($"[403] {ip} -> {host}{path}"); return; }
                if (!await HandleBasicAuth(context, route)) { LogToFile($"[401] {ip} -> {host}{path}"); return; }
                if (!HandleRateLimit(context, route)) { LogToFile($"[429] {ip} -> {host}{path}"); return; }

                if (route.Features.Cors)
                {
                    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Append("Access-Control-Allow-Methods", "*");
                    context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
                }

                if (route.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase))
                {
                    var target = route.Target;
                    if (!target.StartsWith("http")) target = $"http://{target}";
                    LogToFile($"[3xx] {ip} -> {host}{path} to {target}");
                    context.Response.Redirect(target, permanent: route.RedirectCode == 301 || route.RedirectCode == 308);
                    if (route.RedirectCode != 301 && route.RedirectCode != 308) context.Response.StatusCode = route.RedirectCode;
                    return;
                }

                if (route.Type.Equals("static", StringComparison.OrdinalIgnoreCase))
                {
                    var rootPath = Path.IsPathRooted(route.Target) ? route.Target : Path.Combine(Directory.GetCurrentDirectory(), route.Target);
                    var filePath = Path.Combine(rootPath, path.Value?.TrimStart('/') ?? "");
                    if (string.IsNullOrEmpty(path.Value) || path.Value == "/") filePath = Path.Combine(rootPath, "index.html");
                    if (File.Exists(filePath))
                    {
                        context.Response.ContentType = GetContentType(filePath);
                        LogToFile($"[200] {ip} -> {host}{path} (Static)");
                        await context.Response.SendFileAsync(filePath);
                        return;
                    }
                }

                LogToFile($"[200] {ip} -> {host}{path} (Proxy)");
                await next();
            }
            else
            {
                LogToFile($"[404] {ip} -> {host}{path} (No Route)");
                await next();
            }
        });

        _app.UseRouting();
        _app.UseRateLimiter();
        _app.UseCors("DefaultCors");
        _app.MapReverseProxy();

        SetupWatcher();
        
        LogToFile("[SYSTEM] Engine started.");
        _app.RunAsync(_cts.Token);
    }

    public async Task Stop()
    {
        if (_app != null && _cts != null)
        {
            _cts.Cancel();
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
            LogToFile("[SYSTEM] Engine stopped.");
        }
    }

    private void SetupWatcher()
    {
        if (_watcher != null) return;
        _watcher = new FileSystemWatcher(_storage.GetDataRoot())
        {
            IncludeSubdirectories = true,
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        _watcher.Changed += (s, e) => Reload();
        _watcher.Created += (s, e) => Reload();
        _watcher.Deleted += (s, e) => Reload();
        _watcher.EnableRaisingEvents = true;
    }

    private string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext switch { ".html" => "text/html", ".css" => "text/css", ".js" => "application/javascript", ".json" => "application/json", ".png" => "image/png", ".jpg" => "image/jpeg", ".svg" => "image/svg+xml", _ => "application/octet-stream" };
    }

    private HttpROS.Models.RouteConfig? FindBestMatch(string host, List<HttpROS.Models.RouteConfig> routes)
    {
        var exact = routes.FirstOrDefault(r => r.Domain.Equals(host, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        return routes.FirstOrDefault(r => { if (!r.Domain.StartsWith("*.")) return false; var suffix = r.Domain.Substring(2); return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && host.Length > suffix.Length; });
    }

    public void Reload()
    {
        if (_configProvider == null) return;
        var (routes, clusters) = MapConfigs();
        _configProvider.Update(routes, clusters);
        LogToFile("[SYSTEM] Config reloaded.");
    }

    private readonly ConcurrentDictionary<string, (int count, DateTime reset)> _rateStats = new();

    private bool HandleRateLimit(HttpContext context, HttpROS.Models.RouteConfig route)
    {
        if (string.IsNullOrEmpty(route.Features.RateLimit)) return true;
        var match = System.Text.RegularExpressions.Regex.Match(route.Features.RateLimit, @"^(\d+)r/([sm])$");
        if (!match.Success) return true;
        int limit = int.Parse(match.Groups[1].Value);
        string unit = match.Groups[2].Value;
        int seconds = unit == "s" ? 1 : 60;
        var now = DateTime.UtcNow;
        var key = $"{route.Domain}-{context.Connection.RemoteIpAddress}";
        var stat = _rateStats.GetOrAdd(key, _ => (0, now.AddSeconds(seconds)));
        if (now > stat.reset) stat = (0, now.AddSeconds(seconds));
        if (stat.count >= limit) { context.Response.StatusCode = 429; return false; }
        _rateStats[key] = (stat.count + 1, stat.reset);
        return true;
    }

    private bool HandleIpFilter(HttpContext context, HttpROS.Models.RouteConfig route)
    {
        var filter = route.Features.IpFilter;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        if (clientIp == null) return true;
        bool allowed = filter.Mode == "blacklist";
        if (filter.Mode == "blacklist") { if (filter.Blacklist.Contains(clientIp)) allowed = false; }
        else if (filter.Mode == "whitelist") { allowed = filter.Whitelist.Contains(clientIp); }
        if (!allowed) { context.Response.StatusCode = 403; return false; }
        return true;
    }

    private async Task<bool> HandleBasicAuth(HttpContext context, HttpROS.Models.RouteConfig route)
    {
        if (route.Features.BasicAuth == null) return true;
        if (!context.Request.Headers.ContainsKey("Authorization")) { context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"HttpROS\""); context.Response.StatusCode = 401; return false; }
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) { var token = authHeader.Substring(6); var credentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split(':'); if (credentials.Length == 2 && credentials[0] == route.Features.BasicAuth.User && credentials[1] == route.Features.BasicAuth.Pass) return true; }
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return false;
    }

    private (IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) MapConfigs()
    {
        var yarpRoutes = new List<Yarp.ReverseProxy.Configuration.RouteConfig>();
        var yarpClusters = new List<ClusterConfig>();
        var allRoutes = _storage.GetAllRoutes();
        foreach (var r in allRoutes)
        {
            if (r.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase)) continue;
            if (r.Type.Equals("static", StringComparison.OrdinalIgnoreCase)) continue;
            var clusterId = $"cluster-{r.Domain.Replace("*", "wildcard")}";
            var route = new Yarp.ReverseProxy.Configuration.RouteConfig { RouteId = $"route-{r.Domain.Replace("*", "wildcard")}", ClusterId = clusterId, Match = new RouteMatch { Hosts = new[] { r.Domain } } };
            if (r.Features.Cors) route = route with { CorsPolicy = "DefaultCors" };
            yarpRoutes.Add(route);
            var destinations = new Dictionary<string, DestinationConfig>();
            if (!string.IsNullOrEmpty(r.Target)) destinations.Add("primary", new DestinationConfig { Address = r.Target.StartsWith("http") ? r.Target : $"http://{r.Target}" });
            int i = 1;
            foreach (var upstream in r.Balancer.Upstreams) destinations.Add($"upstream-{i++}", new DestinationConfig { Address = upstream.StartsWith("http") ? upstream : $"http://{upstream}" });
            yarpClusters.Add(new ClusterConfig { ClusterId = clusterId, Destinations = destinations, SessionAffinity = r.Balancer.Sticky ? new SessionAffinityConfig { Enabled = true, Policy = "Cookie", AffinityKeyName = ".HttpROS.Affinity" } : null, LoadBalancingPolicy = MapPolicy(r.Balancer.Method), HealthCheck = MapHealthCheck(r.Balancer.HealthCheck), HttpRequest = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(30) } });
        }
        return (yarpRoutes, yarpClusters);
    }

    private string MapPolicy(string method) { return method switch { "round-robin" => "RoundRobin", "least-conn" => "LeastRequests", _ => "RoundRobin" }; }

    private Yarp.ReverseProxy.Configuration.HealthCheckConfig? MapHealthCheck(HttpROS.Models.HealthCheckConfig hc)
    {
        if (!hc.Enabled) return null;
        return new Yarp.ReverseProxy.Configuration.HealthCheckConfig { Active = new ActiveHealthCheckConfig { Enabled = true, Interval = TimeSpan.FromSeconds(hc.Interval), Timeout = TimeSpan.FromSeconds(hc.Timeout), Path = hc.Path, Policy = "ConsecutiveFailures" } };
    }
}
