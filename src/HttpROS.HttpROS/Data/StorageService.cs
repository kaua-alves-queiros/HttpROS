using System.Text.Json;
using HttpROS.Models;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace HttpROS.Data;

public class StorageService
{
    private readonly string[] _types = { "proxy", "static", "redirect" };
    private readonly string _dataRoot;

    public string GetDataRoot() => _dataRoot;

    public StorageService(IConfiguration configuration)
    {
        _dataRoot = configuration["Settings:DataPath"] ?? "Data";
        
        foreach (var t in _types)
        {
            string path = Path.Combine(_dataRoot, t);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
        
        EnsureDir("error-pages");
        EnsureDir("certs");
        EnsureDir("certs/manual");
        EnsureDir("certs/lets-encrypt");
        EnsureDir("backups");
    }

    private void EnsureDir(string name)
    {
        string path = Path.Combine(_dataRoot, name);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    public void SaveRoute(RouteConfig route)
    {
        string dir = Path.Combine(_dataRoot, route.Type.ToLower());
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        string filePath = Path.Combine(dir, $"{route.Domain}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(route, options);
        File.WriteAllText(filePath, json);
    }

    public RouteConfig? LoadRoute(string type, string domain)
    {
        string filePath = Path.Combine(_dataRoot, type.ToLower(), $"{domain}.json");
        if (!File.Exists(filePath)) return null;

        try 
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<RouteConfig>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public RouteConfig? FindConflictingRoute(string domain, string currentType)
    {
        foreach (var type in _types)
        {
            if (type == currentType) continue;
            string filePath = Path.Combine(_dataRoot, type, $"{domain}.json");
            if (File.Exists(filePath)) return LoadRoute(type, domain);
        }
        return null;
    }

    public void DeleteRoute(string type, string domain)
    {
        string filePath = Path.Combine(_dataRoot, type.ToLower(), $"{domain}.json");
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    public List<RouteConfig> GetAllRoutes()
    {
        var routes = new List<RouteConfig>();
        foreach (var type in _types)
        {
            string path = Path.Combine(_dataRoot, type);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.json"))
                {
                    try 
                    {
                        var json = File.ReadAllText(file);
                        var route = JsonSerializer.Deserialize<RouteConfig>(json);
                        if (route != null) routes.Add(route);
                    }
                    catch { }
                }
            }
        }
        return routes;
    }

    public string CreateBackup()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(_dataRoot, "backups", timestamp);
        Directory.CreateDirectory(backupPath);

        foreach (var dir in Directory.GetDirectories(_dataRoot))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName == "backups") continue;

            string targetDir = Path.Combine(backupPath, dirName);
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(dir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            }
        }
        return timestamp;
    }

    public void RestoreBackup(string timestamp)
    {
        string backupPath = Path.Combine(_dataRoot, "backups", timestamp);
        if (!Directory.Exists(backupPath)) throw new DirectoryNotFoundException("Backup not found.");

        // Clear current data except backups
        foreach (var dir in Directory.GetDirectories(_dataRoot))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName == "backups") continue;
            Directory.Delete(dir, true);
        }

        // Copy back from backup
        foreach (var dir in Directory.GetDirectories(backupPath))
        {
            string dirName = Path.GetFileName(dir);
            string targetDir = Path.Combine(_dataRoot, dirName);
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(dir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            }
        }
    }

    public List<string> GetBackups()
    {
        string backupsPath = Path.Combine(_dataRoot, "backups");
        if (!Directory.Exists(backupsPath)) return new List<string>();
        return Directory.GetDirectories(backupsPath).Select(Path.GetFileName).OrderByDescending(x => x).ToList()!;
    }
}
