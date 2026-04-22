using Xunit;
using HttpROS.CLI;
using HttpROS.Data;
using HttpROS.Models;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace HttpROS.Test;

public class CliEngineTests
{
    private readonly string _testDataDir = "TestData_Cli_Final";
    private readonly IConfiguration _config;
    private readonly StorageService _storage;
    private readonly ValidationService _validator;

    public CliEngineTests()
    {
        if (Directory.Exists(_testDataDir)) Directory.Delete(_testDataDir, true);
        Directory.CreateDirectory(_testDataDir);

        var myConfiguration = new Dictionary<string, string>
        {
            {"Settings:DataPath", _testDataDir}
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration!)
            .Build();

        _storage = new StorageService(_config);
        _validator = new ValidationService(_config);
    }

    private CliEngine CreateEngine() => new CliEngine(_storage, _validator, _config);

    [Fact]
    public void Navigation_BasicFlow_Works()
    {
        var engine = CreateEngine();
        Assert.Equal("view", engine.CurrentMode);

        engine.ProcessInput("configure");
        Assert.Equal("config", engine.CurrentMode);

        engine.ProcessInput("proxy example.com");
        Assert.Equal("route-config", engine.CurrentMode);
        Assert.Equal("example.com", engine.ActiveRoute?.Domain);

        engine.ProcessInput("exit");
        Assert.Equal("config", engine.CurrentMode);
        Assert.Null(engine.ActiveRoute);

        engine.ProcessInput("exit");
        Assert.Equal("view", engine.CurrentMode);
    }

    [Fact]
    public void Navigation_TopCommand_ResetsToView()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy example.com");
        engine.ProcessInput("balancer");
        Assert.Equal("balancer-config", engine.CurrentMode);

