using System.Diagnostics;
using QueueManagement.Core;
using Xunit;
using Xunit.Abstractions;

namespace QueueManagement.Tests;

[Trait("Category", "Benchmark")]
public class CustomQueueBenchmarkTests
{
    private static readonly bool IsCiEnvironment =
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    private readonly ITestOutputHelper _output;

    public CustomQueueBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void Benchmark_Enqueue_Performance(int count)
    {
        var queue = new CustomQueue<string>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            queue.Enqueue($"Item {i}");
        }

        stopwatch.Stop();
        var avgTicks = (double)stopwatch.ElapsedTicks / count;

        _output.WriteLine($"Enqueue {count:N0} items: {stopwatch.ElapsedMilliseconds} ms ({avgTicks:F2} ticks/op)");

        Assert.Equal(count, queue.Count);
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void Benchmark_Dequeue_Performance(int count)
    {
        var queue = new CustomQueue<string>();
        for (int i = 0; i < count; i++)
        {
            queue.Enqueue($"Item {i}");
        }

        var stopwatch = Stopwatch.StartNew();

        while (!queue.IsEmpty())
        {
            queue.Dequeue();
        }

        stopwatch.Stop();
        var avgTicks = (double)stopwatch.ElapsedTicks / count;

        _output.WriteLine($"Dequeue {count:N0} items: {stopwatch.ElapsedMilliseconds} ms ({avgTicks:F2} ticks/op)");

        Assert.True(queue.IsEmpty());
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void Benchmark_CompareWithNativeQueue(int count)
    {
        // CustomQueue
        var customQueue = new CustomQueue<string>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
            customQueue.Enqueue($"Item {i}");
        while (!customQueue.IsEmpty())
            customQueue.Dequeue();

        stopwatch.Stop();
        var customTicks = stopwatch.ElapsedTicks;

        // Native Queue<T>
        var nativeQueue = new Queue<string>();
        stopwatch.Restart();

        for (int i = 0; i < count; i++)
            nativeQueue.Enqueue($"Item {i}");
        while (nativeQueue.Count > 0)
            nativeQueue.Dequeue();

        stopwatch.Stop();
        var nativeTicks = stopwatch.ElapsedTicks;

        var ratio = (double)customTicks / nativeTicks;

        _output.WriteLine($"Count: {count:N0}");
        _output.WriteLine($"  CustomQueue: {customTicks:N0} ticks");
        _output.WriteLine($"  Native Queue<T>: {nativeTicks:N0} ticks");
        _output.WriteLine($"  Ratio: {ratio:F2}x");

        // Shared CI runners are noisier than local machines, so keep a wider regression guard there.
        var maxAllowedRatio = IsCiEnvironment ? 10.0 : 3.0;
        Assert.True(ratio < maxAllowedRatio, $"CustomQueue is {ratio:F2}x slower than native, expected < {maxAllowedRatio:F1}x");
    }

    [Fact]
    public void Benchmark_Peek_IsConstantTime()
    {
        var queue = new CustomQueue<int>();
        for (int i = 0; i < 100_000; i++)
            queue.Enqueue(i);

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 10_000; i++)
        {
            _ = queue.Peek();
        }

        stopwatch.Stop();
        var avgTicks = (double)stopwatch.ElapsedTicks / 10_000;

        _output.WriteLine($"Peek 10,000 times: {stopwatch.ElapsedTicks} ticks ({avgTicks:F2} ticks/op)");

        // Keep a looser bound on shared runners where clock noise is higher.
        var maxAllowedTicks = IsCiEnvironment ? 1_000.0 : 100.0;
        Assert.True(avgTicks < maxAllowedTicks, $"Peek avg is {avgTicks:F2} ticks, expected < {maxAllowedTicks:F0} (O(1))");
    }
}
