using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QueueManagement.Domain;
using QueueManagement.Domain.Entities;
using QueueManagement.Infrastructure.Repositories;

namespace QueueManagement.Tests.Benchmarks
{
    /// <summary>
    /// Benchmarks for repository operations.
    /// Measures database query and write performance.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class RepositoryBenchmarks
    {
        private InMemoryQueueEntryRepository _queueEntryRepository = null!;
        private InMemoryReservationRepository _reservationRepository = null!;
        private InMemoryPreRegistrationRepository _preRegistrationRepository = null!;
        private Guid _eventId;
        private Guid[] _userIds = null!;
        private int _userIndex;

        [GlobalSetup]
        public async Task Setup()
        {
            _queueEntryRepository = new InMemoryQueueEntryRepository();
            _reservationRepository = new InMemoryReservationRepository();
            _preRegistrationRepository = new InMemoryPreRegistrationRepository();
            
            _eventId = Guid.NewGuid();

            // Create users and entries
            _userIds = Enumerable.Range(0, 1000)
                .Select(_ => Guid.NewGuid())
                .ToArray();

            // Populate pre-registrations
            foreach (var userId in _userIds)
            {
                var preReg = new PreRegistration(_eventId, userId);
                await _preRegistrationRepository.CreateOrGetAsync(preReg);
            }

            // Populate queue entries
            for (int i = 0; i < _userIds.Length; i++)
            {
                var queueEntry = new QueueEntry
                {
                    EventId = _eventId,
                    UserId = _userIds[i],
                    Rank = i + 1,
                    Status = QueueEntryStatus.Pending
                };
                await _queueEntryRepository.CreateAsync(queueEntry);
            }

            _userIndex = 0;
        }

        [Benchmark]
        public async Task<QueueEntry?> QueueEntry_GetByEventAndUser()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;
            return await _queueEntryRepository.GetByEventAndUserAsync(_eventId, userId);
        }

        [Benchmark]
        public async Task<IReadOnlyList<QueueEntry>> QueueEntry_GetByEvent()
        {
            return await _queueEntryRepository.GetByEventAsync(_eventId);
        }

        [Benchmark]
        public async Task<IReadOnlyList<QueueEntry>> QueueEntry_GetByEventAndStatus()
        {
            return await _queueEntryRepository.GetByEventAndStatusAsync(_eventId, QueueEntryStatus.Pending);
        }

        [Benchmark]
        public async Task QueueEntry_CreateBulk_100()
        {
            var entries = Enumerable.Range(0, 100)
                .Select(i => new QueueEntry
                {
                    EventId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    Rank = i + 1,
                    Status = QueueEntryStatus.Pending
                })
                .ToList();

            await _queueEntryRepository.CreateBulkAsync(entries);
        }

        [Benchmark]
        public async Task<int> PreRegistration_CountByEvent()
        {
            return await _preRegistrationRepository.CountByEventAsync(_eventId);
        }

        [Benchmark]
        public async Task<IReadOnlyList<PreRegistration>> PreRegistration_GetByEvent()
        {
            return await _preRegistrationRepository.GetByEventAsync(_eventId);
        }

        [Benchmark]
        public async Task<PreRegistration?> PreRegistration_GetByEventAndUser()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;
            return await _preRegistrationRepository.GetByEventAndUserAsync(_eventId, userId);
        }

        [Benchmark]
        public async Task<(PreRegistration, bool)> PreRegistration_CreateOrGet()
        {
            var userId = _userIds[_userIndex % _userIds.Length];
            _userIndex++;
            var preReg = new PreRegistration(_eventId, userId);
            return await _preRegistrationRepository.CreateOrGetAsync(preReg);
        }
    }
}