        engine.ProcessInput("top");
        Assert.Equal("view", engine.CurrentMode);
        Assert.Null(engine.ActiveRoute);
    }

    [Fact]
    public void Navigation_ReturnCommand_BackToConfig()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy example.com");
        engine.ProcessInput("balancer");
        
        engine.ProcessInput("return");
        Assert.Equal("config", engine.CurrentMode);
        Assert.Null(engine.ActiveRoute);
    }

    [Fact]
    public void RouteConfig_Features_UpdateCorrectly()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy test.com");

        engine.ProcessInput("target 127.0.0.1:8080");
        Assert.Equal("127.0.0.1:8080", engine.ActiveRoute?.Target);

        engine.ProcessInput("ssl lets-encrypt");
        Assert.True(engine.ActiveRoute?.Features.Ssl.Enabled);
        Assert.Equal("lets-encrypt", engine.ActiveRoute?.Features.Ssl.Provider);

        engine.ProcessInput("gzip");
        Assert.True(engine.ActiveRoute?.Features.Gzip);

        engine.ProcessInput("no gzip");
        Assert.False(engine.ActiveRoute?.Features.Gzip);

        engine.ProcessInput("rate-limit 10r/s");
        Assert.Equal("10r/s", engine.ActiveRoute?.Features.RateLimit);
    }

    [Fact]
    public void RouteConfig_AllFlags_SaturationTest()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy flags.com");

        // Websockets
        engine.ProcessInput("websockets");
        Assert.True(engine.ActiveRoute?.Features.Websockets);
        engine.ProcessInput("no websockets");
        Assert.False(engine.ActiveRoute?.Features.Websockets);

        // CORS
        engine.ProcessInput("cors");
        Assert.True(engine.ActiveRoute?.Features.Cors);
        engine.ProcessInput("no cors");
        Assert.False(engine.ActiveRoute?.Features.Cors);

        // Auth
        engine.ProcessInput("auth user pass123");
        Assert.Equal("user", engine.ActiveRoute?.Features.BasicAuth?.User);
        Assert.Equal("pass123", engine.ActiveRoute?.Features.BasicAuth?.Pass);
        engine.ProcessInput("no auth");
        Assert.Null(engine.ActiveRoute?.Features.BasicAuth);

        // IP Filter removal
        engine.ProcessInput("ip whitelist 1.2.3.4");
        engine.ProcessInput("no ip whitelist 1.2.3.4");
        Assert.Empty(engine.ActiveRoute!.Features.IpFilter.Whitelist);
        
        // Target removal
        engine.ProcessInput("target 1.1.1.1");
        engine.ProcessInput("no target");
        Assert.Equal("", engine.ActiveRoute.Target);
    }

    [Fact]
    public void RouteConfig_IpCommands_Work()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy test.com");

        engine.ProcessInput("ip mode whitelist");
        Assert.Equal("whitelist", engine.ActiveRoute?.Features.IpFilter.Mode);

        engine.ProcessInput("ip whitelist 1.1.1.1");
        Assert.Contains("1.1.1.1", engine.ActiveRoute?.Features.IpFilter.Whitelist);

        engine.ProcessInput("no ip whitelist 1.1.1.1");
        Assert.DoesNotContain("1.1.1.1", engine.ActiveRoute?.Features.IpFilter.Whitelist);
    }

    [Fact]
    public void BalancerConfig_Commands_Work()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy test.com");
        engine.ProcessInput("balancer");
        Assert.Equal("balancer-config", engine.CurrentMode);

        engine.ProcessInput("method least-conn");
        Assert.Equal("least-conn", engine.ActiveRoute?.Balancer.Method);

        engine.ProcessInput("upstream 10.0.0.1:80");
        Assert.Contains("10.0.0.1:80", engine.ActiveRoute?.Balancer.Upstreams);

        engine.ProcessInput("health-check interval 60");
        Assert.True(engine.ActiveRoute?.Balancer.HealthCheck.Enabled);
        Assert.Equal(60, engine.ActiveRoute?.Balancer.HealthCheck.Interval);

        engine.ProcessInput("no health-check");
        Assert.False(engine.ActiveRoute?.Balancer.HealthCheck.Enabled);
    }

    [Fact]
    public void ErrorPageConfig_Commands_Work()
    {
        string errorPageDir = Path.Combine(_testDataDir, "error-pages");
        Directory.CreateDirectory(errorPageDir);
        File.WriteAllText(Path.Combine(errorPageDir, "404.html"), "<html></html>");

        var engine = CreateEngine();
        engine.ProcessInput("configure");
        engine.ProcessInput("proxy test.com");
        engine.ProcessInput("error-page");
        Assert.Equal("error-page-config", engine.CurrentMode);

        engine.ProcessInput("404 404.html");
        Assert.Equal("404.html", engine.ActiveRoute?.Features.CustomErrorPages["404"]);

        engine.ProcessInput("no 404");
        Assert.False(engine.ActiveRoute?.Features.CustomErrorPages.ContainsKey("404"));
    }

    [Fact]
    public void Validation_InvalidInputs_AreRejected()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        
        // Invalid domain
        engine.ProcessInput("proxy invalid_domain");
        Assert.Equal("config", engine.CurrentMode);

        engine.ProcessInput("proxy valid.com");
        Assert.Equal("route-config", engine.CurrentMode);

        // Invalid target
        engine.ProcessInput("target not-an-ip");
        Assert.Equal("", engine.ActiveRoute?.Target);

        // Invalid rate-limit
        engine.ProcessInput("rate-limit 100kbps");
        Assert.Null(engine.ActiveRoute?.Features.RateLimit);
    }

    [Fact]
    public void OperationalCommands_Work()
    {
        var engine = CreateEngine();
        engine.ProcessInput("clear");
        engine.ProcessInput("exit");
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void RouteTypes_Creation_Works()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        
        // Static
        engine.ProcessInput("static static.com");
        Assert.Equal("static", engine.ActiveRoute?.Type);
        engine.ProcessInput("exit");

        // Redirect
        engine.ProcessInput("redirect redirect.com");
        Assert.Equal("redirect", engine.ActiveRoute?.Type);
    }

    [Fact]
    public void RouteConflict_PreventsDuplicates()
    {
        var engine = CreateEngine();
        engine.ProcessInput("configure");
        
        // Create a proxy
        engine.ProcessInput("proxy my.com");
        engine.ProcessInput("exit");

        // Try to create a static with same domain
        engine.ProcessInput("static my.com");
        Assert.Equal("config", engine.CurrentMode); // Should stay in config due to conflict
        Assert.Null(engine.ActiveRoute);
    }
}
