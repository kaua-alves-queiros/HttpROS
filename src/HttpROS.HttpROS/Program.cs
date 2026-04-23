using HttpROS.Data;
using HttpROS.CLI;
using HttpROS.Engine;
using Microsoft.Extensions.Configuration;

namespace HttpROS;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var storage = new StorageService(config);
        var validator = new ValidationService(config);
        
        bool runEngine = args.Contains("--engine");
        bool runCli = args.Contains("--cli");

        if (!runEngine && !runCli)
        {
            runEngine = true;
            runCli = true;
        }

        ProxyEngine? engineProxy = null;
        if (runEngine)
        {
            engineProxy = new ProxyEngine(storage, validator);
            if (runCli)
            {
                // Engine starts in background thread if CLI is active
                engineProxy.Start(args);
            }
            else
            {
                engineProxy.Start(args);
                // In standalone engine mode, we need to keep the process alive
                // WebApplication.RunAsync handles this if we await it, but Start is custom.
                // Let's just wait indefinitely here or use a better signal.
                await Task.Delay(-1); 
                return;
            }
        }

        if (runCli)
        {
            var engineCli = new CliEngine(storage, validator, engineProxy, config);
            await engineCli.Run();
        }
    }
}
