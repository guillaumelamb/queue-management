using System;
using BenchmarkDotNet.Running;
using QueueManagement.Tests.Benchmarks;

namespace QueueManagement.Tests
{
    /// <summary>
    /// Main entry point for the test project.
    /// Supports running either benchmarks or tests.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--run-benchmarks")
            {
                var benchmarkType = args.Length > 1 ? args[1] : "All";
                BenchmarkConfig.RunBenchmarks(benchmarkType);
            }
            else if (args.Length > 0 && args[0].StartsWith("--"))
            {
                // BenchmarkDotNet command line args
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            }
            else
            {
                Console.WriteLine("Queue Management System - Test Runner");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  Run all benchmarks:");
                Console.WriteLine("    dotnet run -c Release --project QueueManagement.Tests -- --filter *");
                Console.WriteLine();
                Console.WriteLine("  Run specific benchmarks:");
                Console.WriteLine("    dotnet run -c Release --project QueueManagement.Tests -- --filter *QueueService*");
                Console.WriteLine("    dotnet run -c Release --project QueueManagement.Tests -- --filter *Cache*");
                Console.WriteLine();
                Console.WriteLine("  Or use custom runner:");
                Console.WriteLine("    dotnet run -c Release --project QueueManagement.Tests --run-benchmarks All");
                Console.WriteLine("    dotnet run -c Release --project QueueManagement.Tests --run-benchmarks QueueService");
                Console.WriteLine();
                Console.WriteLine("  Run tests:");
                Console.WriteLine("    dotnet test QueueManagement.Tests");
            }
        }
    }
}
