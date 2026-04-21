using System.Text.Json.Serialization;

namespace HttpROS.Models;

public class RouteConfig
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "proxy"; 

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("balancer")]
    public BalancerConfig Balancer { get; set; } = new();

    [JsonPropertyName("features")]
    public RouteFeatures Features { get; set; } = new();
}

public class BalancerConfig
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "round-robin"; // round-robin, least-conn, ip-hash

    [JsonPropertyName("sticky")]
    public bool Sticky { get; set; }

    [JsonPropertyName("upstreams")]
    public List<string> Upstreams { get; set; } = new();
}

public class RouteFeatures
{
    [JsonPropertyName("ssl")]
    public SslConfig Ssl { get; set; } = new();

    [JsonPropertyName("gzip")]
    public bool Gzip { get; set; }

    [JsonPropertyName("websockets")]
    public bool Websockets { get; set; }

    [JsonPropertyName("cors")]
    public bool Cors { get; set; }

    [JsonPropertyName("ipFilter")]
    public IpFilterConfig IpFilter { get; set; } = new();

    [JsonPropertyName("basicAuth")]
    public BasicAuth? BasicAuth { get; set; }

    [JsonPropertyName("rateLimit")]
    public string? RateLimit { get; set; }

    [JsonPropertyName("customErrorPages")]
    public Dictionary<string, string> CustomErrorPages { get; set; } = new();
}

public class SslConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "lets-encrypt";

    [JsonPropertyName("certName")]
    public string? CertName { get; set; }
}

public class IpFilterConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "blacklist";

    [JsonPropertyName("whitelist")]
    public List<string> Whitelist { get; set; } = new();

    [JsonPropertyName("blacklist")]
    public List<string> Blacklist { get; set; } = new();
}

public class BasicAuth
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("pass")]
    public string Pass { get; set; } = string.Empty;
}
