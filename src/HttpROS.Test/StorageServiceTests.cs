using Xunit;
using HttpROS.Data;
using HttpROS.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace HttpROS.Test;

public class StorageServiceTests
{
    private readonly StorageService _storage;
    private readonly string _testDataDir = "TestData_Storage";

    public StorageServiceTests()
    {
        if (Directory.Exists(_testDataDir)) Directory.Delete(_testDataDir, true);
        Directory.CreateDirectory(_testDataDir);

        var myConfiguration = new Dictionary<string, string>
        {
            {"Settings:DataPath", _testDataDir}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration!)
            .Build();

        _storage = new StorageService(config);
    }

    [Fact]
    public void SaveAndLoadRoute_Works()
    {
        var route = new RouteConfig { Domain = "test.com", Type = "proxy", Target = "1.1.1.1" };
        _storage.SaveRoute(route);

        var loaded = _storage.LoadRoute("proxy", "test.com");
        Assert.NotNull(loaded);
        Assert.Equal("1.1.1.1", loaded.Target);
    }

    [Fact]
    public void BackupAndRestore_Works()
    {
        // 1. Save initial data
        _storage.SaveRoute(new RouteConfig { Domain = "initial.com", Type = "proxy" });
        
        // 2. Create backup
        string ts = _storage.CreateBackup();
        Assert.Contains(ts, _storage.GetBackups());

        // 3. Modify data
        _storage.SaveRoute(new RouteConfig { Domain = "new.com", Type = "proxy" });
        _storage.DeleteRoute("proxy", "initial.com");
        Assert.Null(_storage.LoadRoute("proxy", "initial.com"));

        // 4. Restore
        _storage.RestoreBackup(ts);
        
        // 5. Verify restoration
        Assert.NotNull(_storage.LoadRoute("proxy", "initial.com"));
        Assert.Null(_storage.LoadRoute("proxy", "new.com"));
    }

    [Fact]
    public void ConflictDetection_Works()
    {
        _storage.SaveRoute(new RouteConfig { Domain = "test.com", Type = "proxy" });
        
        var conflict = _storage.FindConflictingRoute("test.com", "static");
        Assert.NotNull(conflict);
        Assert.Equal("proxy", conflict.Type);
    }
}
