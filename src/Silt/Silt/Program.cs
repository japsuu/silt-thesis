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

        Option<double> benchmarkWarmupMeshingSecondsOption = new("--benchmark-warmup-meshing-seconds")
        {
            DefaultValueFactory = _ => 10.0,
            Description = "Warm-up duration (seconds) for the meshing phase"
        };

        Option<double> benchmarkSampleMeshingSecondsOption = new("--benchmark-sample-meshing-seconds")
        {
            DefaultValueFactory = _ => 10.0,
            Description = "Sample duration (seconds) for the meshing phase"
        };

        Option<double> benchmarkWarmupRenderingSecondsOption = new("--benchmark-warmup-rendering-seconds")
        {
            DefaultValueFactory = _ => 10.0,
            Description = "Warm-up duration (seconds) for the rendering phase"
        };

        Option<double> benchmarkSampleRenderingSecondsOption = new("--benchmark-sample-rendering-seconds")
        {
            DefaultValueFactory = _ => 30.0,
            Description = "Sample duration (seconds) for the rendering phase"
        };

        RootCommand root = new("Silt Rendering Engine")
        {
            benchmarkSceneOption,
            benchmarkOutOption,
            benchmarkWarmupMeshingSecondsOption,
            benchmarkSampleMeshingSecondsOption,
            benchmarkWarmupRenderingSecondsOption,
            benchmarkSampleRenderingSecondsOption
        };

        root.SetAction(parseResult =>
        {
            AppOptions options = new()
            {
                BenchmarkEnabled = parseResult.GetValue(benchmarkSceneOption) != null,
                BenchmarkOutputFilePath = parseResult.GetValue(benchmarkOutOption),
                BenchmarkWarmUpMeshingSeconds = parseResult.GetValue(benchmarkWarmupMeshingSecondsOption),
                BenchmarkSampleMeshingSeconds = parseResult.GetValue(benchmarkSampleMeshingSecondsOption),
                BenchmarkWarmUpRenderingSeconds = parseResult.GetValue(benchmarkWarmupRenderingSecondsOption),
                BenchmarkSampleRenderingSeconds = parseResult.GetValue(benchmarkSampleRenderingSecondsOption),
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