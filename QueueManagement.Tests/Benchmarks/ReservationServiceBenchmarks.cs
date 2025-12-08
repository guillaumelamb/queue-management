using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QueueManagement.Application.Services;
using QueueManagement.Domain;
using QueueManagement.Domain.Cache;
using QueueManagement.Domain.Entities;
using QueueManagement.Domain.Repositories;
using QueueManagement.Infrastructure.Cache;
using QueueManagement.Infrastructure.Repositories;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmarks for ReservationService operations.
    /// Measures reservation creation and confirmation performance.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class ReservationServiceBenchmarks
    {
        private ReservationService _reservationService = null!;
        private InMemoryEventRepository _eventRepository = null!;
        private InMemoryQueueEntryRepository _queueEntryRepository = null!;
        private Guid _eventId;
        private Guid[] _userIds = null!;
        private int _userIndex;

        [GlobalSetup]
        public async Task Setup()
        {
            // Initialize repositories
            var userRepository = new InMemoryUserRepository();
            _eventRepository = new InMemoryEventRepository();
            _queueEntryRepository = new InMemoryQueueEntryRepository();
            var reservationRepository = new InMemoryReservationRepository();
            var registrationRepository = new InMemoryRegistrationRepository();
            var cacheService = new InMemoryCacheService();

            // Initialize service
            _reservationService = new ReservationService(
                _eventRepository,
                _queueEntryRepository,
                reservationRepository,
                registrationRepository,
                cacheService);

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
            await cacheService.CapacitySetAsync(_eventId, new CachedCapacity
            {
                CapacityTotal = evt.CapacityTotal,
                CapacityReserved = 0,
                CapacityConfirmed = 0
            });

            // Create test users and queue entries
            _userIds = new Guid[1000];
            for (int i = 0; i < 1000; i++)
            {
                var user = new User($"user{i}@benchmark.com");
                await userRepository.CreateAsync(user);
                _userIds[i] = user.Id;

                // Create invited queue entry for each user
                var queueEntry = new QueueEntry
                {
                    EventId = _eventId,
                    UserId = user.Id,
                    Rank = i + 1,
                    Status = QueueEntryStatus.Invited
                };
                await _queueEntryRepository.CreateAsync(queueEntry);
            }

            _userIndex = 0;
        }

        [IterationSetup]
        public async Task IterationSetup()
        {
            // Reset queue entries to invited status for each iteration
            var userId = _userIds[_userIndex % _userIds.Length];
            var queueEntry = await _queueEntryRepository.GetByEventAndUserAsync(_eventId, userId);
            if (queueEntry != null)
            {
                queueEntry.Status = QueueEntryStatus.Invited;
                await _queueEntryRepository.UpdateAsync(queueEntry);
            }
        }

        [Benchmark]
        public async Task<bool> CreateReservation()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;

            var result = await _reservationService.CreateReservationAsync(_eventId, userId);
            return result.IsSuccess;
        }

        [Benchmark]
        public async Task<bool> GetCurrentReservation()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;

            var result = await _reservationService.GetCurrentReservationAsync(_eventId, userId);
            return result.IsSuccess || result.ErrorCode == ErrorCodes.NotFound;
        }
    }
}
