using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CacheMe;

/// <summary>Checks every setting, and reports all failures together when the application starts.</summary>
internal sealed class CacheMeOptionsValidator(CachePolicyRegistry policies, bool connectionStringRequired) : IValidateOptions<CacheMeOptions>
{
    private const int MaxKeyPrefixLength = 128;
    private const int RedisMaxValueBytes = 536_870_912;

    public ValidateOptionsResult Validate(string? name, CacheMeOptions options)
    {
        var failures = new List<string>();

        ValidateGeneral(options, failures);
        ValidatePolicies(options, failures);
        ValidateRedis(options.Redis, failures);
        ValidateWriteBehind(options.WriteBehind, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateGeneral(CacheMeOptions options, List<string> failures)
    {
        if (string.IsNullOrEmpty(options.KeyPrefix) || options.KeyPrefix.Any(char.IsWhiteSpace) || options.KeyPrefix.Length > MaxKeyPrefixLength)
        {
            failures.Add($"CacheMe:KeyPrefix is required and must have no whitespace and at most {MaxKeyPrefixLength} characters.");
        }

        if (options.DefaultTtl is not { } ttl || ttl <= TimeSpan.Zero)
        {
            failures.Add("CacheMe:DefaultTtl is required and must be greater than zero.");
        }
        else if (ttl > options.MaxTtl)
        {
            failures.Add($"CacheMe:DefaultTtl ({ttl}) must not exceed MaxTtl ({options.MaxTtl}).");
        }

        Positive("CacheMe:MaxTtl", options.MaxTtl, failures);
        Threshold("CacheMe:RefreshThreshold", options.RefreshThreshold, failures);

        if (options.MaxValueBytes is < 1 or > RedisMaxValueBytes)
        {
            failures.Add($"CacheMe:MaxValueBytes must be from 1 to {RedisMaxValueBytes}.");
        }
    }

    private void ValidatePolicies(CacheMeOptions options, List<string> failures)
    {
        foreach (var (key, settings) in options.Policies)
        {
            var path = $"CacheMe:Policies:{key}";
            if (!policies.TryGet(key, out var policy))
            {
                failures.Add($"{path} does not match a registered value type.");
                continue;
            }

            if (settings.DefaultTtl is { } defaultTtl)
            {
                Positive($"{path}:DefaultTtl", defaultTtl, failures);
            }

            if (settings.MaxTtl is { } maxTtl)
            {
                Positive($"{path}:MaxTtl", maxTtl, failures);
            }

            var effectiveDefault = settings.DefaultTtl ?? options.DefaultTtl;
            var effectiveMax = settings.MaxTtl ?? options.MaxTtl;
            if (effectiveDefault > effectiveMax)
            {
                failures.Add($"{path}:DefaultTtl ({effectiveDefault}) must not exceed MaxTtl ({effectiveMax}).");
            }

            if (settings.RefreshThreshold is { } threshold)
            {
                Threshold($"{path}:RefreshThreshold", threshold, failures);
                if (!policy!.UsesRefreshAhead)
                {
                    failures.Add($"{path}:RefreshThreshold is set, but {key} does not use refresh-ahead.");
                }
            }
        }
    }

    private void ValidateRedis(RedisOptions redis, List<string> failures)
    {
        Positive("CacheMe:Redis:OperationTimeout", redis.OperationTimeout, failures);
        if (!connectionStringRequired)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(redis.ConnectionString))
        {
            failures.Add("CacheMe:Redis:ConnectionString is required.");
            return;
        }

        // The message must not include the connection string, because it can hold a password.
        try
        {
            if (ConfigurationOptions.Parse(redis.ConnectionString).EndPoints.Count != 1)
            {
                failures.Add("CacheMe:Redis:ConnectionString is invalid or lists more than one endpoint.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or FormatException)
        {
            failures.Add("CacheMe:Redis:ConnectionString is invalid or lists more than one endpoint.");
        }
    }

    private static void ValidateWriteBehind(WriteBehindOptions writeBehind, List<string> failures)
    {
        Positive("CacheMe:WriteBehind:MaxFlushDelay", writeBehind.MaxFlushDelay, failures);
        Positive("CacheMe:WriteBehind:ShutdownTimeout", writeBehind.ShutdownTimeout, failures);
        Positive("CacheMe:WriteBehind:Retry:InitialDelay", writeBehind.Retry.InitialDelay, failures);
        Positive("CacheMe:WriteBehind:DeadLetter:Retention", writeBehind.DeadLetter.Retention, failures);

        if (writeBehind.MaxBatchSize < 1)
        {
            failures.Add("CacheMe:WriteBehind:MaxBatchSize must be at least 1.");
        }

        if (writeBehind.MaxQueueLength < writeBehind.MaxBatchSize)
        {
            failures.Add("CacheMe:WriteBehind:MaxQueueLength must be at least MaxBatchSize.");
        }

        if (writeBehind.Retry.MaxAttempts < 1)
        {
            failures.Add("CacheMe:WriteBehind:Retry:MaxAttempts must be at least 1.");
        }

        if (writeBehind.Retry.MaxDelay < writeBehind.Retry.InitialDelay)
        {
            failures.Add("CacheMe:WriteBehind:Retry:MaxDelay must be at least InitialDelay.");
        }
    }

    private static void Positive(string key, TimeSpan value, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{key} must be greater than zero.");
        }
    }

    private static void Threshold(string key, double value, List<string> failures)
    {
        if (value is <= 0 or >= 1)
        {
            failures.Add($"{key} must be greater than 0 and less than 1.");
        }
    }
}
