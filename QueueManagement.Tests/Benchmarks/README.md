# Queue Management System - Performance Benchmarks

This directory contains comprehensive performance benchmarks for the Queue Management System, measuring baseline performance before optimization.

## Benchmark Categories

### 1. QueueService Benchmarks (`QueueServiceBenchmarks.cs`)
Measures the performance of core queue service operations:
- **CreatePreRegistration**: User pre-registration throughput
- **GetQueueStatus**: Queue position lookup performance
- **GetEventCapacity**: Capacity query performance

**Key Metrics:**
- Operations per second
- Memory allocations per operation
- Latency percentiles

### 2. ReservationService Benchmarks (`ReservationServiceBenchmarks.cs`)
Measures reservation and confirmation operations:
- **CreateReservation**: Slot reservation throughput
- **GetCurrentReservation**: Active reservation lookup
- **ConfirmReservation**: Registration confirmation (in progress)

**Key Metrics:**
- Transaction throughput
- Lock contention
- Memory pressure

### 3. Cache Benchmarks (`CacheBenchmarks.cs`)
Measures in-memory cache operations:
- **CapacityGet/CapacityGetRemaining**: Capacity lookup performance
- **CapacityIncrement**: Atomic capacity updates
- **QueueGetRank**: Rank lookup in sorted set
- **QueueCount**: Queue size queries
- **QueueAddBulk**: Batch insertion performance
- **CheckRateLimit**: Rate limiting checks (user & IP)
- **AcquireLock**: Distributed lock acquisition

**Key Metrics:**
- Cache hit rates
- Concurrent access performance
- Memory overhead

### 4. Job Benchmarks (`JobBenchmarks.cs`)
Measures background job performance with varying data sizes:
- **ComputeQueueRanks_Lottery**: Fisher-Yates shuffle (100/1000/10000 participants)
- **InviteParticipants**: Batch invitation processing
- **SyncCache**: Cache synchronization operations

**Parameterized by:** 100, 1,000, and 10,000 participants

**Key Metrics:**
- Processing time scaling (O(n) complexity)
- Memory allocation patterns
- Throughput at scale

### 5. Shuffle Benchmarks (`ShuffleBenchmarks.cs`)
Measures the cryptographic Fisher-Yates shuffle algorithm:
- **Shuffle_100_Items**: Small dataset shuffle
- **Shuffle_1000_Items**: Medium dataset shuffle
- **Shuffle_10000_Items**: Large dataset shuffle (production scale)

**Key Metrics:**
- Time complexity verification (O(n))
- Cryptographic RNG overhead
- Memory efficiency

### 6. Repository Benchmarks (`RepositoryBenchmarks.cs`)
Measures database operations (in-memory implementation):
- **QueueEntry operations**: Single/bulk queries, filtering
- **PreRegistration operations**: Lookups, counts, CreateOrGet pattern
- **CreateBulk**: Batch insertion performance

**Key Metrics:**
- Query performance
- Bulk operation efficiency
- Index effectiveness simulation

## Running Benchmarks

### Prerequisites
```bash
dotnet restore
```

### Run All Benchmarks
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *
```

### Run Specific Benchmark Categories

**Queue Service:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *QueueService*
```

**Cache Operations:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Cache*
```

**Background Jobs:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Job*
```

**Fisher-Yates Shuffle:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Shuffle*
```

**Repository Operations:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Repository*
```

**Reservation Service:**
```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *Reservation*
```

### Alternative: Use BenchmarkRunner directly
```bash
dotnet run -c Release --project QueueManagement.Tests --run-benchmarks All
dotnet run -c Release --project QueueManagement.Tests --run-benchmarks QueueService
dotnet run -c Release --project QueueManagement.Tests --run-benchmarks Cache
```

## Benchmark Output

Results are saved to `BenchmarkDotNet.Artifacts/results/` in multiple formats:
- **Markdown** (`.md`): Human-readable summary
- **HTML**: Interactive reports
- **CSV**: Raw data for analysis

## Understanding Results

### Key Metrics Explained

**Mean**: Average execution time per operation
**Error**: Standard error of the mean
**StdDev**: Standard deviation (consistency measure)
**Gen0/Gen1/Gen2**: Garbage collection frequency
**Allocated**: Memory allocated per operation

### Performance Baselines (Expected)

#### Pre-Registration (QueueService)
- **Target**: 1,000-5,000 ops/sec
- **Memory**: < 1 KB per operation

#### Lottery (Fisher-Yates Shuffle)
- **10,000 users**: < 50ms
- **Memory**: O(n)

#### Reservation Creation
- **Target**: 500-1,000 ops/sec
- **Lock contention**: Minimal with distributed locking

#### Cache Operations
- **Capacity queries**: < 1?s
- **Rank lookups**: < 10?s
- **Bulk operations**: > 10,000 items/sec

## Optimization Targets

After establishing baselines, focus optimization on:

1. **High-Frequency Operations**
   - Pre-registration (100,000+ requests in minutes)
   - Queue status lookups
   - Capacity checks

2. **Memory Hotspots**
   - Identify operations with high Gen2 collections
   - Reduce allocations in hot paths

3. **Scalability Bottlenecks**
   - Lock contention in reservation creation
   - Bulk operation performance
   - Cache synchronization overhead

4. **Critical Path Latency**
   - P99 latency for user-facing operations
   - Lottery computation time
   - Invitation batch processing

## Best Practices

1. **Always run in Release mode** (`-c Release`)
2. **Close unnecessary applications** during benchmarking
3. **Run multiple times** to verify consistency
4. **Compare before/after** optimization changes
5. **Monitor memory allocations** not just speed

## Next Steps

1. ? **Establish Baseline**: Run all benchmarks and document results
2. **Identify Bottlenecks**: Analyze memory allocations and slow operations
3. **Prioritize Optimizations**: Focus on high-impact, high-frequency operations
4. **Implement Changes**: Apply targeted optimizations
5. **Validate Improvements**: Re-run benchmarks and compare
6. **Load Testing**: Test under realistic concurrent load

## Continuous Monitoring

Add benchmark runs to CI/CD pipeline:
```bash
# In CI pipeline
dotnet run -c Release --project QueueManagement.Tests -- --filter * --exporters json
# Compare with baseline stored in repo
```

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Tips](https://learn.microsoft.com/en-us/dotnet/core/testing/benchmark-dotnet)
- [Profiling .NET Applications](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)

---

**Note**: These benchmarks use in-memory implementations. Production performance will differ with actual Redis cache and SQL database. Consider integration benchmarks after code optimization.
