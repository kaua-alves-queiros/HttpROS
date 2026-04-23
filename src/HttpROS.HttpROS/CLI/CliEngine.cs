using Spectre.Console;
using HttpROS.Models;
using HttpROS.Data;
using HttpROS.CLI.Commands;
using HttpROS.Engine;
using Microsoft.Extensions.Configuration;

namespace HttpROS.CLI;

public class CliEngine
{
    private readonly StorageService _storage;
    private readonly ValidationService _validator;
    private readonly ProxyEngine? _proxyEngine;
    private readonly ShellService _shell;
    private readonly CommandProcessor _processor;
    
    public string CurrentMode { get; private set; } = "view";
    public RouteConfig? ActiveRoute { get; private set; }
    public bool IsRunning { get; private set; } = true;

    public CliEngine(StorageService storage, ValidationService validator, ProxyEngine? proxyEngine, IConfiguration configuration)
    {
        _storage = storage;
        _validator = validator;
        _proxyEngine = proxyEngine;
        _shell = new ShellService(storage, configuration);
        _processor = new CommandProcessor(storage, validator, proxyEngine);
    }

    public CliEngine(StorageService storage, ValidationService validator, IConfiguration configuration)
        : this(storage, validator, null, configuration) { }

    public async Task ProcessInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool isNoCommand = parts[0] == "no";
        var command = isNoCommand ? (parts.Length > 1 ? parts[1] : "") : parts[0];
        var argsList = isNoCommand ? parts.Skip(2).ToArray() : parts.Skip(1).ToArray();

        if (string.IsNullOrEmpty(command)) return;

        if (command == "clear") { Console.Clear(); return; }
        if (command == "show") { _processor.HandleShowCommand(parts, CurrentMode, ActiveRoute); return; }
        if (command == "top") { CurrentMode = "view"; ActiveRoute = null; return; }

        switch (CurrentMode)
        {
            case "view": await HandleViewMode(command, argsList, isNoCommand); break;
            case "config": HandleConfigMode(command, argsList, isNoCommand); break;
            case "route-config": HandleRouteConfigMode(command, argsList, isNoCommand); break;
            case "balancer-config": HandleBalancerConfigMode(command, argsList, isNoCommand); break;
            case "error-page-config": HandleErrorPageConfigMode(command, argsList, isNoCommand); break;
        }
    }

    private async Task HandleViewMode(string command, string[] argsList, bool isNoCommand)
    {
        if (command == "configure" || command == "conf") { CurrentMode = "config"; AnsiConsole.MarkupLine("[yellow]Entering configuration mode...[/]"); }
        else if (command == "monitor" && argsList.Length > 0 && argsList[0] == "logs") _processor.HandleMonitorLogs();
        else if (command == "status") _processor.HandleShowCommand(new[] { "show", "status" }, CurrentMode, ActiveRoute);
        else if (command == "engine") await _processor.HandleEngineCommand(argsList);
        else if (command == "exit" || command == "quit") IsRunning = false;
        else AnsiConsole.MarkupLine($"[red]Error: Unknown command '{command}'.[/]");
    }

    private void HandleConfigMode(string command, string[] argsList, bool isNoCommand)
    {
        if (command == "exit" || command == "quit" || command == "return") CurrentMode = "view";
        else if (command == "proxy" || command == "static" || command == "redirect")
        {
            if (argsList.Length < 1) { AnsiConsole.MarkupLine("[red]Error: Domain required.[/]"); return; }
            string domain = argsList[0];
            if (!_validator.IsValidDomain(domain)) { AnsiConsole.MarkupLine($"[red]Error: Domínio '{domain}' inválido.[/]"); return; }

            if (isNoCommand)
            {
                _storage.DeleteRoute(command, domain);
                AnsiConsole.MarkupLine($"[grey]Rota {command} {domain} removida.[/]");
            }
            else
            {
                var conflict = _storage.FindConflictingRoute(domain, command);
                if (conflict != null) { AnsiConsole.MarkupLine($"[red]Error: O dominio '{domain}' ja esta configurado como '{conflict.Type}'.[/]"); return; }

                ActiveRoute = _storage.LoadRoute(command, domain) ?? new RouteConfig { Domain = domain, Type = command };
                _storage.SaveRoute(ActiveRoute);
                CurrentMode = "route-config";
            }
        }
        else if (command == "backup") _processor.HandleBackup();
        else if (command == "restore") _processor.HandleRestore(argsList);
        else AnsiConsole.MarkupLine($"[red]Error: Unknown command '{command}'.[/]");
    }

    private void HandleRouteConfigMode(string command, string[] argsList, bool isNoCommand)
    {
        if (command == "exit" || command == "quit" || command == "return") { CurrentMode = "config"; ActiveRoute = null; }
        else if (command == "save") { _storage.SaveRoute(ActiveRoute!); CurrentMode = "config"; ActiveRoute = null; }
        else if (command == "balancer" && !isNoCommand) CurrentMode = "balancer-config";
        else if (command == "error-page" && !isNoCommand && argsList.Length == 0) CurrentMode = "error-page-config";
        else _processor.HandleRouteConfig(command, argsList, ActiveRoute!, isNoCommand);
    }

    private void HandleBalancerConfigMode(string command, string[] argsList, bool isNoCommand)
    {
        if (command == "exit" || command == "quit") CurrentMode = "route-config";
        else if (command == "return") { CurrentMode = "config"; ActiveRoute = null; }
        else _processor.HandleBalancerConfig(command, argsList, ActiveRoute!, isNoCommand);
    }

    private void HandleErrorPageConfigMode(string command, string[] argsList, bool isNoCommand)
    {
        if (command == "exit" || command == "quit") CurrentMode = "route-config";
        else if (command == "return") { CurrentMode = "config"; ActiveRoute = null; }
        else _processor.HandleErrorPageConfig(command, argsList, ActiveRoute!, isNoCommand);
    }

    public async Task Run()
    {
        AnsiConsole.Write(new FigletText("HttpROS").Color(Color.Green));
        AnsiConsole.MarkupLine("[grey]Http Router Operation Sistem - v0.1.0[/]");
        AnsiConsole.WriteLine();

        while (IsRunning)
        {
            string promptPrefix = GetPrompt();
            string input = _shell.ReadLineInteractive(promptPrefix, CurrentMode, ActiveRoute);
            await ProcessInput(input);
        }
    }

    private string GetPrompt()
    {
        return CurrentMode switch
        {
            "config" => "HttpROS(config)# ",
            "route-config" => $"HttpROS(config-route-{ActiveRoute?.Domain})# ",
            "balancer-config" => $"HttpROS(config-route-{ActiveRoute?.Domain}-balancer)# ",
            "error-page-config" => $"HttpROS(config-route-{ActiveRoute?.Domain}-error-page)# ",
            _ => "HttpROS> "
        };
    }
}
