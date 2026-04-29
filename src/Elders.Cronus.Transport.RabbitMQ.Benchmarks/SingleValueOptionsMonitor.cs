using System;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> that always returns the supplied value.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
internal sealed class SingleValueOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    private readonly TOptions value;

    public SingleValueOptionsMonitor(TOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
    }

    public TOptions CurrentValue => value;

    public TOptions Get(string? name) => value;

    public IDisposable OnChange(Action<TOptions, string> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new NoopDisposable();
        public void Dispose() { }
    }
}
