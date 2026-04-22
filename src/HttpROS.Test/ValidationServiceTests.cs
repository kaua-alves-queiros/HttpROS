using Xunit;
using HttpROS.Data;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace HttpROS.Test;

public class ValidationServiceTests
{
    private readonly ValidationService _validator;
    private readonly string _testDataDir = "TestData_Validation";

    public ValidationServiceTests()
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

        _validator = new ValidationService(config);
    }

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("sub.example.com", true)]
    [InlineData("invalid-domain", false)]
    [InlineData("test@com", false)]
    [InlineData("", false)]
    public void IsValidDomain_Validation_Works(string domain, bool expected)
    {
        Assert.Equal(expected, _validator.IsValidDomain(domain));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("192.168.1.1:8080", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("not-an-ip", false)]
    [InlineData("1.1.1.1:999999", false)]
    public void IsValidIpOrTarget_Validation_Works(string input, bool expected)
    {
        Assert.Equal(expected, _validator.IsValidIpOrTarget(input));
    }

    [Theory]
    [InlineData("10r/s", true)]
    [InlineData("100r/m", true)]
    [InlineData("100", false)]
    [InlineData("10r/h", false)]
    public void IsValidRateLimit_Validation_Works(string rate, bool expected)
    {
        Assert.Equal(expected, _validator.IsValidRateLimit(rate));
    }

    [Fact]
    public void AssetExistence_Validation_Works()
    {
        string errorPagePath = Path.Combine(_testDataDir, "error-pages");
        Directory.CreateDirectory(errorPagePath);
        File.WriteAllText(Path.Combine(errorPagePath, "test.html"), "");

        Assert.True(_validator.ErrorPageExists("test.html"));
        Assert.True(_validator.ErrorPageExists("test")); // Should append .html
        Assert.False(_validator.ErrorPageExists("missing.html"));
    }
}
