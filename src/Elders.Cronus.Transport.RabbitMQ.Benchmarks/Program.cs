using BenchmarkDotNet.Running;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Benchmark host. Run with:
/// <c>dotnet run -c Release --project src/Elders.Cronus.Transport.RabbitMQ.Benchmarks -- --filter "*"</c>.
/// </summary>
public static class Program
{
    /// <summary>
    /// Dispatches BenchmarkDotNet across every benchmark class in the assembly.
    /// </summary>
    /// <param name="args">Standard BenchmarkDotNet command-line arguments.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
