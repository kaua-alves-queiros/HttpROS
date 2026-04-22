using HttpROS.Data;
using HttpROS.CLI;
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
        var engine = new CliEngine(storage, validator, config);
        engine.Run();
    }
}
