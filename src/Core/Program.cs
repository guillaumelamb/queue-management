using System;
using System.Linq;
using System.Runtime.CompilerServices;
using QueueManagement.Demo;

namespace QueueManagement.Core
{
    /// <summary>
    /// High-performance queue (FIFO) implementation using a ring buffer.
    /// Optimized for minimal allocations and maximum throughput.
    /// </summary>
    public sealed class CustomQueue<T>
    {
        private T[] buffer;
        private int head;      // Index of the first element
        private int tail;      // Index where next element will be inserted
        private int count;
        private const int DefaultCapacity = 16;

        public CustomQueue() : this(DefaultCapacity) { }

        public CustomQueue(int capacity)
        {
            if (capacity < 1) capacity = DefaultCapacity;
            // Use power of 2 for fast modulo with bitwise AND
            buffer = new T[RoundUpToPowerOf2(capacity)];
            head = 0;
            tail = 0;
            count = 0;
        }

        /// <summary>
        /// Returns the number of elements in the queue.
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        /// <summary>
        /// Checks if the queue is empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty() => count == 0;

        /// <summary>
        /// Adds an element to the end of the queue. O(1) amortized.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T element)
        {
            if (count == buffer.Length)
                Grow();

            buffer[tail] = element;
            tail = (tail + 1) & (buffer.Length - 1); // Fast modulo for power of 2
            count++;
        }

        /// <summary>
        /// Removes and returns the element at the front of the queue. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue()
        {
            if (count == 0)
                ThrowEmpty();

            T element = buffer[head];
            buffer[head] = default!; // Help GC for reference types
            head = (head + 1) & (buffer.Length - 1);
            count--;
            return element;
        }

        /// <summary>
        /// Returns the element at the front without removing it. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            if (count == 0)
                ThrowEmpty();
            return buffer[head];
        }

        /// <summary>
        /// Attempts to dequeue an element. Returns false if queue is empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T? result)
        {
            if (count == 0)
            {
                result = default;
                return false;
            }

            result = buffer[head];
            buffer[head] = default!;
            head = (head + 1) & (buffer.Length - 1);
            count--;
            return true;
        }

        /// <summary>
        /// Attempts to peek at the front element. Returns false if queue is empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T? result)
        {
            if (count == 0)
            {
                result = default;
                return false;
            }
            result = buffer[head];
            return true;
        }

