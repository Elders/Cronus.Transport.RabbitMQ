# Cronus.Transport.RabbitMQ Benchmarks

Manual perf measurements for the RabbitMQ transport. Not run by CI — invoke locally.

## Prerequisites

A RabbitMQ broker on `localhost:5672` (default credentials `guest/guest`):

```bash
docker run -d --rm -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management
```

## Run

From the repo root:

```bash
dotnet run -c Release --project src/Elders.Cronus.Transport.RabbitMQ.Benchmarks -- --filter "*"
```

To run a specific benchmark class:

```bash
dotnet run -c Release --project src/Elders.Cronus.Transport.RabbitMQ.Benchmarks -- --filter "*ChannelResolutionBenchmarks*"
```

Results land in `BenchmarkDotNet.Artifacts/` next to the project. The `MarkdownExporter` output is the most readable.

## What's measured

- **`ChannelResolutionBenchmarks`**: throughput of `PublisherChannelResolver.ResolveAsync` under varying parallelism (1, 10, 50, 100). Validates whether the static `SemaphoreSlim` lock in `ChannelResolverBase` becomes a contention bottleneck.

## Why this isn't in CI

The CI pipeline (`ci/azure-pipelines.yml`) builds `src/$(PROJECT_DIR)/*.csproj` (where `PROJECT_DIR=Elders.Cronus.Transport.RabbitMQ`) and tests `**/*Tests.csproj`. Neither glob matches this project, so it's only built and run on demand.
