using System.CommandLine;
using Silt.Core.SceneManagement;
using Silt.Metrics;

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
            DefaultValueFactory = _ => 30.0,
            Description = "Sample duration (seconds) for the meshing phase"
        };
        
        Option<int> benchmarkBatchRemeshWarmupIterationsOption = new("--benchmark-batch-remesh-warmup-iterations")
        {
            DefaultValueFactory = _ => 3,
            Description = "Number of full world remesh iterations for warmup"
        };
        
        Option<int> benchmarkBatchRemeshSampleIterationsOption = new("--benchmark-batch-remesh-sample-iterations")
        {
            DefaultValueFactory = _ => 5,
            Description = "Number of full world remesh iterations to sample for timing"
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

        Option<string> profilePhaseOption = new("--profile")
        {
            Description = "Enable dotTrace profiling for the defined phase. Launch the app under dotTrace with 'Do not profile on start'. " +
                          "Profiling data will be collected only during the defined sample phase and saved as a snapshot automatically. " +
                          "Requires --benchmark to be specified." +
                          $"Valid phases are: {string.Join(", ", ProfilePhaseExtensions.GetValidProfilePhases())}"
        };

        RootCommand root = new("Silt Rendering Engine")
        {
            benchmarkSceneOption,
            benchmarkOutOption,
            benchmarkWarmupMeshingSecondsOption,
            benchmarkSampleMeshingSecondsOption,
            benchmarkBatchRemeshWarmupIterationsOption,
            benchmarkBatchRemeshSampleIterationsOption,
            benchmarkWarmupRenderingSecondsOption,
            benchmarkSampleRenderingSecondsOption,
            profilePhaseOption
        };

        root.SetAction(parseResult =>
        {
            string? benchmarkScene = parseResult.GetValue(benchmarkSceneOption);
            string? profilePhase = parseResult.GetValue(profilePhaseOption);
            
            bool benchmarkEnabled = benchmarkScene != null;
            if (profilePhase != null && !benchmarkEnabled)
                throw new InvalidOperationException("The --profile option requires --benchmark to be specified.");

            AppOptions options = new()
            {
                BenchmarkEnabled = benchmarkEnabled,
                BenchmarkOutputFilePath = parseResult.GetValue(benchmarkOutOption),
                BenchmarkWarmUpMeshingSeconds = parseResult.GetValue(benchmarkWarmupMeshingSecondsOption),
                BenchmarkSampleMeshingSeconds = parseResult.GetValue(benchmarkSampleMeshingSecondsOption),
                BenchmarkBatchRemeshWarmupIterations = parseResult.GetValue(benchmarkBatchRemeshWarmupIterationsOption),
                BenchmarkBatchRemeshSampleIterations = parseResult.GetValue(benchmarkBatchRemeshSampleIterationsOption),
                BenchmarkWarmUpRenderingSeconds = parseResult.GetValue(benchmarkWarmupRenderingSecondsOption),
                BenchmarkSampleRenderingSeconds = parseResult.GetValue(benchmarkSampleRenderingSecondsOption),
                BenchmarkSceneId = benchmarkScene,
                TargetProfilePhase = profilePhase != null
                    ? ProfilePhaseExtensions.ParseProfilePhase(profilePhase)
                    : null
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