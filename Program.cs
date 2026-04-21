using Spectre.Console;
using HttpROS.Models;
using HttpROS.Services;
using HttpROS.Commands;

namespace HttpROS;

class Program
{
    static void Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("HttpROS").Color(Color.Green));
        AnsiConsole.MarkupLine("[grey]Http Router Operation Sistem - v0.1.0[/]");
        AnsiConsole.MarkupLine("[grey]Inspirado em Network OS (Datacom/Huawei style)[/]");
        AnsiConsole.WriteLine();

        var storage = new StorageService();
        var shell = new ShellService(storage);
        var processor = new CommandProcessor(storage);

        bool running = true;
        string currentMode = "view"; 
        RouteConfig? activeRoute = null;

        while (running)
        {
            string promptPrefix = currentMode switch
            {
                "config" => "HttpROS(config)# ",
                "route-config" => $"HttpROS(config-route-{activeRoute?.Domain})# ",
                "balancer-config" => $"HttpROS(config-route-{activeRoute?.Domain}-balancer)# ",
                _ => "HttpROS> "
            };

            string input = shell.ReadLineInteractive(promptPrefix, currentMode, activeRoute);
            if (string.IsNullOrWhiteSpace(input)) continue;

            var parts = input.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool isNoCommand = parts[0] == "no";
            var command = isNoCommand ? (parts.Length > 1 ? parts[1] : "") : parts[0];
            var argsList = isNoCommand ? parts.Skip(2).ToArray() : parts.Skip(1).ToArray();

            if (string.IsNullOrEmpty(command)) continue;

            // Global Commands
            if (command == "clear") { Console.Clear(); continue; }
            if (command == "show") { processor.HandleShowCommand(parts, currentMode, activeRoute); continue; }
            if (command == "top") { currentMode = "view"; activeRoute = null; continue; }

            // Mode Switching & Logic
            switch (currentMode)
            {
                case "view":
                    if (command == "configure" || command == "conf")
                    {
                        currentMode = "config";
                        AnsiConsole.MarkupLine("[yellow]Entering configuration mode...[/]");
                    }
                    else if (command == "status") processor.HandleShowCommand(new[] { "show", "status" }, currentMode, activeRoute);
                    else if (command == "exit" || command == "quit") running = false;
                    else AnsiConsole.MarkupLine($"[red]Error: Unknown command '{command}'.[/]");
                    break;

                case "config":
                    if (command == "exit" || command == "quit" || command == "return") currentMode = "view";
                    else if (command == "proxy" || command == "static" || command == "redirect")
                    {
                        if (argsList.Length < 1) { AnsiConsole.MarkupLine("[red]Error: Domain required.[/]"); continue; }
                        string domain = argsList[0];

                        if (isNoCommand)
                        {
                            storage.DeleteRoute(command, domain);
                            AnsiConsole.MarkupLine($"[grey]Rota {command} {domain} removida.[/]");
                        }
                        else
                        {
                            var conflict = storage.FindConflictingRoute(domain, command);
                            if (conflict != null)
                            {
                                AnsiConsole.MarkupLine($"[red]Error: O dominio '{domain}' ja esta configurado como '{conflict.Type}'.[/]");
                                AnsiConsole.MarkupLine($"[red]Remova a rota existente antes de criar uma nova do tipo '{command}'.[/]");
                                continue;
                            }

                            activeRoute = storage.LoadRoute(command, domain) ?? new RouteConfig { Domain = domain, Type = command };
                            storage.SaveRoute(activeRoute);
                            currentMode = "route-config";
                        }
                    }
                    else if (command == "backup") processor.HandleBackup();
                    else if (command == "restore") processor.HandleRestore();
                    else AnsiConsole.MarkupLine($"[red]Error: Unknown command '{command}'.[/]");
                    break;

                case "route-config":
                    if (command == "exit" || command == "quit")
                    {
                        currentMode = "config";
                        activeRoute = null;
                    }
                    else if (command == "save")
                    {
                        storage.SaveRoute(activeRoute!);
                        currentMode = "config";
                        activeRoute = null;
                    }
                    else if (command == "balancer" && !isNoCommand)
                    {
                        currentMode = "balancer-config";
                    }
                    else if (command == "error-page" && !isNoCommand && argsList.Length == 0)
                    {
                        currentMode = "error-page-config";
                    }
                    else
                    {
                        processor.HandleRouteConfig(command, argsList, activeRoute!, isNoCommand);
                    }
                    break;

                case "balancer-config":
                    if (command == "exit" || command == "quit")
                    {
                        currentMode = "route-config";
                    }
                    else if (command == "return")
                    {
                        currentMode = "config";
                        activeRoute = null;
                    }
                    else
                    {
                        processor.HandleBalancerConfig(command, argsList, activeRoute!, isNoCommand);
                    }
                    break;

                case "error-page-config":
                    if (command == "exit" || command == "quit")
                    {
                        currentMode = "route-config";
                    }
                    else if (command == "return")
                    {
                        currentMode = "config";
                        activeRoute = null;
                    }
                    else
                    {
                        processor.HandleErrorPageConfig(command, argsList, activeRoute!, isNoCommand);
                    }
                    break;
            }
        }
    }
}
