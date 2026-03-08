# QueueManagement

A high-performance, production-ready virtual waiting room system designed to handle mass registration events with millions of concurrent users. Inspired by real-world challenges like the Ultramarin trail registration fiasco.

## 🎯 Features

### Core Queue Implementation
- **CustomQueue\<T\>**: High-performance FIFO queue using ring buffer architecture
  - Minimal allocations for maximum throughput
  - Optimized for concurrent access patterns
  - Thread-safe operations

### Virtual Waiting Room
The `VirtualWaitingRoom` class solves critical problems in high-demand registration systems:

✅ **Position Preservation** - Users maintain their queue position even after disconnection
✅ **Reconnection Support** - Graceful reconnection with configurable grace periods
✅ **Session Management** - Automatic timeout and expiration handling
✅ **Anti-Bot Protection** - IP-based rate limiting and behavioral detection
✅ **Capacity Management** - Queue limits and active session constraints
✅ **Fair Queuing** - Priority for returning users and accurate position tracking
✅ **Statistics & Monitoring** - Real-time metrics on queue health and throughput
✅ **Event-Driven** - Status change events for reactive UI updates

### Key Components

#### Participant Status States
- **Waiting** - In queue, awaiting their turn
- **Active** - Currently registered, accessing registration system
- **Completed** - Successfully completed registration
- **Expired** - Session timeout exceeded
- **Disconnected** - Lost connection (can reconnect within grace period)
- **Banned** - Blocked due to suspicious behavior

#### Public API

```csharp
// Join the waiting room
JoinResult Join(string? ipAddress, string? userAgent, int maxConnectionsPerIp = 5)

// Reconnect with previous token
ReconnectResult Reconnect(string token)

// Keep session alive
HeartbeatResult Heartbeat(string token)

// Mark registration complete
bool Complete(string token)

// Get current status
StatusResult GetStatus(string token)

// Process queue (activate waiting, expire sessions)
int ProcessQueue()

// Get statistics
QueueStatistics GetStatistics()

// Ban suspicious participants
void Ban(string id, string? reason = null)
```

## 🏗️ Project Structure

```
QueueManagement/
├── src/
│   ├── Core/
│   │   └── Program.cs              # CustomQueue<T> implementation
│   ├── Features/
│   │   └── VirtualWaitingRoom.cs   # Main waiting room logic
│   ├── Demo/
│   │   └── RegistrationSimulation.cs # Mass registration simulation
│   ├── Domain/
│   │   ├── Entities.cs             # Domain models
│   │   ├── Enums.cs                # Status enums
│   │   ├── Cache/                  # Cache service contracts
│   │   └── Repositories/           # Repository contracts
│   ├── Application/
│   │   ├── Services/               # Business logic services
│   │   ├── DTOs/                   # Data transfer objects
│   │   └── Jobs/                   # Background jobs
│   ├── Infrastructure/
│   │   ├── Cache/                  # Cache implementations
│   │   └── Repositories/           # Repository implementations
│   └── Api/
│       └── Controllers/            # API endpoints
├── QueueManagement.Tests/
│   ├── CustomQueueTests.cs         # Unit tests for queue
│   ├── CustomQueueBenchmarkTests.cs # Performance benchmarks
│   └── UnitTest1.cs
├── QueueManagement.sln             # Solution file
├── QueueManagement.csproj          # Main project
└── README.md                       # This file
```

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Build
```bash
dotnet build QueueManagement.sln
```

### Run Tests
```bash
dotnet test QueueManagement.sln --filter "Category!=Benchmark"
```

### Run Simulation
```bash
dotnet run --project QueueManagement.csproj
```

The simulation demonstrates the virtual waiting room handling:
- 10,000 concurrent registration attempts
- 500 available slots
- 50 max active sessions
- Realistic disconnection and reconnection patterns
- Bot detection and banning

## 📊 Usage Example

