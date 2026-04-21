using System.Text.Json;
using HttpROS.Models;

namespace HttpROS.Services;

public class StorageService
{
    private readonly string[] _types = { "proxy", "static", "redirect" };

    public StorageService()
    {
        foreach (var t in _types)
        {
            if (!Directory.Exists(t)) Directory.CreateDirectory(t);
        }
    }

    public void SaveRoute(RouteConfig route)
    {
        string dir = route.Type.ToLower();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        string filePath = Path.Combine(dir, $"{route.Domain}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(route, options);
        File.WriteAllText(filePath, json);
    }

    public RouteConfig? LoadRoute(string type, string domain)
    {
        string filePath = Path.Combine(type.ToLower(), $"{domain}.json");
        if (!File.Exists(filePath)) return null;

        try 
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<RouteConfig>(json);
        }
        catch (JsonException)
        {
            // If the JSON is old/incompatible, return null so a new one can be created
            return null;
        }
    }

    public List<RouteConfig> GetAllRoutes()
    {
        var routes = new List<RouteConfig>();
        foreach (var type in _types)
        {
            if (Directory.Exists(type))
            {
                foreach (var file in Directory.GetFiles(type, "*.json"))
                {
                    try 
                    {
                        var json = File.ReadAllText(file);
                        var route = JsonSerializer.Deserialize<RouteConfig>(json);
                        if (route != null) routes.Add(route);
                    }
                    catch { /* Skip incompatible files */ }
                }
            }
        }
        return routes;
    }
}
