# Cronus.Transport.RabbitMQ — v11 → v12 perf findings

Captured during the v12 async migration that bumped `RabbitMQ.Client` 6.8.1 (sync) → 7.2.1 (async-only). This document records the perf shape of the change and why the numbers look the way they do.

## Hardware / runtime

- 13th Gen Intel Core i7-13700, 24 logical / 16 physical cores
- Windows 11 (10.0.26200)
- .NET SDK 10.0.203, runtime 10.0.7, X64 RyuJIT AVX2
- BenchmarkDotNet v0.14.0, `[SimpleJob(warmupCount: 3, iterationCount: 5)]`, `[MemoryDiagnoser]`
- RabbitMQ broker: `rabbitmq:3.13` Docker image, localhost:5672, default credentials, plain TCP (no TLS)
- Build configuration: `Release`

Benchmarks live under `src/Elders.Cronus.Transport.RabbitMQ.Benchmarks/`. Re-run instructions: see [`../../src/Elders.Cronus.Transport.RabbitMQ.Benchmarks/README.md`](../../src/Elders.Cronus.Transport.RabbitMQ.Benchmarks/README.md).

## What changed

| | v11 | v12 |
|---|---|---|
| RabbitMQ.Client | 6.8.1 (sync) | 7.2.1 (async-only) |
| Publish API | `IModel.BasicPublish(...)` (void return) | `IChannel.BasicPublishAsync(...)` (`ValueTask`) |
| Lock around channel cache | `lock(object)` | `SemaphoreSlim.WaitAsync()` |
| Publisher confirms | not used | hardcoded ON in `ChannelResolverBase.cs:57` and `PublisherChannelResolver.cs:77` |
| `IConnection` / `IChannel` | `IDisposable` | `IAsyncDisposable` |

## Headline result: single publish

Sequential publish, one-at-a-time, on a hot channel:

```
v11 sync, no confirms (baseline) ........   770 ns
v12 async, no confirms .................. 6,976 ns       9x slower
v12 async, WITH confirms (production) .. 259,778 ns    337x slower
```

The v12 production single-publish path is **~337× slower** than v11. The cost decomposes:

```
770 ns       v11 baseline
+ 6,200 ns   sync→async conversion overhead
+ 252,800 ns publisher confirms (one broker round-trip per call)
= 259,778 ns v12 production single publish
```

The 9× from sync→async is intrinsic and unavoidable — the price of moving to the modern async-only client. The 252,800 ns from confirms is a feature opt-in that brings a real correctness benefit (at-least-once delivery instead of fire-and-forget) but is paid per call.

## Concurrent publish

Where the v12 design pays back. Publishing N messages in parallel on a single channel:

| parallelism | v12 no confirms | v12 with confirms | per-op (with confirms) | confirms tax |
|---:|---:|---:|---:|---:|
| 10  | 58.5 µs | 546 µs | 55 µs/op | 9.3× |
| 50  | 333 µs | 936 µs | 19 µs/op | 2.8× |
| 100 | 663 µs | 1,496 µs | 15 µs/op | 2.3× |

Confirms cost amortizes as parallelism grows — at p=100, per-publish cost is **15 µs** rather than the sequential 260 µs. The client pipelines outstanding confirms instead of waiting per-call.

For comparison, v11 sync concurrent publish (p=100) clocked **186 µs total** — it scaled linearly because every call grabbed `lock(IModel)` and serialized through it. v12 with confirms is 8× slower than v11 sync at p=100, but with a 27% smaller allocation footprint and proper async behavior under thread-pool pressure.

## Other measurements

### Channel resolver (the win)

`PublisherChannelResolver.ResolveAsync` under contention — this is the path Stage 2 explicitly redesigned:

```
v11 lock(obj), p=100  ........ 26,681 ns
v12 SemaphoreSlim, p=100 ..... 10,939 ns       2.4x FASTER
```

`lock(obj)` parks OS threads on contention; `SemaphoreSlim.WaitAsync()` returns to the thread pool. Scales better as worker count grows.

### Connection open (one-time cost)

```
v11 sync .........  4.11 ms
v12 async ........ 46.98 ms     11.4x slower
```

