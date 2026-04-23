using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HttpROS.Data;

public class ValidationService
{
    private readonly string _dataRoot;

    public ValidationService(IConfiguration configuration)
    {
        _dataRoot = configuration["Settings:DataPath"] ?? "Data";
    }

    public bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;
        // Support for wildcards like *.example.com
        var regex = new Regex(@"^(\*\.)?(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9][a-z0-9-]{0,61}[a-z0-9]$");
        return regex.IsMatch(domain.ToLower());
    }

    public bool IsValidIpOrTarget(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        
        // Allow URLs (http:// or https://)
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;

        // Check if it's just an IP
        if (IPAddress.TryParse(input, out _)) return true;

        // Check if it's IP:Port
        var parts = input.Split(':');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out _) && int.TryParse(parts[1], out int port))
        {
            return port > 0 && port <= 65535;
        }

        // Allow Hostnames (Require at least one dot to distinguish from random strings)
        var hostRegex = new Regex(@"^([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])(\.([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]))+$");
        return hostRegex.IsMatch(input);
    }

    public bool IsValidRateLimit(string rate)
    {
        if (string.IsNullOrWhiteSpace(rate)) return false;
        // Format: 10r/s or 100r/m
        return Regex.IsMatch(rate, @"^\d+r/[sm]$");
    }

    public bool ErrorPageExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        if (!fileName.EndsWith(".html")) fileName += ".html";
        return File.Exists(Path.Combine(_dataRoot, "error-pages", fileName));
    }

    public bool ManualCertificateExists(string certName)
    {
        if (string.IsNullOrWhiteSpace(certName)) return false;
        return File.Exists(Path.Combine(_dataRoot, "certs", "manual", certName));
    }
}
