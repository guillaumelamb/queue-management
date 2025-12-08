using System;
using BenchmarkDotNet.Running;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmark configuration and utilities.
    /// 
    /// Usage:
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *QueueService*
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *Cache*
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *Job*
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *Repository*
    ///   dotnet run -c Release --project QueueManagement.Tests -- --filter *Shuffle*
    /// </summary>
    public static class BenchmarkConfig
    {
        public static void RunBenchmarks(string type)
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????????");
            Console.WriteLine("     QUEUE MANAGEMENT SYSTEM - PERFORMANCE BENCHMARKS");
            Console.WriteLine("     Baseline measurements before optimization");
            Console.WriteLine("???????????????????????????????????????????????????????????????????\n");
            Console.WriteLine($"Running benchmarks: {type}\n");

            switch (type.ToLowerInvariant())
            {
                case "queueservice":
                    BenchmarkRunner.Run<QueueServiceBenchmarks>();
                    break;

                case "reservation":
                case "reservationservice":
                    BenchmarkRunner.Run<ReservationServiceBenchmarks>();
                    break;

                case "cache":
                    BenchmarkRunner.Run<CacheBenchmarks>();
                    break;

                case "job":
                case "jobs":
                    BenchmarkRunner.Run<JobBenchmarks>();
                    break;

                case "shuffle":
                    BenchmarkRunner.Run<ShuffleBenchmarks>();
                    break;

                case "repository":
                case "repositories":
                    BenchmarkRunner.Run<RepositoryBenchmarks>();
                    break;

                case "all":
                    Console.WriteLine("??? Running QueueService Benchmarks ???\n");
                    BenchmarkRunner.Run<QueueServiceBenchmarks>();
                    
                    Console.WriteLine("\n??? Running ReservationService Benchmarks ???\n");
                    BenchmarkRunner.Run<ReservationServiceBenchmarks>();
                    
                    Console.WriteLine("\n??? Running Cache Benchmarks ???\n");
                    BenchmarkRunner.Run<CacheBenchmarks>();
                    
                    Console.WriteLine("\n??? Running Job Benchmarks ???\n");
                    BenchmarkRunner.Run<JobBenchmarks>();
                    
                    Console.WriteLine("\n??? Running Shuffle Benchmarks ???\n");
                    BenchmarkRunner.Run<ShuffleBenchmarks>();
                    
                    Console.WriteLine("\n??? Running Repository Benchmarks ???\n");
                    BenchmarkRunner.Run<RepositoryBenchmarks>();
                    break;

                default:
                    Console.WriteLine($"Unknown benchmark type: {type}");
                    Console.WriteLine("Available types: QueueService, Reservation, Cache, Job, Shuffle, Repository, All");
                    break;
            }
        }
    }
}