A real regression in the v7 client's connection handshake (stricter state machine), but only paid at process startup and on tenant-options reload via `RabbitMqStartup.OnChange`. Not a steady-state concern.

### Channel open

```
v11 sync ......... 660.8 µs
v12 async ........ 673.0 µs     ~same
```

No meaningful change. Channels are cached and reused after creation, so this rarely matters at runtime.

## Takeaways

- **Production single publish is ~337× slower than v11** (~260 µs vs 770 ns). Most of the cost is publisher confirms (~250 µs per call); the rest is sync→async overhead.
- **Concurrent publish amortizes confirms.** At p=100, per-op cost drops to 15 µs.
- **Confirms is the only meaningful tuning knob.** Sync→async is irreversible (RabbitMQ.Client 6.x is unsupported).
- **The resolver got faster.** Channel resolution under contention is 2.4× faster because `SemaphoreSlim` doesn't park OS threads — this scales as worker count grows.

## Why publisher confirms are NOT exposed as a setting (yet)

The natural follow-up — adding to `RabbitMqOptions`:

```csharp
public bool PublisherConfirmationsEnabled { get; set; } = true;
public bool PublisherConfirmationTrackingEnabled { get; set; } = true;
```

— is intentionally deferred. The Cronus contract guarantees at-least-once delivery for domain commands, events, and saga timeouts; turning confirms off silently breaks that guarantee. A global flag is too blunt: a user with mixed workloads (durable commands + ephemeral signals on one broker) gets the wrong answer either way. Per-publisher-type configuration is the right shape, but that's a more involved change. Park until a real user reports needing it.

## When you would want to disable confirms

Confirms are hardcoded ON because the dominant Cronus use case — domain commands and events — must not be silently lost. But there are real workloads where the latency cost is unacceptable and the loss is tolerable:

**Self-healing message streams** (the next message replaces the lost one):
- Heartbeats / liveness pings (one every 5s; the next arrives 5s later)
- Real-time presence and typing indicators (lost frame is invisible)
- High-frequency tick streams — price quotes, sensor readings, IoT telemetry (stale data is worse than missing; next tick supersedes)
- Live dashboards and metric streams (next refresh frame replaces it)

**Statistically-acceptable loss**:
- Application metrics (counter increments, request timings — 0.001% loss doesn't change dashboards)
- Distributed log shipping (logs are noisy by design; most loggers default to fire-and-forget)
- Click-stream / analytics events (trends matter, not exact counts)

**Higher-level retry already provides at-least-once**:
- Outbox pattern — local DB durability + background republisher
- Saga-driven retry — saga emits retries on timeout, inner publish doesn't need confirms
- Idempotent consumers + periodic reconciliation

**Hot-path producers in tight loops**:
- Migration tools publishing 100k events/sec (with confirms = 3,800 events/sec ceiling per channel; without = milliseconds)
- Game state / multiplayer position updates (60 fps × 10k players; confirms would make multiplayer unplayable)
- Real-time bidding / HFT signals (microsecond budgets; lifecycle has its own deadline-based ack)

**Cronus message types where confirms must stay ON**:
- `ICommand` publishes — user intent, must not lose
- `IEvent` publishes — read-model consistency depends on every event landing
- `IPublicEvent` publishes — cross-BC contract
- Saga timeout publishes via `Saga.RequestTimeoutAsync` — losing one wedges the saga
- Audit / compliance / billing events

## Methodology notes

- The "v11 sync resolver" measurement uses a synthetic `SyncPublisherChannelResolver` mirroring the master `ChannelResolverBase` `lock(obj) + Dictionary` pattern. The cached path is identical to v11 production semantics. Captured via a `git worktree` of `master`, then deleted.
- Confirms cost was isolated by parameterizing `PublishBenchmarks.ConfirmsEnabled = [false, true]` rather than running two builds.
- Concurrent benchmarks share a single `IChannel` across all parallel publishes — matches the production publisher path which caches channels per `(boundedContext, exchange, server)`.
- Benchmarks deliberately avoid Cronus's serialization, header-building, and namer logic to isolate the transport-layer cost. Real Cronus publishes will be modestly slower than these floor numbers.
