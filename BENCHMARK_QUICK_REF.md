# ?? Benchmark Quick Reference

## ? Status: FIXED & READY

The benchmark suite is now fully functional after fixing the async IterationSetup issue.

---

## ?? Quick Commands

### Fast Test (2-5 min) - Verify Everything Works
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Shuffle* --job short
```

### Full Baseline (15-30 min) - Complete Performance Profile
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *
```

---

## ?? Individual Benchmark Categories

| Category | Command | What It Measures | Est. Time |
|----------|---------|------------------|-----------|
| **Queue Service** | `--filter *QueueService*` | Pre-registration, queue status | ~5 min |
| **Reservation** | `--filter *Reservation*` | Slot reservation, confirmation | ~5 min |
| **Cache** | `--filter *Cache*` | Capacity, ranks, rate limits | ~5 min |
| **Jobs** | `--filter *Job*` | Lottery, invitations (100/1K/10K) | ~10 min |
| **Shuffle** | `--filter *Shuffle*` | Fisher-Yates algorithm | ~3 min |
| **Repository** | `--filter *Repository*` | Database queries | ~5 min |

---

## ?? Sample Results (Shuffle Benchmark)

```
| Method              | Mean       | Allocated |
|-------------------- |-----------:|----------:|
| Shuffle_100_Items   |   5.232 us |      32 B |
| Shuffle_1000_Items  |  54.690 us |      32 B |
| Shuffle_10000_Items | 531.437 us |      32 B |
```

**Analysis:**
- ? O(n) complexity confirmed (10x items = 10x time)
- ? Minimal memory allocations
- ? Ready for 10,000 participant lottery

---

## ?? Performance Targets

| Operation | Current | Target | Status |
|-----------|---------|--------|--------|
| Shuffle (10K) | ~531 ?s | < 1 ms | ? Excellent |
| Pre-registration | TBD | 1K-5K ops/sec | ?? Measure |
| Cache lookup | TBD | < 10 ?s | ?? Measure |
| Reservation | TBD | 500-1K ops/sec | ?? Measure |

---

## ?? Results Location

```
BenchmarkDotNet.Artifacts/results/
??? *.md      # Human-readable summaries
??? *.html    # Interactive reports
??? *.csv     # Raw data
```

**Open reports:**
```bash
start BenchmarkDotNet.Artifacts\results\*.html
```

---

## ?? Save Baseline

```bash
# Create baseline directory
mkdir baseline-results

# Copy results
copy BenchmarkDotNet.Artifacts\results\*.md baseline-results\

# Commit to git
git add baseline-results/
git commit -m "Add performance baseline benchmarks"
```

---

## ?? What to Look For

### ? Speed
- Mean < 1ms for hot paths
- Low StdDev (consistency)

### ?? Memory
- Minimal allocations
- No Gen2 collections

### ?? Red Flags
- Operations > 10ms
- High Gen2 collections
- Large allocations

---

## ??? Troubleshooting

**Slow performance?**
- Close Visual Studio
- Close browsers
- Set power mode to "High Performance"

**Build errors?**
```bash
dotnet clean
dotnet build -c Release
```

**Results not appearing?**
Check: `BenchmarkDotNet.Artifacts/results/`

---

## ?? Documentation

- **Full Guide**: `QueueManagement.Tests/Benchmarks/README.md`
- **Quick Start**: `QueueManagement.Tests/Benchmarks/QUICKSTART.md`
- **Summary**: `QueueManagement.Tests/Benchmarks/SUMMARY.md`
- **Fix Notes**: `BENCHMARK_FIX.md`

---

## ? Next Steps

1. **Run Shuffle Test** (verify setup) - 3 min ?
2. **Run Cache Benchmarks** (quick feedback) - 5 min
3. **Run Full Suite** (complete baseline) - 30 min
4. **Analyze Results** (identify bottlenecks)
5. **Document Findings**
6. **Plan Optimizations**

---

**Ready to run!** ??

Start with: `dotnet run -c Release --project QueueManagement.Tests -- --filter *Cache*`