        /// <summary>
        /// Removes all elements from the queue.
        /// </summary>
        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                // Clear references to help GC
                if (head < tail)
                {
                    Array.Clear(buffer, head, count);
                }
                else if (count > 0)
                {
                    Array.Clear(buffer, head, buffer.Length - head);
                    Array.Clear(buffer, 0, tail);
                }
            }
            head = 0;
            tail = 0;
            count = 0;
        }

        /// <summary>
        /// Ensures the queue has at least the specified capacity.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            if (buffer.Length < capacity)
            {
                int newCapacity = RoundUpToPowerOf2(capacity);
                SetCapacity(newCapacity);
            }
        }

        public override string ToString() => $"Queue[{count}]";

        #region Private Methods

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow()
        {
            int newCapacity = buffer.Length * 2;
            if (newCapacity < 0) newCapacity = int.MaxValue; // Overflow protection
            SetCapacity(newCapacity);
        }

        private void SetCapacity(int newCapacity)
        {
            T[] newBuffer = new T[newCapacity];

            if (count > 0)
            {
                if (head < tail)
                {
                    Array.Copy(buffer, head, newBuffer, 0, count);
                }
                else
                {
                    // Wrap-around case
                    int headToEnd = buffer.Length - head;
                    Array.Copy(buffer, head, newBuffer, 0, headToEnd);
                    Array.Copy(buffer, 0, newBuffer, headToEnd, tail);
                }
            }

            buffer = newBuffer;
            head = 0;
            tail = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundUpToPowerOf2(int value)
        {
            // Bit manipulation to round up to nearest power of 2
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowEmpty() =>
            throw new InvalidOperationException("The queue is empty.");

        #endregion
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== HIGH-PERFORMANCE QUEUE BENCHMARK ===");
            Console.WriteLine("Comparing CustomQueue (Ring Buffer) vs Native Queue<T>\n");

            // Warmup run to eliminate JIT compilation overhead
            Console.WriteLine("Warming up JIT...");
            WarmUp();
            Console.WriteLine("Warmup complete.\n");

            Console.WriteLine("Select mode:");
            Console.WriteLine("  1. Performance benchmark (50K vs 2.5M)");
            Console.WriteLine("  2. Virtual Waiting Room simulation (legacy)");
            Console.WriteLine("  3. Complete Queue Management System (spec-compliant)");
            Console.Write("\nChoice [1/2/3]: ");
            
            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            if (choice == "2")
            {
                RegistrationSimulation.Run();
            }
            else if (choice == "3")
            {
                QueueManagement.Demo.RegistrationSystemDemo.RunAsync().GetAwaiter().GetResult();
            }
            else
            {
                RunBenchmark(50_000);
                RunBenchmark(2_500_000);
            }

            Console.WriteLine("=== COMPLETE ===");
        }

        static void WarmUp()
        {
            // Pre-JIT both queue implementations
            var custom = new CustomQueue<int>(1000);
            var native = new System.Collections.Generic.Queue<int>(1000);
            
            for (int i = 0; i < 1000; i++)
            {
                custom.Enqueue(i);
                native.Enqueue(i);
            }
            for (int i = 0; i < 1000; i++)
            {
                custom.Dequeue();
                native.Dequeue();
            }
        }

        static void RunBenchmark(int count)
        {
            const int runs = 5;
            Console.WriteLine($"--- {count:N0} clients - {runs} runs ---\n");

            long[] enqueueCustomTicks = new long[runs];
            long[] dequeueCustomTicks = new long[runs];
            long[] enqueueNativeTicks = new long[runs];
            long[] dequeueNativeTicks = new long[runs];

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int run = 0; run < runs; run++)
            {
                // CustomQueue with pre-allocated capacity
                var customQueue = new CustomQueue<int>(count);
                
                sw.Restart();
                for (int i = 0; i < count; i++)
                    customQueue.Enqueue(i);
                sw.Stop();
                enqueueCustomTicks[run] = sw.ElapsedTicks;

                sw.Restart();
                while (!customQueue.IsEmpty())
                    customQueue.Dequeue();
                sw.Stop();
                dequeueCustomTicks[run] = sw.ElapsedTicks;

                // Native Queue<T> with capacity
                var nativeQueue = new System.Collections.Generic.Queue<int>(count);
                
                sw.Restart();
                for (int i = 0; i < count; i++)
                    nativeQueue.Enqueue(i);
                sw.Stop();
                enqueueNativeTicks[run] = sw.ElapsedTicks;

                sw.Restart();
                while (nativeQueue.Count > 0)
                    nativeQueue.Dequeue();
                sw.Stop();
                dequeueNativeTicks[run] = sw.ElapsedTicks;
            }

            // Calculate statistics
            double ticksPerMicrosecond = System.Diagnostics.Stopwatch.Frequency / 1_000_000.0;

            Console.WriteLine("| Run | Custom Enqueue | Native Enqueue | Custom Dequeue | Native Dequeue |");
            Console.WriteLine("|-----|----------------|----------------|----------------|----------------|");
            
            for (int run = 0; run < runs; run++)
            {
                Console.WriteLine($"| {run + 1}   | {enqueueCustomTicks[run],12:N0} t  | {enqueueNativeTicks[run],12:N0} t  | {dequeueCustomTicks[run],12:N0} t  | {dequeueNativeTicks[run],12:N0} t  |");
            }

            // Averages (excluding first run to remove any remaining warmup effects)
            double avgEnqueueCustom = enqueueCustomTicks.Skip(1).Average();
            double avgDequeueCustom = dequeueCustomTicks.Skip(1).Average();
            double avgEnqueueNative = enqueueNativeTicks.Skip(1).Average();
            double avgDequeueNative = dequeueNativeTicks.Skip(1).Average();

            Console.WriteLine();
            Console.WriteLine("=== SUMMARY (average of runs 2-5) ===");
            Console.WriteLine();
            Console.WriteLine($"CustomQueue (Ring Buffer):");
            Console.WriteLine($"  Enqueue: {avgEnqueueCustom:N0} ticks ({avgEnqueueCustom / ticksPerMicrosecond:N2} µs) - {avgEnqueueCustom / count:F4} ticks/op");
            Console.WriteLine($"  Dequeue: {avgDequeueCustom:N0} ticks ({avgDequeueCustom / ticksPerMicrosecond:N2} µs) - {avgDequeueCustom / count:F4} ticks/op");
            Console.WriteLine();
            Console.WriteLine($"Native Queue<T>:");
            Console.WriteLine($"  Enqueue: {avgEnqueueNative:N0} ticks ({avgEnqueueNative / ticksPerMicrosecond:N2} µs) - {avgEnqueueNative / count:F4} ticks/op");
            Console.WriteLine($"  Dequeue: {avgDequeueNative:N0} ticks ({avgDequeueNative / ticksPerMicrosecond:N2} µs) - {avgDequeueNative / count:F4} ticks/op");
            Console.WriteLine();
            Console.WriteLine("=== PERFORMANCE COMPARISON ===");
            Console.WriteLine();

            string enqueueResult = avgEnqueueCustom < avgEnqueueNative
                ? $"✓ CustomQueue is {avgEnqueueNative / avgEnqueueCustom:F2}x FASTER"
                : $"✗ Native Queue is {avgEnqueueCustom / avgEnqueueNative:F2}x faster";
            
            string dequeueResult = avgDequeueCustom < avgDequeueNative
                ? $"✓ CustomQueue is {avgDequeueNative / avgDequeueCustom:F2}x FASTER"
                : $"✗ Native Queue is {avgDequeueCustom / avgDequeueNative:F2}x faster";

            Console.WriteLine($"Enqueue: {enqueueResult}");
            Console.WriteLine($"Dequeue: {dequeueResult}");
            Console.WriteLine();

            double totalCustom = avgEnqueueCustom + avgDequeueCustom;
            double totalNative = avgEnqueueNative + avgDequeueNative;
            
            string totalResult = totalCustom < totalNative
                ? $"✓ CustomQueue is {totalNative / totalCustom:F2}x FASTER overall"
                : $"✗ Native Queue is {totalCustom / totalNative:F2}x faster overall";
            
            Console.WriteLine($"TOTAL:   {totalResult}");
            Console.WriteLine();
        }
    }
}
