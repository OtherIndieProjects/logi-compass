using System.Collections.Concurrent;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Abstractions;

namespace AAALogistics.Infrastructure.Identity;

/// <summary>In-memory single-use denylist for step-up JWT <c>jti</c> (TTL = token lifetime).</summary>
public sealed class MemoryStepUpJtiStore(IClock clock) : IStepUpJtiStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _consumed = new(StringComparer.Ordinal);
    private readonly IClock _clock = clock;

    public bool TryConsume(string jti, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        PurgeExpired();
        var expires = expiresAtUtc <= _clock.UtcNow
            ? _clock.UtcNow.AddMinutes(1)
            : expiresAtUtc;
        return _consumed.TryAdd(jti.Trim(), expires);
    }

    private void PurgeExpired()
    {
        var now = _clock.UtcNow;
        foreach (var pair in _consumed)
        {
            if (pair.Value <= now)
            {
                _consumed.TryRemove(pair.Key, out _);
            }
        }
    }
}
