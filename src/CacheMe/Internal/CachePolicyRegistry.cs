using Microsoft.Extensions.DependencyInjection;

namespace CacheMe;

/// <summary>Maps a queued write's value type to its value type and writer.</summary>
internal sealed class CachePolicyRegistry
{
    private readonly Dictionary<string, RegisteredPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether any registered policy uses write-behind.</summary>
    internal bool UsesWriteBehind => _policies.Values.Any(p => p.UsesWriteBehind);

    /// <summary>Adds a policy to the map, under its settings key.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="policy">The policy.</param>
    internal void Add<T>(CachePolicy<T> policy) where T : class =>
        _policies.Add(policy.SettingsKey, new RegisteredPolicy<T>(policy));

    /// <summary>Finds a policy by its settings key.</summary>
    /// <param name="settingsKey">The settings key.</param>
    /// <param name="policy">The policy, if there is one.</param>
    /// <returns>Whether there is a policy for the key.</returns>
    internal bool TryGet(string settingsKey, out RegisteredPolicy? policy) =>
        _policies.TryGetValue(settingsKey, out policy);
}

/// <summary>A policy without its value type, so the queue can work with any registered type.</summary>
internal abstract class RegisteredPolicy
{
    internal abstract bool UsesRefreshAhead { get; }

    internal abstract bool UsesWriteBehind { get; }

    internal abstract object Deserialize(ICacheSerializer serializer, ReadOnlyMemory<byte> value);

    internal abstract Task WriteAsync(IServiceProvider services, string key, object value, CancellationToken cancellationToken);
}

internal sealed class RegisteredPolicy<T>(CachePolicy<T> policy) : RegisteredPolicy where T : class
{
    internal override bool UsesRefreshAhead => policy.RefreshAhead;

    internal override bool UsesWriteBehind => policy.WriteStrategy == WriteStrategy.WriteBehind;

    internal override object Deserialize(ICacheSerializer serializer, ReadOnlyMemory<byte> value) =>
        serializer.Deserialize<T>(value);

    internal override Task WriteAsync(IServiceProvider services, string key, object value, CancellationToken cancellationToken)
    {
        var writer = (ICacheWriter<T>)services.GetRequiredService(policy.WriterType!);
        return writer.WriteAsync(key, (T)value, cancellationToken);
    }
}
