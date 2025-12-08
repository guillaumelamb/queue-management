using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QueueManagement.Domain.Cache;
using QueueManagement.Infrastructure.Cache;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmarks for cache operations.
    /// Measures performance of capacity management, queue operations, and rate limiting.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class CacheBenchmarks
    {
        private InMemoryCacheService _cacheService = null!;
        private Guid _eventId;
        private Guid[] _userIds = null!;
        private int _userIndex;

        [GlobalSetup]
        public async Task Setup()
        {
            _cacheService = new InMemoryCacheService();
            _eventId = Guid.NewGuid();

            // Initialize capacity
            await _cacheService.CapacitySetAsync(_eventId, new CachedCapacity
            {
                CapacityTotal = 10000,
                CapacityReserved = 0,
                CapacityConfirmed = 0
            });

            // Create user IDs
            _userIds = Enumerable.Range(0, 1000)
                .Select(_ => Guid.NewGuid())
                .ToArray();

            // Populate queue
            var queueEntries = _userIds
                .Select((userId, index) => (userId, index + 1))
                .ToArray();
            await _cacheService.QueueAddBulkAsync(_eventId, queueEntries);

            _userIndex = 0;
        }

        [Benchmark]
        public async Task<CachedCapacity?> CapacityGet()
        {
            return await _cacheService.CapacityGetAsync(_eventId);
        }

        [Benchmark]
        public async Task<int> CapacityGetRemaining()
        {
            return await _cacheService.CapacityGetRemainingAsync(_eventId);
        }

        [Benchmark]
        public async Task CapacityIncrementReserved()
        {
            await _cacheService.CapacityIncrementReservedAsync(_eventId, 1);
        }

        [Benchmark]
        public async Task CapacityIncrementConfirmed()
        {
            await _cacheService.CapacityIncrementConfirmedAsync(_eventId, 1);
        }

        [Benchmark]
        public async Task<int?> QueueGetRank()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;
            return await _cacheService.QueueGetRankAsync(_eventId, userId);
        }

        [Benchmark]
        public async Task<long> QueueCount()
        {
            return await _cacheService.QueueCountAsync(_eventId);
        }

        [Benchmark]
        public async Task QueueAddBulk_100()
        {
            var entries = Enumerable.Range(0, 100)
                .Select(i => (Guid.NewGuid(), i + 1))
                .ToArray();
            
            await _cacheService.QueueAddBulkAsync(_eventId, entries);
        }

        [Benchmark]
        public async Task<RateLimitResult> CheckRateLimitUser()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;
            return await _cacheService.CheckRateLimitUserAsync(userId, 10, TimeSpan.FromMinutes(1));
        }

        [Benchmark]
        public async Task<RateLimitResult> CheckRateLimitIp()
        {
            var ip = $"192.168.{_userIndex % 256}.{_userIndex % 256}";
            _userIndex++;
            return await _cacheService.CheckRateLimitIpAsync(ip, 20, TimeSpan.FromMinutes(1));
        }

        [Benchmark]
        public async Task<IAsyncDisposable?> AcquireLock()
        {
            var lockKey = $"lock:{_userIndex % 100}";
            _userIndex++;
            var handle = await _cacheService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(5));
            if (handle != null)
            {
                await handle.DisposeAsync();
            }
            return handle;
        }
    }
}
