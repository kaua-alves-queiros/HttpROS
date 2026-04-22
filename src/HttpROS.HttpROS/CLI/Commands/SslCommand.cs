using HttpROS.Models;
using HttpROS.Data;
using Spectre.Console;

namespace HttpROS.CLI.Commands;

public class SslCommand
{
    private readonly StorageService _storage;

    public SslCommand(StorageService storage)
    {
        _storage = storage;
    }

    public void Execute(string[] args, RouteConfig activeRoute, bool isNo)
    {
        if (isNo)
        {
            activeRoute.Features.Ssl.Enabled = false;
            _storage.SaveRoute(activeRoute);
            AnsiConsole.MarkupLine("[grey]SSL desativado.[/]");
            return;
        }

        if (args.Length == 0 || args[0].ToLower() == "lets-encrypt")
        {
            activeRoute.Features.Ssl.Enabled = true;
            activeRoute.Features.Ssl.Provider = "lets-encrypt";
            activeRoute.Features.Ssl.CertName = null;
            AnsiConsole.MarkupLine("[grey]SSL ativado via Let's Encrypt.[/]");
        }
        else if (args[0].ToLower() == "manual")
        {
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error: 'ssl manual' requer o nome do certificado.[/]");
                return;
            }
            activeRoute.Features.Ssl.Enabled = true;
            activeRoute.Features.Ssl.Provider = "manual";
            activeRoute.Features.Ssl.CertName = args[1];
            AnsiConsole.MarkupLine($"[grey]SSL ativado via certificado manual: {args[1]}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error: Argumento de SSL inválido. Use 'lets-encrypt' ou 'manual <nome>'.[/]");
            return;
        }

        _storage.SaveRoute(activeRoute);
    }
}
