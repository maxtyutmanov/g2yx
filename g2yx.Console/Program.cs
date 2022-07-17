using g2yx.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

using Console = System.Console;

namespace g2yx.ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: g2yx <takeout_directory_path> <yandex_access_token>");
                return;
            }

            var takeoutDirPath = args[0];
            var yandexAccessToken = args[1];

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, ea) =>
            {
                // Tell .NET to not terminate the process
                ea.Cancel = true;

                Console.WriteLine("Received SIGINT (Ctrl+C)");
                cts.Cancel();
            };

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Default", LogLevel.Information)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddConsole();
            });

            try
            {
                var logger = loggerFactory.CreateLogger<SyncJob>();
                var syncJob = new SyncJob(logger);
                await syncJob.SyncFromGTakeout(takeoutDirPath, yandexAccessToken, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) {}
        }
    }
}