```csharp
// Create a waiting room for 100 concurrent sessions
var waitingRoom = new VirtualWaitingRoom(
    maxActiveSessions: 100,
    activeSessionTimeout: TimeSpan.FromMinutes(10),
    reconnectionGracePeriod: TimeSpan.FromMinutes(5),
    heartbeatInterval: TimeSpan.FromSeconds(30),
    maxReconnectionAttempts: 3,
    maxQueueCapacity: 100_000
);

// User joins the queue
var joinResult = waitingRoom.Join(
    ipAddress: "192.168.1.100",
    userAgent: "Mozilla/5.0..."
);

if (joinResult.IsSuccess)
{
    var participant = joinResult.Participant!;
    Console.WriteLine($"Position: {participant.CurrentPosition}");
    Console.WriteLine($"Estimated wait: {joinResult.EstimatedWait.TotalMinutes:F1} minutes");
    
    // Client periodically sends heartbeats
    var heartbeat = waitingRoom.Heartbeat(participant.Token);
    Console.WriteLine($"Status: {heartbeat.Status}");
    
    // When it's their turn...
    if (heartbeat.Status == ParticipantStatus.Active)
    {
        // User completes registration
        waitingRoom.Complete(participant.Token);
    }
}

// Process queue periodically (every 5 seconds)
var processedCount = waitingRoom.ProcessQueue();

// Monitor queue health
var stats = waitingRoom.GetStatistics();
Console.WriteLine($"Queue: {stats.CurrentlyWaiting} waiting, " +
    $"{stats.CurrentlyActive} active, " +
    $"{stats.TotalCompleted}/{stats.TotalJoined} completed");
```

## 🔧 Configuration Options

| Parameter | Default | Description |
|-----------|---------|-------------|
| `maxActiveSessions` | 100 | Max concurrent active registrations |
| `activeSessionTimeout` | 10 min | How long an active session lasts |
| `reconnectionGracePeriod` | 5 min | Time allowed to reconnect after disconnect |
| `heartbeatInterval` | 30 sec | Required heartbeat frequency |
| `maxReconnectionAttempts` | 3 | Max reconnect attempts before expiry |
| `maxQueueCapacity` | 0 (unlimited) | Max queue size |

## 📈 Performance Characteristics

- **Join Operation**: O(1) - Constant time queue insertion
- **Memory**: O(n) - Linear with queue size
- **Reconnection**: O(1) - Token lookup and status update
- **Position Updates**: O(n) - Batch updates when activating participants
- **Queue Processing**: O(m) - Where m = number of expired sessions

Typical throughput: **10,000+ joins/second** on modern hardware

## 🧪 Testing

The project includes comprehensive tests:

```bash
# Run CI-aligned tests
dotnet test --filter "Category!=Benchmark"

# Run with coverage
dotnet test --filter "Category!=Benchmark" --collect:"XPlat Code Coverage"

# Run benchmark-only tests
dotnet test --filter "Category=Benchmark"
```

Tests cover:
- Queue correctness (FIFO ordering)
- Concurrent access patterns
- Position preservation on reconnection
- Session expiration and cleanup
- Anti-bot IP limiting
- Event emission and status tracking

## CI And Test Report

GitHub Actions now runs restore, build, and unit tests for every push and pull request.

- The default CI test command excludes timing-sensitive benchmark tests with `--filter "Category!=Benchmark"`.
- CI also runs the lightweight benchmark test suite with `--filter "Category=Benchmark"` and includes it in the published report.
- On pushes to the repository default branch, the workflow publishes an HTML test report to GitHub Pages.
- The deployed Pages job exposes the report URL directly in the Actions UI as the environment link and in the job summary.

If GitHub Pages is not already configured for the repository, set the Pages source to `GitHub Actions` in repository settings.

For deeper performance analysis beyond the CI benchmark checks, run the full BenchmarkDotNet suite manually:

```bash
dotnet run -c Release --project QueueManagement.Tests -- --filter *
```

## 🐛 Troubleshooting

### Queue Fills Up
- Increase `maxActiveSessions` if registration is slow
- Check that `ProcessQueue()` is being called regularly
- Verify `Complete()` is called after successful registration

### High Disconnection Rates
- Increase `reconnectionGracePeriod`
- Ensure clients send heartbeats frequently
- Check network stability

### False Positive Bots
- Adjust `maxConnectionsPerIp` parameter in `Join()`
- Consider whitelist for legitimate bulk operations

## 📝 Namespaces

- `QueueManagement.Core` - Core queue data structure
- `QueueManagement.Features.VirtualWaitingRoom` - Waiting room logic
- `QueueManagement.Demo` - Simulation and examples
- `QueueManagement.Tests` - Unit tests and benchmarks
- `QueueManagement.Domain` - Domain entities and contracts
- `QueueManagement.Application` - Business logic layer
- `QueueManagement.Infrastructure` - Implementation details

## 📄 License

This project is part of a queue management system for high-demand registration events.

## 🤝 Contributing

1. Ensure all tests pass
2. Add tests for new features
3. Follow C# naming conventions
4. Use nullable reference types enabled
5. Target .NET 8.0+

## 📞 Support

For issues or questions about queue management, check:
- Unit tests for usage examples
- Simulation code for real-world scenarios
- XML documentation in source files
