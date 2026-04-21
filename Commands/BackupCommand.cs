using Spectre.Console;

namespace HttpROS.Commands;

public class BackupCommand
{
    public void Execute()
    {
        AnsiConsole.Status()
            .Start("Gerando backup...", ctx => {
                // Simulação de backup
                System.Threading.Thread.Sleep(1000);
                AnsiConsole.MarkupLine("[green]Backup das configurações gerado com sucesso![/]");
            });
    }
}
