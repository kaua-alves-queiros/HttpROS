using System.Text;
using Spectre.Console;
using HttpROS.Models;
using HttpROS.CLI.Commands;
using HttpROS.Data;
using Microsoft.Extensions.Configuration;

namespace HttpROS.CLI;

public class ShellService
{
    private readonly StorageService _storage;
    private readonly HelpCommand _helpCommand;
    private readonly string _dataRoot;

    public ShellService(StorageService storage, IConfiguration configuration)
    {
        _storage = storage;
        _helpCommand = new HelpCommand();
        _dataRoot = configuration["Settings:DataPath"] ?? "Data";
    }

    public string ReadLineInteractive(string prompt, string mode, RouteConfig? activeRoute)
    {
        StringBuilder sb = new StringBuilder();
        Console.Write(prompt);

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.KeyChar == '?')
            {
                Console.WriteLine("?");
                _helpCommand.HandleHelpContextual(sb.ToString(), mode);
                Console.Write(prompt + sb.ToString());
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Enter) { Console.WriteLine(); return sb.ToString(); }
            if (keyInfo.Key == ConsoleKey.Backspace) { if (sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); } continue; }
            if (keyInfo.Key == ConsoleKey.Tab) { HandleTabCompletion(sb, mode, activeRoute); continue; }
            if (!char.IsControl(keyInfo.KeyChar)) { sb.Append(keyInfo.KeyChar); Console.Write(keyInfo.KeyChar); }
        }
    }

    private void HandleTabCompletion(StringBuilder sb, string mode, RouteConfig? activeRoute)
    {
        var fullText = sb.ToString();
        var parts = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> matches = new();
        
        bool endsWithSpace = fullText.EndsWith(" ");
        int partsCount = endsWithSpace ? parts.Length + 1 : parts.Length;
        string currentToken = endsWithSpace ? "" : (parts.Length > 0 ? parts.Last().ToLower() : "");

        if (partsCount <= 1)
        {
            var commands = GetAvailableCommands(mode);
            if (!commands.Contains("clear")) commands.Add("clear");
            if (!commands.Contains("show")) commands.Add("show");
            if (!commands.Contains("top")) commands.Add("top");
            matches = commands.Where(c => c.StartsWith(currentToken)).ToList();
        }
        else if (partsCount == 2)
        {
            string firstPart = parts[0].ToLower();
            if (firstPart == "show")
                matches = new List<string> { "routes", "proxy", "static", "redirect", "status", "version" }.Where(m => m.StartsWith(currentToken)).ToList();
            else if (firstPart == "monitor" && mode == "view")
                matches = new List<string> { "logs" }.Where(m => m.StartsWith(currentToken)).ToList();
            else if (mode == "config" && (firstPart == "proxy" || firstPart == "static" || firstPart == "redirect"))
            {
                string path = Path.Combine(_dataRoot, firstPart);
                matches = Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Select(Path.GetFileNameWithoutExtension).Where(d => d!.StartsWith(currentToken)).ToList()! : new List<string>();
            }
            else if (mode == "config" && firstPart == "no")
            {
                matches = new List<string> { "proxy", "static", "redirect" }.Where(m => m.StartsWith(currentToken)).ToList();
            }
            else if (mode == "config" && firstPart == "restore")
            {
                matches = _storage.GetBackups().Where(b => b.StartsWith(currentToken)).ToList();
            }
            else if (mode == "route-config")
            {
                if (firstPart == "ssl") matches = new List<string> { "lets-encrypt", "manual" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "ip") matches = new List<string> { "mode", "whitelist", "blacklist" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "balancer") matches = new List<string> { "method", "sticky", "upstream", "no" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "error-page") 
                {
                    matches = new List<string> { "404", "500", "502", "503", "504" }.Where(m => m.StartsWith(currentToken)).ToList();
                }
                else if (firstPart == "no") matches = new List<string> { "target", "ssl", "gzip", "auth", "ip", "balancer", "rate-limit" }.Where(m => m.StartsWith(currentToken)).ToList();
            }
            else if (mode == "balancer-config")
            {
                if (firstPart == "sticky") matches = new List<string> { "enable", "disable" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "method") matches = new List<string> { "round-robin", "least-conn", "ip-hash" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "health-check") matches = new List<string> { "interval", "timeout", "path" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "no") matches = new List<string> { "upstream", "sticky", "health-check" }.Where(m => m.StartsWith(currentToken)).ToList();
            }
            else if (mode == "error-page-config")
            {
                matches = new List<string> { "404", "500", "502", "503", "504", "no" }.Where(m => m.StartsWith(currentToken)).ToList();
            }
        }
        else if (partsCount == 3)
        {
            string firstPart = parts[0].ToLower();
            string secondPart = parts[1].ToLower();
            
            if (firstPart == "show" && (secondPart == "proxy" || secondPart == "static" || secondPart == "redirect"))
            {
                string path = Path.Combine(_dataRoot, secondPart);
                matches = Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Select(Path.GetFileNameWithoutExtension).Where(d => d!.StartsWith(currentToken)).ToList()! : new List<string>();
            }
            else if (mode == "config" && firstPart == "no" && (secondPart == "proxy" || secondPart == "static" || secondPart == "redirect"))
            {
                string path = Path.Combine(_dataRoot, secondPart);
                matches = Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Select(Path.GetFileNameWithoutExtension).Where(d => d!.StartsWith(currentToken)).ToList()! : new List<string>();
            }
            else if (mode == "route-config")
            {
                if (firstPart == "ssl" && secondPart == "manual")
                {
                    string path = Path.Combine(_dataRoot, "certs", "manual");
                    if (Directory.Exists(path))
                        matches = Directory.GetFiles(path).Select(Path.GetFileName).Where(f => f!.StartsWith(currentToken)).ToList()!;
                }
                else if (firstPart == "ip" && secondPart == "mode")
                    matches = new List<string> { "whitelist", "blacklist" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "balancer" && secondPart == "method")
                    matches = new List<string> { "round-robin", "least-conn", "ip-hash" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "balancer" && secondPart == "sticky")
                    matches = new List<string> { "enable", "disable" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "balancer" && secondPart == "no")
                    matches = new List<string> { "upstream", "sticky" }.Where(m => m.StartsWith(currentToken)).ToList();
                else if (firstPart == "no" && secondPart == "ip")
                    matches = new List<string> { "whitelist", "blacklist" }.Where(m => m.StartsWith(currentToken)).ToList();
            }
            else if (mode == "balancer-config" && firstPart == "no")
            {
                if (secondPart == "upstream") { /* No sub-completion for IPs yet */ }
                else if (secondPart == "health-check") { /* Cr completion */ }
            }
            else if (mode == "error-page-config")
            {
                if (firstPart == "no") matches = new List<string> { "404", "500", "502", "503", "504" }.Where(m => m.StartsWith(currentToken)).ToList();
                else 
                {
                    string path = Path.Combine(_dataRoot, "error-pages");
                    if (Directory.Exists(path))
                        matches = Directory.GetFiles(path, "*.html").Select(Path.GetFileNameWithoutExtension).Where(f => f!.StartsWith(currentToken)).ToList()!;
                }
            }
        }

        if (matches.Count == 1)
        {
            var completion = matches[0].Substring(currentToken.Length);
            sb.Append(completion);
            Console.Write(completion);
        }
        else if (matches.Count > 1)
        {
            Console.WriteLine();
            AnsiConsole.MarkupLine("[grey]" + string.Join("  ", matches) + "[/]");
            string promptPrefix = GetPromptPrefix(mode, activeRoute);
            Console.Write(promptPrefix + sb.ToString());
        }
    }

    private string GetPromptPrefix(string mode, RouteConfig? activeRoute)
    {
        return mode switch { 
            "config" => "HttpROS(config)# ", 
            "route-config" => $"HttpROS(config-route-{activeRoute?.Domain})# ", 
            "balancer-config" => $"HttpROS(config-route-{activeRoute?.Domain}-balancer)# ",
            "error-page-config" => $"HttpROS(config-route-{activeRoute?.Domain}-error-page)# ",
            _ => "HttpROS> " 
        };
    }

    public List<string> GetAvailableCommands(string mode)
    {
        return mode switch
        {
            "view" => new List<string> { "show", "configure", "exit", "quit", "clear", "status", "monitor" },
            "config" => new List<string> { "proxy", "static", "redirect", "backup", "restore", "exit", "quit", "return", "clear", "show", "top", "no" },
            "route-config" => new List<string> { "target", "code", "balancer", "ssl", "gzip", "auth", "ip", "websockets", "cors", "rate-limit", "error-page", "save", "exit", "quit", "clear", "show", "top" },
            "balancer-config" => new List<string> { "method", "sticky", "upstream", "health-check", "exit", "quit", "return", "clear", "show", "no", "top" },
            "error-page-config" => new List<string> { "404", "500", "502", "503", "504", "exit", "quit", "return", "clear", "show", "no", "top" },
            _ => new List<string>()
        };
    }
}
