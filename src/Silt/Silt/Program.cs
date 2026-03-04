using System.CommandLine;
using Silt.SceneManagement;

namespace Silt;

using Serilog;
using Serilog.Events;

internal static class Program
{
    private static int Main(string[] args)
    {
        SetupLogging();

        RootCommand root = SetupRootCommand();

        ParseResult parseResult = root.Parse(args);
        return parseResult.Invoke();
    }


    private static RootCommand SetupRootCommand()
    {
        Option<string> benchmarkSceneOption = new("--benchmark")
        {
            Description = $"Run the specific scene in benchmark mode for a fixed duration, collect performance metrics, and output them to a file." +
                          $"Valid scene ids are: {string.Join(", ", SceneRegistry.CreateBenchmarks().SceneIds)}"
        };

        Option<string> benchmarkOutOption = new("--benchmark-out")
        {
            DefaultValueFactory = _ => "benchmark_results.txt",
            Description = "Benchmark output file path"
        };

        Option<double> benchmarkWarmupSecondsOption = new("--benchmark-warmup-seconds")
        {
            DefaultValueFactory = _ => 10.0,
            Description = "Warm-up duration in seconds"
        };

        Option<double> benchmarkSampleSecondsOption = new("--benchmark-sample-seconds")
        {
            DefaultValueFactory = _ => 50.0,
            Description = "Sample duration in seconds"
        };

        RootCommand root = new("Silt Rendering Engine")
        {
            benchmarkSceneOption,
            benchmarkOutOption,
            benchmarkWarmupSecondsOption,
            benchmarkSampleSecondsOption
        };

        root.SetAction(parseResult =>
        {
            AppOptions options = new()
            {
                BenchmarkEnabled = parseResult.GetValue(benchmarkSceneOption) != null,
                BenchmarkOutputFilePath = parseResult.GetValue(benchmarkOutOption),
                BenchmarkWarmUpSeconds = parseResult.GetValue(benchmarkWarmupSecondsOption),
                BenchmarkSampleSeconds = parseResult.GetValue(benchmarkSampleSecondsOption),
                BenchmarkSceneId = parseResult.GetValue(benchmarkSceneOption)
            };

            SiltEngine engine = new();
            engine.Run(options);
        });

        return root;
    }


    private static void SetupLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}