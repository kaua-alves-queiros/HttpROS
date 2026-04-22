using Yarp.ReverseProxy.Configuration;
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

    public ProxyEngine(StorageService storage, ValidationService validator)
    {
        _storage = storage;
        _validator = validator;
    }

    public void Start(string[] args)
    {
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
                        var route = _storage.GetAllRoutes().FirstOrDefault(r => r.Domain.Equals(name, StringComparison.OrdinalIgnoreCase));
                        
                        if (route == null || route.Features.Ssl == null || !route.Features.Ssl.Enabled) return null;

                        string certFolder = route.Features.Ssl.Provider == "manual" ? "manual" : "lets-encrypt";
                        string certPath = Path.Combine(_storage.GetDataRoot(), "certs", certFolder, $"{name}.pfx");
                        
                        if (File.Exists(certPath))
                        {
                            return X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
                        }
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

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DefaultCors", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseResponseCompression();
        
        app.Use(async (context, next) =>
        {
            var host = context.Request.Host.Host;
            var route = _storage.GetAllRoutes().FirstOrDefault(r => r.Domain.Equals(host, StringComparison.OrdinalIgnoreCase));
            
            if (route != null)
            {
                // 1. IP Filter
                if (!HandleIpFilter(context, route)) return;

                // 2. Auth
                if (!await HandleBasicAuth(context, route)) return;

                // 3. Rate Limit
                if (!HandleRateLimit(context, route)) return;

                // 4. Handle REDIRECT directly in middleware
                if (route.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase))
                {
                    var target = route.Target;
                    if (!target.StartsWith("http")) target = $"http://{target}";
                    context.Response.Redirect(target, permanent: route.RedirectCode == 301 || route.RedirectCode == 308);
                    
                    // If it's a specific code like 307 or 302 (non-permanent), we set it explicitly
                    if (route.RedirectCode != 301 && route.RedirectCode != 308)
                    {
                        context.Response.StatusCode = route.RedirectCode;
                    }
                    return;
                }

                // 5. If it's proxy/static, let YARP handle it
                await next();

                // 6. Custom Error Pages
                if (route.Features.CustomErrorPages.ContainsKey(context.Response.StatusCode.ToString()))
                {
                    var page = route.Features.CustomErrorPages[context.Response.StatusCode.ToString()];
                    var filePath = Path.Combine(_storage.GetDataRoot(), "error-pages", page);
                    if (File.Exists(filePath))
                    {
                        var content = File.ReadAllText(filePath);
                        context.Response.ContentType = "text/html";
                        await context.Response.WriteAsync(content);
                    }
                }
            }
            else
            {
                await next();
            }
        });

        app.UseRouting();
        app.UseRateLimiter();
        app.UseCors("DefaultCors");
        app.MapReverseProxy();

        Task.Run(() => app.Run());
    }

    public void Reload()
    {
        if (_configProvider == null) return;
        var (routes, clusters) = MapConfigs();
        _configProvider.Update(routes, clusters);
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
        
        if (now > stat.reset)
        {
            stat = (0, now.AddSeconds(seconds));
        }

        if (stat.count >= limit)
        {
            context.Response.StatusCode = 429;
            return false;
        }

        _rateStats[key] = (stat.count + 1, stat.reset);
        return true;
    }

    private bool HandleIpFilter(HttpContext context, HttpROS.Models.RouteConfig route)
    {
        var filter = route.Features.IpFilter;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        if (clientIp == null) return true;

        bool allowed = filter.Mode == "blacklist";
        if (filter.Mode == "blacklist")
        {
            if (filter.Blacklist.Contains(clientIp)) allowed = false;
        }
        else if (filter.Mode == "whitelist")
        {
            allowed = filter.Whitelist.Contains(clientIp);
        }

        if (!allowed)
        {
            context.Response.StatusCode = 403;
            return false;
        }
        return true;
    }

    private async Task<bool> HandleBasicAuth(HttpContext context, HttpROS.Models.RouteConfig route)
    {
        if (route.Features.BasicAuth == null) return true;

        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"HttpROS\"");
            context.Response.StatusCode = 401;
            return false;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring(6);
            var credentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split(':');
            if (credentials.Length == 2 && credentials[0] == route.Features.BasicAuth.User && credentials[1] == route.Features.BasicAuth.Pass)
            {
                return true;
            }
        }

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
            // Note: We only register proxy/static routes in YARP. 
            // Redirects are handled in our middleware for speed/reliability.
            if (r.Type.Equals("redirect", StringComparison.OrdinalIgnoreCase)) continue;

            var clusterId = $"cluster-{r.Domain}";

            var route = new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = $"route-{r.Domain}",
                ClusterId = clusterId,
                Match = new RouteMatch { Hosts = new[] { r.Domain } }
            };

            if (r.Features.Cors) route = route with { CorsPolicy = "DefaultCors" };
            yarpRoutes.Add(route);

            var destinations = new Dictionary<string, DestinationConfig>();
            if (!string.IsNullOrEmpty(r.Target))
            {
                destinations.Add("primary", new DestinationConfig { Address = r.Target.StartsWith("http") ? r.Target : $"http://{r.Target}" });
            }

            int i = 1;
            foreach (var upstream in r.Balancer.Upstreams)
            {
                destinations.Add($"upstream-{i++}", new DestinationConfig { Address = upstream.StartsWith("http") ? upstream : $"http://{upstream}" });
            }

            yarpClusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = destinations,
                SessionAffinity = r.Balancer.Sticky ? new SessionAffinityConfig { Enabled = true, Policy = "Cookie", AffinityKeyName = ".HttpROS.Affinity" } : null,
                LoadBalancingPolicy = MapPolicy(r.Balancer.Method),
                HealthCheck = MapHealthCheck(r.Balancer.HealthCheck)
            });
        }

        return (yarpRoutes, yarpClusters);
    }

    private string MapPolicy(string method)
    {
        return method switch
        {
            "round-robin" => "RoundRobin",
            "least-conn" => "LeastRequests",
            _ => "RoundRobin"
        };
    }

    private Yarp.ReverseProxy.Configuration.HealthCheckConfig? MapHealthCheck(HttpROS.Models.HealthCheckConfig hc)
    {
        if (!hc.Enabled) return null;
        return new Yarp.ReverseProxy.Configuration.HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(hc.Interval),
                Timeout = TimeSpan.FromSeconds(hc.Timeout),
                Path = hc.Path,
                Policy = "ConsecutiveFailures"
            }
        };
    }
}
