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
            if (command == "show") { processor.HandleShowCommand(parts, activeRoute); continue; }

            // Mode Switching & Logic
            switch (currentMode)
            {
                case "view":
                    if (command == "exit" || command == "quit") running = false;
                    else if (command == "configure" || command == "conf")
                    {
                        currentMode = "config";
                        AnsiConsole.MarkupLine("[yellow]Entering configuration mode...[/]");
                    }
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
                            // Logic to delete the JSON file
                            string filePath = Path.Combine(command, $"{domain}.json");
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                                AnsiConsole.MarkupLine($"[red]Route {command} {domain} deleted.[/]");
                            }
                        }
                        else
                        {
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
                    else
                    {
                        processor.HandleRouteConfig(command, argsList, activeRoute!, isNoCommand);
                    }
                    break;
            }
        }
    }
}
