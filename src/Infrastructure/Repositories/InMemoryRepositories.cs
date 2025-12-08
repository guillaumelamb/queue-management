using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QueueManagement.Domain;
using QueueManagement.Domain.Entities;
using QueueManagement.Domain.Repositories;

namespace QueueManagement.Infrastructure.Repositories
{
    /// <summary>
    /// In-memory implementation of IUserRepository for testing/demo.
    /// In production, replace with PostgreSQL implementation.
    /// </summary>
    public sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<Guid, User> _users = new();
        private readonly ConcurrentDictionary<string, Guid> _emailIndex = new();

        public Task<User?> GetByIdAsync(Guid id)
        {
            _users.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            if (_emailIndex.TryGetValue(email.ToLowerInvariant(), out var id))
            {
                _users.TryGetValue(id, out var user);
                return Task.FromResult(user);
            }
            return Task.FromResult<User?>(null);
        }

        public Task<User> CreateAsync(User user)
        {
            if (!_users.TryAdd(user.Id, user))
                throw new InvalidOperationException($"User {user.Id} already exists");
            
            _emailIndex[user.Email.ToLowerInvariant()] = user.Id;
            return Task.FromResult(user);
        }

        public Task<User> UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _users[user.Id] = user;
            return Task.FromResult(user);
        }
    }

    /// <summary>
    /// In-memory implementation of IEventRepository.
    /// </summary>
    public sealed class InMemoryEventRepository : IEventRepository
    {
        private readonly ConcurrentDictionary<Guid, Event> _events = new();
        private readonly object _lock = new();

        public Task<Event?> GetByIdAsync(Guid id)
        {
            _events.TryGetValue(id, out var evt);
            return Task.FromResult(evt);
        }

        public Task<IReadOnlyList<Event>> GetOpenEventsAsync()
        {
            var now = DateTime.UtcNow;
            var result = _events.Values
                .Where(e => e.RegistrationStartAt <= now && e.RegistrationEndAt >= now)
                .ToList();
            return Task.FromResult<IReadOnlyList<Event>>(result);
        }

        public Task<Event> CreateAsync(Event evt)
        {
            if (!_events.TryAdd(evt.Id, evt))
                throw new InvalidOperationException($"Event {evt.Id} already exists");
            return Task.FromResult(evt);
        }

        public Task<Event> UpdateAsync(Event evt)
        {
            evt.UpdatedAt = DateTime.UtcNow;
            _events[evt.Id] = evt;
            return Task.FromResult(evt);
        }

        public Task<bool> IncrementReservedAsync(Guid eventId, int delta)
        {
            lock (_lock)
            {
                if (!_events.TryGetValue(eventId, out var evt))
                    return Task.FromResult(false);

                if (evt.CapacityRemaining < delta)
                    return Task.FromResult(false);

                evt.CapacityReserved += delta;
                evt.UpdatedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
        }

        public Task<bool> IncrementConfirmedAsync(Guid eventId, int delta)
        {
            lock (_lock)
            {
                if (!_events.TryGetValue(eventId, out var evt))
                    return Task.FromResult(false);

                evt.CapacityConfirmed += delta;
                evt.UpdatedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
        }

        public Task<bool> DecrementReservedAsync(Guid eventId, int delta)
        {
            lock (_lock)
            {
                if (!_events.TryGetValue(eventId, out var evt))
                    return Task.FromResult(false);

                evt.CapacityReserved = Math.Max(0, evt.CapacityReserved - delta);
                evt.UpdatedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
        }
    }

    /// <summary>
    /// In-memory implementation of IPreRegistrationRepository.
    /// </summary>
    public sealed class InMemoryPreRegistrationRepository : IPreRegistrationRepository
    {
        private readonly ConcurrentDictionary<Guid, PreRegistration> _preRegs = new();
        private readonly ConcurrentDictionary<(Guid eventId, Guid userId), Guid> _index = new();
        private readonly object _lock = new();

        public Task<PreRegistration?> GetByIdAsync(Guid id)
        {
            _preRegs.TryGetValue(id, out var preReg);
            return Task.FromResult(preReg);
        }

        public Task<PreRegistration?> GetByEventAndUserAsync(Guid eventId, Guid userId)
        {
            if (_index.TryGetValue((eventId, userId), out var id))
            {
                _preRegs.TryGetValue(id, out var preReg);
                return Task.FromResult(preReg);
            }
            return Task.FromResult<PreRegistration?>(null);
        }

        public Task<IReadOnlyList<PreRegistration>> GetByEventAsync(Guid eventId)
        {
            var result = _preRegs.Values.Where(p => p.EventId == eventId).ToList();
            return Task.FromResult<IReadOnlyList<PreRegistration>>(result);
        }

        public Task<int> CountByEventAsync(Guid eventId)
        {
            return Task.FromResult(_preRegs.Values.Count(p => p.EventId == eventId));
        }

        public Task<(PreRegistration registration, bool created)> CreateOrGetAsync(PreRegistration preReg)
        {
            lock (_lock)
            {
                var key = (preReg.EventId, preReg.UserId);
                if (_index.TryGetValue(key, out var existingId))
                {
                    return Task.FromResult((_preRegs[existingId], false));
                }

                _preRegs[preReg.Id] = preReg;
                _index[key] = preReg.Id;
                return Task.FromResult((preReg, true));
            }
        }
    }

    /// <summary>
    /// In-memory implementation of IQueueEntryRepository.
    /// </summary>
    public sealed class InMemoryQueueEntryRepository : IQueueEntryRepository
    {
        private readonly ConcurrentDictionary<Guid, QueueEntry> _entries = new();
        private readonly ConcurrentDictionary<(Guid eventId, Guid userId), Guid> _userIndex = new();
        private readonly ConcurrentDictionary<(Guid eventId, int rank), Guid> _rankIndex = new();
        private readonly object _lock = new();

        public Task<QueueEntry?> GetByIdAsync(Guid id)
        {
            _entries.TryGetValue(id, out var entry);
            return Task.FromResult(entry);
        }

        public Task<QueueEntry?> GetByEventAndUserAsync(Guid eventId, Guid userId)
        {
            if (_userIndex.TryGetValue((eventId, userId), out var id))
            {
                _entries.TryGetValue(id, out var entry);
                return Task.FromResult(entry);
            }
            return Task.FromResult<QueueEntry?>(null);
        }

        public Task<QueueEntry?> GetByEventAndRankAsync(Guid eventId, int rank)
        {
            if (_rankIndex.TryGetValue((eventId, rank), out var id))
            {
                _entries.TryGetValue(id, out var entry);
                return Task.FromResult(entry);
            }
            return Task.FromResult<QueueEntry?>(null);
        }

        public Task<IReadOnlyList<QueueEntry>> GetByEventAsync(Guid eventId)
        {
            var result = _entries.Values
                .Where(e => e.EventId == eventId)
                .OrderBy(e => e.Rank)
                .ToList();
            return Task.FromResult<IReadOnlyList<QueueEntry>>(result);
        }

        public Task<IReadOnlyList<QueueEntry>> GetByEventAndStatusAsync(Guid eventId, QueueEntryStatus status)
        {
            var result = _entries.Values
                .Where(e => e.EventId == eventId && e.Status == status)
                .OrderBy(e => e.Rank)
                .ToList();
            return Task.FromResult<IReadOnlyList<QueueEntry>>(result);
        }

        public Task<IReadOnlyList<QueueEntry>> GetInvitableEntriesAsync(Guid eventId, int maxRank)
        {
            var result = _entries.Values
                .Where(e => e.EventId == eventId && e.Rank <= maxRank && e.Status == QueueEntryStatus.Pending)
                .OrderBy(e => e.Rank)
                .ToList();
            return Task.FromResult<IReadOnlyList<QueueEntry>>(result);
        }

        public Task<QueueEntry> CreateAsync(QueueEntry entry)
        {
            lock (_lock)
            {
                if (!_entries.TryAdd(entry.Id, entry))
                    throw new InvalidOperationException($"Entry {entry.Id} already exists");
                
                _userIndex[(entry.EventId, entry.UserId)] = entry.Id;
                _rankIndex[(entry.EventId, entry.Rank)] = entry.Id;
                return Task.FromResult(entry);
            }
        }

        public Task CreateBulkAsync(IEnumerable<QueueEntry> entries)
        {
            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    _entries[entry.Id] = entry;
                    _userIndex[(entry.EventId, entry.UserId)] = entry.Id;
                    _rankIndex[(entry.EventId, entry.Rank)] = entry.Id;
                }
            }
            return Task.CompletedTask;
        }

        public Task<QueueEntry> UpdateAsync(QueueEntry entry)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            _entries[entry.Id] = entry;
            return Task.FromResult(entry);
        }

        public Task UpdateStatusAsync(Guid id, QueueEntryStatus status)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                entry.Status = status;
                entry.UpdatedAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task<bool> HasRanksAsync(Guid eventId)
        {
            return Task.FromResult(_entries.Values.Any(e => e.EventId == eventId));
        }
    }

    /// <summary>
    /// In-memory implementation of IReservationRepository.
    /// </summary>
    public sealed class InMemoryReservationRepository : IReservationRepository
    {
        private readonly ConcurrentDictionary<Guid, Reservation> _reservations = new();
        private readonly ConcurrentDictionary<(Guid eventId, Guid userId), Guid> _index = new();
        private readonly HashSet<Guid> _lockedIds = new();
        private readonly object _lock = new();

        public Task<Reservation?> GetByIdAsync(Guid id)
        {
            _reservations.TryGetValue(id, out var res);
            return Task.FromResult(res);
        }

        public Task<Reservation?> GetByEventAndUserAsync(Guid eventId, Guid userId)
        {
            if (_index.TryGetValue((eventId, userId), out var id))
            {
                _reservations.TryGetValue(id, out var res);
                return Task.FromResult(res);
            }
            return Task.FromResult<Reservation?>(null);
        }

        public Task<Reservation?> GetActiveByEventAndUserAsync(Guid eventId, Guid userId)
        {
            if (_index.TryGetValue((eventId, userId), out var id))
            {
                if (_reservations.TryGetValue(id, out var res))
                {
                    if (res.Status == ReservationStatus.Pending || res.Status == ReservationStatus.Confirmed)
                    {
                        return Task.FromResult<Reservation?>(res);
                    }
                }
            }
            return Task.FromResult<Reservation?>(null);
        }

        public Task<IReadOnlyList<Reservation>> GetExpiredPendingAsync(DateTime asOf)
        {
            var result = _reservations.Values
                .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= asOf)
                .ToList();
            return Task.FromResult<IReadOnlyList<Reservation>>(result);
        }

        public Task<int> CountPendingByEventAsync(Guid eventId)
        {
            return Task.FromResult(_reservations.Values
                .Count(r => r.EventId == eventId && r.Status == ReservationStatus.Pending));
        }

        public Task<Reservation> CreateAsync(Reservation reservation)
        {
            lock (_lock)
            {
                if (!_reservations.TryAdd(reservation.Id, reservation))
                    throw new InvalidOperationException($"Reservation {reservation.Id} already exists");
                
                _index[(reservation.EventId, reservation.UserId)] = reservation.Id;
                return Task.FromResult(reservation);
            }
        }

        public Task<Reservation> UpdateAsync(Reservation reservation)
        {
            reservation.UpdatedAt = DateTime.UtcNow;
            _reservations[reservation.Id] = reservation;
            return Task.FromResult(reservation);
        }

        public Task UpdateStatusAsync(Guid id, ReservationStatus status)
        {
            if (_reservations.TryGetValue(id, out var res))
            {
                res.Status = status;
                res.UpdatedAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task<Reservation?> GetAndLockAsync(Guid id)
        {
            lock (_lock)
            {
                if (!_reservations.TryGetValue(id, out var res))
                    return Task.FromResult<Reservation?>(null);

                if (_lockedIds.Contains(id))
                    return Task.FromResult<Reservation?>(null); // Already locked

                _lockedIds.Add(id);
                return Task.FromResult<Reservation?>(res);
            }
        }

        public void ReleaseLock(Guid id)
        {
            lock (_lock)
            {
                _lockedIds.Remove(id);
            }
        }
    }

    /// <summary>
    /// In-memory implementation of IRegistrationRepository.
    /// </summary>
    public sealed class InMemoryRegistrationRepository : IRegistrationRepository
    {
        private readonly ConcurrentDictionary<Guid, Registration> _registrations = new();
        private readonly ConcurrentDictionary<(Guid eventId, Guid userId), Guid> _userIndex = new();
        private readonly ConcurrentDictionary<Guid, Guid> _reservationIndex = new();
        private readonly object _lock = new();

        public Task<Registration?> GetByIdAsync(Guid id)
        {
            _registrations.TryGetValue(id, out var reg);
            return Task.FromResult(reg);
        }

        public Task<Registration?> GetByEventAndUserAsync(Guid eventId, Guid userId)
        {
            if (_userIndex.TryGetValue((eventId, userId), out var id))
            {
                _registrations.TryGetValue(id, out var reg);
                return Task.FromResult(reg);
            }
            return Task.FromResult<Registration?>(null);
        }

        public Task<Registration?> GetByReservationAsync(Guid reservationId)
        {
            if (_reservationIndex.TryGetValue(reservationId, out var id))
            {
                _registrations.TryGetValue(id, out var reg);
                return Task.FromResult(reg);
            }
            return Task.FromResult<Registration?>(null);
        }

        public Task<int> CountByEventAsync(Guid eventId)
        {
            return Task.FromResult(_registrations.Values.Count(r => r.EventId == eventId));
        }

        public Task<Registration> CreateAsync(Registration registration)
        {
            lock (_lock)
            {
                if (!_registrations.TryAdd(registration.Id, registration))
                    throw new InvalidOperationException($"Registration {registration.Id} already exists");
                
                _userIndex[(registration.EventId, registration.UserId)] = registration.Id;
                _reservationIndex[registration.ReservationId] = registration.Id;
                return Task.FromResult(registration);
            }
        }

        public Task<Registration> UpdateAsync(Registration registration)
        {
            registration.UpdatedAt = DateTime.UtcNow;
            _registrations[registration.Id] = registration;
            return Task.FromResult(registration);
        }

        public Task<bool> ExistsAsync(Guid eventId, Guid userId)
        {
            return Task.FromResult(_userIndex.ContainsKey((eventId, userId)));
        }
    }
}
