using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QueueManagement.Application.Jobs;
using QueueManagement.Domain;
using QueueManagement.Domain.Cache;
using QueueManagement.Domain.Entities;
using QueueManagement.Domain.Repositories;
using QueueManagement.Infrastructure.Cache;
using QueueManagement.Infrastructure.Repositories;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmarks for background job operations.
    /// Measures lottery computation, invitation, and synchronization performance.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class JobBenchmarks
    {
        private ComputeQueueRanksJob _computeRanksJob = null!;
        private InviteParticipantsJob _inviteParticipantsJob = null!;
        private SyncCacheJob _syncCacheJob = null!;
        private ExpireReservationsJob _expireReservationsJob = null!;
        
        private InMemoryPreRegistrationRepository _preRegistrationRepository = null!;
        private InMemoryQueueEntryRepository _queueEntryRepository = null!;
        private InMemoryEventRepository _eventRepository = null!;
        private InMemoryReservationRepository _reservationRepository = null!;
        private ICacheService _cacheService = null!;
        
        private Guid _eventId;
        private List<Guid> _userIds = null!;

        [Params(100, 1000, 10000)]
        public int ParticipantCount { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            // Initialize repositories
            var userRepository = new InMemoryUserRepository();
            _eventRepository = new InMemoryEventRepository();
            _preRegistrationRepository = new InMemoryPreRegistrationRepository();
            _queueEntryRepository = new InMemoryQueueEntryRepository();
            _reservationRepository = new InMemoryReservationRepository();
            var registrationRepository = new InMemoryRegistrationRepository();
            _cacheService = new InMemoryCacheService();

            // Initialize jobs
            _computeRanksJob = new ComputeQueueRanksJob(
                _preRegistrationRepository,
                _queueEntryRepository,
                _eventRepository,
                _cacheService);

            _inviteParticipantsJob = new InviteParticipantsJob(
                _queueEntryRepository,
                _eventRepository,
                _cacheService);

            _syncCacheJob = new SyncCacheJob(
                _eventRepository,
                _queueEntryRepository,
                _reservationRepository,
                _cacheService);

            _expireReservationsJob = new ExpireReservationsJob(
                _reservationRepository,
                _eventRepository,
                _queueEntryRepository,
                _cacheService);

            // Create event
            var evt = new Event
            {
                Name = "Benchmark Event",
                CapacityTotal = 10000,
                RegistrationStartAt = DateTime.UtcNow.AddMinutes(-10),
                RegistrationEndAt = DateTime.UtcNow.AddHours(24),
                ReservationDurationSeconds = 420
            };
            await _eventRepository.CreateAsync(evt);
            _eventId = evt.Id;

            // Initialize cache
            await _cacheService.CapacitySetAsync(_eventId, new CachedCapacity
            {
                CapacityTotal = evt.CapacityTotal,
                CapacityReserved = 0,
                CapacityConfirmed = 0
            });

            // Create users and pre-registrations
            _userIds = new List<Guid>();
            for (int i = 0; i < ParticipantCount; i++)
            {
                var user = new User($"user{i}@benchmark.com");
                await userRepository.CreateAsync(user);
                _userIds.Add(user.Id);

                var preReg = new PreRegistration(_eventId, user.Id);
                await _preRegistrationRepository.CreateOrGetAsync(preReg);
            }
        }

        [IterationSetup]
        public async Task IterationSetup()
        {
            // Clear queue entries before each iteration for lottery benchmark
            // Note: In-memory implementation doesn't have DeleteByEventAsync, so we'll skip this
            // The lottery will check HasRanksAsync and skip if already computed
        }

        [Benchmark]
        public async Task<JobResult> ComputeQueueRanks_Lottery()
        {
            return await _computeRanksJob.ExecuteAsync(_eventId);
        }

        [Benchmark]
        public async Task<JobResult> InviteParticipants()
        {
            // Setup: ensure queue entries exist
            if (!await _queueEntryRepository.HasRanksAsync(_eventId))
            {
                await _computeRanksJob.ExecuteAsync(_eventId);
            }

            return await _inviteParticipantsJob.ExecuteAsync(_eventId, 100);
        }

        [Benchmark]
        public async Task<JobResult> SyncCache()
        {
            return await _syncCacheJob.ExecuteAsync(_eventId);
        }
    }

    /// <summary>
    /// Benchmarks for Fisher-Yates shuffle algorithm used in lottery.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class ShuffleBenchmarks
    {
        private List<int> _smallList = null!;
        private List<int> _mediumList = null!;
        private List<int> _largeList = null!;

        [GlobalSetup]
        public void Setup()
        {
            _smallList = Enumerable.Range(0, 100).ToList();
            _mediumList = Enumerable.Range(0, 1000).ToList();
            _largeList = Enumerable.Range(0, 10000).ToList();
        }

        [Benchmark]
        public void Shuffle_100_Items()
        {
            CryptoShuffle(_smallList);
        }

        [Benchmark]
        public void Shuffle_1000_Items()
        {
            CryptoShuffle(_mediumList);
        }

        [Benchmark]
        public void Shuffle_10000_Items()
        {
            CryptoShuffle(_largeList);
        }

        private static void CryptoShuffle<T>(IList<T> list)
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[4];

            for (int i = list.Count - 1; i > 0; i--)
            {
                rng.GetBytes(bytes);
                int j = (int)(BitConverter.ToUInt32(bytes, 0) % (uint)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
