using HttpROS.Data;
using HttpROS.CLI;
using HttpROS.Engine;
using Microsoft.Extensions.Configuration;

namespace HttpROS;

class Program
{
    static void Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var storage = new StorageService(config);
        var validator = new ValidationService(config);
        
        // If --cli-only is passed, we skip the Proxy Engine (useful for SSH management)
        bool cliOnly = args.Contains("--cli-only");

        ProxyEngine? engineProxy = null;
        if (!cliOnly)
        {
            engineProxy = new ProxyEngine(storage, validator);
            engineProxy.Start(args);
        }

        // Start the Control Plane (CLI Engine)
        var engineCli = new CliEngine(storage, validator, engineProxy!, config);
        engineCli.Run();
    }
}
