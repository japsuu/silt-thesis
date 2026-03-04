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
            Description = $"Run the specific scene in benchmark mode for a fixed number of frames, collect performance metrics, and output them to a file." +
                          $"Valid scene ids are: {string.Join(", ", SceneRegistry.CreateBenchmarks().SceneIds)}"
        };

        Option<string> benchmarkOutOption = new("--benchmark-out")
        {
            DefaultValueFactory = _ => "benchmark_results.txt",
            Description = "Benchmark output file path"
        };

        Option<int> benchmarkWarmupOption = new("--benchmark-warmup")
        {
            DefaultValueFactory = _ => 20_000,
            Description = "Warm-up frame count"
        };

        Option<int> benchmarkSamplesOption = new("--benchmark-samples")
        {
            DefaultValueFactory = _ => 100_000,
            Description = "Sample frame count"
        };

        RootCommand root = new("Silt Rendering Engine")
        {
            benchmarkSceneOption,
            benchmarkOutOption,
            benchmarkWarmupOption,
            benchmarkSamplesOption
        };

        root.SetAction(parseResult =>
        {
            AppOptions options = new()
            {
                BenchmarkEnabled = parseResult.GetValue(benchmarkSceneOption) != null,
                BenchmarkOutputFilePath = parseResult.GetValue(benchmarkOutOption),
                BenchmarkWarmUpFrameCount = parseResult.GetValue(benchmarkWarmupOption),
                BenchmarkSampleFrameCount = parseResult.GetValue(benchmarkSamplesOption),
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