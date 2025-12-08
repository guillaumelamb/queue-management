# Quick Benchmark Run Guide

## ?? Quick Start

### 1. Run All Benchmarks (Recommended for first baseline)
```bash
cd C:\source\queue-management
dotnet run -c Release --project QueueManagement.Tests -- --filter *
```

This will run all benchmark categories and generate comprehensive reports.

**Estimated time**: 15-30 minutes

---

## ?? Run Individual Categories (Faster)

### Core Service Operations (~5 min)
```bash
# Queue Service (pre-registration, queue status)
dotnet run -c Release --project QueueManagement.Tests -- --filter *QueueService*

# Reservation Service (slot reservation, confirmation)
dotnet run -c Release --project QueueManagement.Tests -- --filter *Reservation*
```

### Cache & Storage (~5 min)
```bash
# Cache operations (capacity, ranks, rate limiting)
dotnet run -c Release --project QueueManagement.Tests -- --filter *Cache*

# Repository operations (database queries)
dotnet run -c Release --project QueueManagement.Tests -- --filter *Repository*
```

### Background Jobs (~10 min)
```bash
# Jobs (lottery, invitations, cache sync) - Tests with 100, 1K, 10K items
dotnet run -c Release --project QueueManagement.Tests -- --filter *Job*

# Fisher-Yates shuffle algorithm only
dotnet run -c Release --project QueueManagement.Tests -- --filter *Shuffle*
```

---

## ?? View Results

Results are saved in:
```
BenchmarkDotNet.Artifacts/results/
```

Files generated:
- `*.md` - Human-readable markdown summaries
- `*.html` - Interactive HTML reports  
- `*.csv` - Raw data for analysis

**Open in browser**:
```bash
start BenchmarkDotNet.Artifacts/results/*.html
```

---

## ?? What to Look For

### ? Speed Metrics
- **Mean**: Average time per operation
- **StdDev**: Consistency (lower is better)

### ?? Memory Metrics
- **Allocated**: Memory per operation
- **Gen0/1/2**: Garbage collection frequency

### ?? Red Flags
- Operations > 10ms for high-frequency paths
- High Gen2 collections (> 0)
- Large memory allocations in hot paths
- High standard deviation (inconsistent performance)

---

## ?? Document Baseline

After running benchmarks, save the results:

1. Copy markdown summaries to a baseline folder:
```bash
mkdir baseline-results
copy BenchmarkDotNet.Artifacts\results\*.md baseline-results\
```

2. Commit to git:
```bash
git add baseline-results/
git commit -m "Add performance baseline benchmarks"
```

---

## ?? Important Notes

1. **Always use Release mode** (`-c Release`)
2. **Close other applications** for accurate results
3. **Run on same machine** for valid comparisons
4. **Warm up first** - first run may be slower
5. **Check CPU usage** - should be stable during benchmarks

---

## ?? Troubleshooting

### Benchmark doesn't start?
```bash
# Verify Release build
dotnet build -c Release QueueManagement.Tests
```

### "No benchmarks found" error?
```bash
# Check filter syntax
dotnet run -c Release --project QueueManagement.Tests -- --list flat
```

### Slow performance?
- Close Visual Studio / IDEs
- Disable antivirus temporarily
- Ensure power mode is "High Performance"

---

## ?? Sample Expected Output

```
| Method                    | Mean      | Error    | Allocated |
|-------------------------- |----------:|---------:|----------:|
| CreatePreRegistration     | 125.3 ?s  | 2.1 ?s   | 1.2 KB    |
| GetQueueStatus            | 45.8 ?s   | 0.8 ?s   | 0.5 KB    |
| ComputeQueueRanks_10000   | 42.5 ms   | 1.2 ms   | 850 KB    |
| QueueGetRank              | 8.2 ?s    | 0.2 ?s   | 0.1 KB    |
```

**Legend:**
- ?s = microseconds (0.000001 seconds)
- ms = milliseconds (0.001 seconds)
- KB = kilobytes

---

## Next Steps After Baseline

1. ? Review results and identify bottlenecks
2. ?? Focus on high-frequency operations first
3. ?? Apply targeted optimizations
4. ? Re-run benchmarks to validate improvements
5. ?? Compare before/after results

---

**Need help?** Check `Benchmarks/README.md` for detailed documentation.
