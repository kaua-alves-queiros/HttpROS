using HttpROS.Models;
using HttpROS.Services;
using Spectre.Console;

namespace HttpROS.Commands;

public class TargetCommand
{
    private readonly StorageService _storage;

    public TargetCommand(StorageService storage)
    {
        _storage = storage;
    }

    public void Execute(string[] args, RouteConfig activeRoute)
    {
        if (args.Length < 1)
        {
            AnsiConsole.MarkupLine("[red]Error: 'target' requer um valor.[/]");
            return;
        }

        activeRoute.Target = args[0];
        _storage.SaveRoute(activeRoute);
        AnsiConsole.MarkupLine($"[grey]Target definido para: {args[0]}[/]");
    }
}
