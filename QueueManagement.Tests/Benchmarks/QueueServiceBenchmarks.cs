using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QueueManagement.Application.Services;
using QueueManagement.Domain.Cache;
using QueueManagement.Domain.Entities;
using QueueManagement.Infrastructure.Cache;
using QueueManagement.Infrastructure.Repositories;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmarks for QueueService operations.
    /// Measures pre-registration and queue status retrieval performance.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class QueueServiceBenchmarks
    {
        private QueueService _queueService = null!;
        private Guid _eventId;
        private Guid[] _userIds = null!;
        private Event _event = null!;
        private int _userIndex;

        [GlobalSetup]
        public async Task Setup()
        {
            // Initialize repositories
            var userRepository = new InMemoryUserRepository();
            var eventRepository = new InMemoryEventRepository();
            var preRegistrationRepository = new InMemoryPreRegistrationRepository();
            var queueEntryRepository = new InMemoryQueueEntryRepository();
            var registrationRepository = new InMemoryRegistrationRepository();
            var cacheService = new InMemoryCacheService();

            // Initialize service
            _queueService = new QueueService(
                eventRepository,
                preRegistrationRepository,
                queueEntryRepository,
                registrationRepository,
                cacheService);

            // Create event
            _event = new Event
            {
                Name = "Benchmark Event",
                CapacityTotal = 10000,
                RegistrationStartAt = DateTime.UtcNow.AddMinutes(-10),
                RegistrationEndAt = DateTime.UtcNow.AddHours(24),
                ReservationDurationSeconds = 420
            };
            await eventRepository.CreateAsync(_event);
            _eventId = _event.Id;

            // Initialize cache
            await cacheService.CapacitySetAsync(_eventId, new CachedCapacity
            {
                CapacityTotal = _event.CapacityTotal,
                CapacityReserved = 0,
                CapacityConfirmed = 0
            });

            // Create test users
            _userIds = new Guid[1000];
            for (int i = 0; i < 1000; i++)
            {
                var user = new User($"user{i}@benchmark.com");
                await userRepository.CreateAsync(user);
                _userIds[i] = user.Id;
            }

            _userIndex = 0;
        }

        [Benchmark]
        public async Task<bool> CreatePreRegistration()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;

            var result = await _queueService.CreatePreRegistrationAsync(
                _eventId, 
                userId, 
                $"192.168.1.{_userIndex % 256}");

            return result.IsSuccess;
        }

        [Benchmark]
        public async Task<bool> GetQueueStatus()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;

            var result = await _queueService.GetQueueStatusAsync(_eventId, userId);
            return result.IsSuccess;
        }

        [Benchmark]
        public async Task<bool> GetEventCapacity()
        {
            var result = await _queueService.GetEventCapacityAsync(_eventId);
            return result.IsSuccess;
        }
    }
}
