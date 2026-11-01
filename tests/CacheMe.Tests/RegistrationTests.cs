using CacheMe.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace CacheMe.Tests;

public sealed class RegistrationTests
{
    private sealed class PlainStore : ICacheStore
    {
        public Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken) => Task.FromResult<CacheEntry?>(null);
        public Task SetAsync(string key, ReadOnlyMemory<byte> value, DateTimeOffset createdAt, TimeSpan ttl, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeLoader : ICacheLoader<Product>
    {
        public Task<Product?> LoadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Product?>(null);
    }

    private sealed class FakeWriter : ICacheWriter<Product>
    {
        public Task WriteAsync(string key, Product value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSerializer : ICacheSerializer
    {
        public byte[] Serialize<T>(T value) where T : class => [];
        public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class => throw new NotSupportedException();
    }

    private sealed class FakeHandler : IDeadLetterHandler
    {
        public Task HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private const string Valid = """
        {
          "CacheMe": {
            "KeyPrefix": "shop:",
            "DefaultTtl": "00:05:00",
            "Redis": { "ConnectionString": "localhost:6379" }
          }
        }
        """;

    private static IConfiguration Section(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["CacheMe:KeyPrefix"] = "shop:",
            ["CacheMe:DefaultTtl"] = "00:05:00",
            ["CacheMe:Redis:ConnectionString"] = "localhost:6379"
        };
        foreach (var (key, value) in settings)
        {
            if (value is null) values.Remove(key);
            else values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("CacheMe");
    }

    private static ServiceProvider Build(IConfiguration section, Action<CacheMeBuilder>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(section, cache =>
        {
            cache.UseRedis();
            cache.ForType<Product>(policy => policy.ReadThrough<FakeLoader>().RefreshAhead());
            extra?.Invoke(cache);
        });
        return services.BuildServiceProvider();
    }

    private static OptionsValidationException InvalidSettings(params (string Key, string? Value)[] settings)
    {
        using var provider = Build(Section(settings));
        return Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CacheMeOptions>>().Value);
    }

    private static void RegistrationFails(Action<CacheMeBuilder> configure)
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddCacheMe(Section(), configure));
    }

    [Fact, Trait("Requirement", "FR-001")]
    public void AddCacheMe_TypeRegistered_ResolvesCache()
    {
        using var provider = Build(Section());

        var cache = provider.GetRequiredService<ICache<Product>>();

        Assert.IsType<Cache<Product>>(cache);
    }

    [Fact, Trait("Requirement", "TD-006")]
    public void AddCacheMe_TypeRegistered_RegistersCacheAsSingleton()
    {
        using var provider = Build(Section());

        Assert.Same(provider.GetRequiredService<ICache<Product>>(), provider.GetRequiredService<ICache<Product>>());
    }

    [Fact, Trait("Requirement", "FR-001")]
    public void AddCacheMe_TypeNotRegistered_FailsWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(Section(), cache => cache.UseRedis());
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ICache<Product>>());
    }

    [Fact, Trait("Requirement", "TD-006")]
    public void ForType_LoaderNotRegistered_RegistersItAsScoped()
    {
        var services = new ServiceCollection();

        services.AddCacheMe(Section(), cache =>
        {
            cache.UseRedis();
            cache.ForType<Product>(policy => policy.ReadThrough<FakeLoader>());
        });

        Assert.Contains(services, d => d.ServiceType == typeof(FakeLoader) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact, Trait("Requirement", "TD-006")]
    public void ForType_LoaderAlreadyRegistered_KeepsRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FakeLoader>();

        services.AddCacheMe(Section(), cache =>
        {
            cache.UseRedis();
            cache.ForType<Product>(policy => policy.ReadThrough<FakeLoader>());
        });

        Assert.Single(services, d => d.ServiceType == typeof(FakeLoader) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void AddCacheMe_JsonConfiguration_BindsAllSettings()
    {
        const string json = """
            {
              "CacheMe": {
                "KeyPrefix": "shop:",
                "DefaultTtl": "00:04:00",
                "MaxTtl": "00:06:00",
                "RefreshThreshold": 0.6,
                "MaxValueBytes": 2048,
                "Redis": { "ConnectionString": "localhost:6379", "OperationTimeout": "00:00:02" },
                "WriteBehind": {
                  "MaxFlushDelay": "00:00:03",
                  "MaxBatchSize": 20,
                  "MaxQueueLength": 40,
                  "ShutdownTimeout": "00:00:04",
                  "Retry": { "MaxAttempts": 2, "InitialDelay": "00:00:05", "MaxDelay": "00:00:06" },
                  "DeadLetter": { "Retention": "2.00:00:00" }
                },
                "Policies": { "Product": { "DefaultTtl": "00:01:00", "MaxTtl": "00:02:00", "RefreshThreshold": 0.9 } }
              }
            }
            """;
        var section = new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build().GetSection("CacheMe");
        using var provider = Build(section);

        var options = provider.GetRequiredService<IOptions<CacheMeOptions>>().Value;

        Assert.Equal("shop:", options.KeyPrefix);
        Assert.Equal(TimeSpan.FromMinutes(4), options.DefaultTtl);
        Assert.Equal(TimeSpan.FromMinutes(6), options.MaxTtl);
        Assert.Equal(0.6, options.RefreshThreshold);
        Assert.Equal(2048, options.MaxValueBytes);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Redis.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), options.WriteBehind.MaxFlushDelay);
        Assert.Equal(20, options.WriteBehind.MaxBatchSize);
        Assert.Equal(40, options.WriteBehind.MaxQueueLength);
        Assert.Equal(TimeSpan.FromSeconds(4), options.WriteBehind.ShutdownTimeout);
        Assert.Equal(2, options.WriteBehind.Retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(5), options.WriteBehind.Retry.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(6), options.WriteBehind.Retry.MaxDelay);
        Assert.Equal(TimeSpan.FromDays(2), options.WriteBehind.DeadLetter.Retention);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Policies["Product"].DefaultTtl);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void Configure_SettingAlsoInJson_CodeWins()
    {
        using var provider = Build(Section(), cache => cache.Configure(o => o.KeyPrefix = "code:"));

        var options = provider.GetRequiredService<IOptions<CacheMeOptions>>().Value;

        Assert.Equal("code:", options.KeyPrefix);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void AddCacheMe_CodeOnly_UsesConfigureAndConnectionString()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(cache =>
        {
            cache.Configure(o =>
            {
                o.KeyPrefix = "shop:";
                o.DefaultTtl = TimeSpan.FromMinutes(5);
            });
            cache.UseRedis("localhost:6379");
            cache.ForType<Product>(policy => policy.ReadThrough<FakeLoader>());
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CacheMeOptions>>().Value;

        Assert.Equal("shop:", options.KeyPrefix);
        Assert.NotNull(provider.GetRequiredService<ICache<Product>>());
    }

    [Theory, Trait("Requirement", "FR-033")]
    [InlineData("")]
    [InlineData("has space")]
    public void Options_KeyPrefixInvalid_FailsValidation(string prefix)
    {
        var exception = InvalidSettings(("CacheMe:KeyPrefix", prefix));

        Assert.Contains("KeyPrefix", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-033")]
    public void Options_KeyPrefixOver128Characters_FailsValidation()
    {
        InvalidSettings(("CacheMe:KeyPrefix", new string('p', 129)));
    }

    [Fact, Trait("Requirement", "FR-034")]
    public void Options_DefaultTtlMissing_FailsValidation()
    {
        var exception = InvalidSettings(("CacheMe:DefaultTtl", null));

        Assert.Contains("DefaultTtl", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-034"), Trait("Requirement", "NFR-002")]
    public void Options_DefaultTtlOverMaxTtl_FailsValidation()
    {
        InvalidSettings(("CacheMe:DefaultTtl", "00:06:00"));
    }

    [Fact, Trait("Requirement", "FR-034"), Trait("Requirement", "NFR-002")]
    public void Options_PolicyDefaultTtlOverPolicyMaxTtl_NamesPolicyInFailure()
    {
        var exception = InvalidSettings(("CacheMe:Policies:Product:DefaultTtl", "00:10:00"));

        Assert.Contains("CacheMe:Policies:Product:DefaultTtl", exception.Message);
    }

    [Fact, Trait("Requirement", "NFR-002")]
    public void Options_PolicyRaisesMaxTtl_PassesValidation()
    {
        using var provider = Build(Section(
            ("CacheMe:Policies:Product:MaxTtl", "01:00:00"),
            ("CacheMe:Policies:Product:DefaultTtl", "01:00:00")));

        var options = provider.GetRequiredService<IOptions<CacheMeOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(1), options.Policies["Product"].MaxTtl);
    }

    [Theory, Trait("Requirement", "FR-009"), Trait("Requirement", "FR-022"), Trait("Requirement", "FR-025"), Trait("Requirement", "FR-026"), Trait("Requirement", "FR-028"), Trait("Requirement", "FR-029"), Trait("Requirement", "FR-038"), Trait("Requirement", "NFR-003")]
    [InlineData("CacheMe:MaxTtl", "00:00:00")]
    [InlineData("CacheMe:RefreshThreshold", "0")]
    [InlineData("CacheMe:RefreshThreshold", "1")]
    [InlineData("CacheMe:MaxValueBytes", "0")]
    [InlineData("CacheMe:MaxValueBytes", "536870913")]
    [InlineData("CacheMe:Redis:OperationTimeout", "00:00:00")]
    [InlineData("CacheMe:WriteBehind:MaxFlushDelay", "00:00:00")]
    [InlineData("CacheMe:WriteBehind:MaxBatchSize", "0")]
    [InlineData("CacheMe:WriteBehind:MaxQueueLength", "99")]
    [InlineData("CacheMe:WriteBehind:ShutdownTimeout", "00:00:00")]
    [InlineData("CacheMe:WriteBehind:Retry:MaxAttempts", "0")]
    [InlineData("CacheMe:WriteBehind:Retry:InitialDelay", "00:00:00")]
    [InlineData("CacheMe:WriteBehind:Retry:MaxDelay", "00:00:00.500")]
    [InlineData("CacheMe:WriteBehind:DeadLetter:Retention", "00:00:00")]
    [InlineData("CacheMe:Policies:Product:RefreshThreshold", "1")]
    public void Options_SettingOutOfRange_FailsValidation(string key, string value)
    {
        InvalidSettings((key, value));
    }

    [Theory, Trait("Requirement", "FR-009"), Trait("Requirement", "FR-038")]
    [InlineData("CacheMe:RefreshThreshold", "0.99")]
    [InlineData("CacheMe:MaxValueBytes", "536870912")]
    [InlineData("CacheMe:WriteBehind:MaxQueueLength", "100")]
    public void Options_SettingAtBoundary_PassesValidation(string key, string value)
    {
        using var provider = Build(Section((key, value)));

        Assert.NotNull(provider.GetRequiredService<IOptions<CacheMeOptions>>().Value);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void Options_SeveralInvalidSettings_ReportsAllTogether()
    {
        var exception = InvalidSettings(("CacheMe:KeyPrefix", ""), ("CacheMe:DefaultTtl", null));

        Assert.Contains("KeyPrefix", exception.Message);
        Assert.Contains("DefaultTtl", exception.Message);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void Options_UnknownKey_ThrowsInvalidOperationExceptionNamingIt()
    {
        using var provider = Build(Section(("CacheMe:MaxBatchSizes", "5")));

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<CacheMeOptions>>().Value);

        Assert.Contains("MaxBatchSizes", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-001")]
    public void Options_PolicyKeyHasNoRegisteredType_FailsValidation()
    {
        var exception = InvalidSettings(("CacheMe:Policies:Prodct:DefaultTtl", "00:01:00"));

        Assert.Contains("Prodct", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-008")]
    public void Options_ThresholdSetForTypeWithoutRefreshAhead_FailsValidation()
    {
        var section = Section(("CacheMe:Policies:Product:RefreshThreshold", "0.8"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(section, cache =>
        {
            cache.UseRedis();
            cache.ForType<Product>(policy => policy.CacheAside());
        });
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CacheMeOptions>>().Value);

        Assert.Contains("RefreshThreshold", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-036")]
    public void Options_UseRedisWithoutConnectionString_FailsValidation()
    {
        var exception = InvalidSettings(("CacheMe:Redis:ConnectionString", null));

        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact, Trait("Requirement", "TD-009")]
    public void Options_ConnectionStringListsTwoEndpoints_FailsValidation()
    {
        InvalidSettings(("CacheMe:Redis:ConnectionString", "one:6379,two:6379"));
    }

    [Fact, Trait("Requirement", "TD-011")]
    public void Options_ConnectionStringInvalid_DoesNotPutItInMessage()
    {
        const string secret = "one:6379,two:6379,password=s3cret-value";

        var exception = InvalidSettings(("CacheMe:Redis:ConnectionString", secret));

        Assert.DoesNotContain("s3cret-value", exception.Message);
    }

    [Fact, Trait("Requirement", "FR-036"), Trait("Requirement", "TD-009")]
    public void UseRedis_ExistingConnection_DoesNotNeedConnectionString()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(Section(("CacheMe:Redis:ConnectionString", null)), cache =>
        {
            cache.UseRedis(_ => connection.Object);
            cache.ForType<Product>(policy => policy.CacheAside());
        });
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IOptions<CacheMeOptions>>().Value);
    }

    [Fact, Trait("Requirement", "FR-036")]
    public void UseStore_CustomStore_DoesNotNeedRedisSettings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(Section(("CacheMe:Redis:ConnectionString", null)), cache =>
        {
            cache.UseStore<PlainStore>();
            cache.ForType<Product>(policy => policy.CacheAside());
        });
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<ICacheStore>(), provider.GetRequiredService<ICacheStore>());
        Assert.IsType<PlainStore>(provider.GetRequiredService<ICacheStore>());
    }

    [Fact, Trait("Requirement", "FR-036")]
    public void AddCacheMe_TwoStores_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().UseStore<PlainStore>());

    [Fact, Trait("Requirement", "FR-036")]
    public void AddCacheMe_NoStore_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.ForType<Product>(policy => policy.CacheAside()));

    [Fact, Trait("Requirement", "FR-036")]
    public void AddCacheMe_WriteBehindWithPlainStore_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseStore<PlainStore>().ForType<Product>(policy => policy.WriteBehind<FakeWriter>()));

    [Fact, Trait("Requirement", "FR-001")]
    public void ForType_SameTypeTwice_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis()
            .ForType<Product>(policy => policy.CacheAside())
            .ForType<Product>("Other", policy => policy.CacheAside()));

    [Fact, Trait("Requirement", "FR-001")]
    public void ForType_SameSettingsKeyTwice_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis()
            .ForType<Product>("Shared", policy => policy.CacheAside())
            .ForType<string>("Shared", policy => policy.CacheAside()));

    [Fact, Trait("Requirement", "FR-001")]
    public void ForType_NoStrategy_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(_ => { }));

    [Fact, Trait("Requirement", "FR-001")]
    public void CacheAside_ReadStrategyAlreadySet_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.ReadThrough<FakeLoader>().CacheAside()));

    [Fact, Trait("Requirement", "FR-001")]
    public void ReadThrough_ReadStrategyAlreadySet_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.CacheAside().ReadThrough<FakeLoader>()));

    [Fact, Trait("Requirement", "FR-001")]
    public void WriteBehind_WriteStrategyAlreadySet_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.WriteThrough<FakeWriter>().WriteBehind<FakeWriter>()));

    [Fact, Trait("Requirement", "FR-001")]
    public void WriteAround_WriteStrategyAlreadySet_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.WriteBehind<FakeWriter>().WriteAround<FakeWriter>()));

    [Fact, Trait("Requirement", "FR-001")]
    public void WriteThrough_WriteStrategyAlreadySet_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.WriteAround<FakeWriter>().WriteThrough<FakeWriter>()));

    [Fact, Trait("Requirement", "FR-008")]
    public void RefreshAhead_NoReadStrategy_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.RefreshAhead()));

    [Fact, Trait("Requirement", "FR-008")]
    public void RefreshAhead_WithCacheAside_ThrowsInvalidOperationException() =>
        RegistrationFails(cache => cache.UseRedis().ForType<Product>(policy => policy.CacheAside().RefreshAhead()));

    [Fact, Trait("Requirement", "FR-002")]
    public void ForType_ReadAndWriteStrategy_Registers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(Section(), cache =>
        {
            cache.UseRedis();
            cache.ForType<Product>(policy => policy.CacheAside().WriteBehind<FakeWriter>());
        });
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICache<Product>>());
    }

    [Fact, Trait("Requirement", "FR-035")]
    public void UseSerializer_CustomSerializer_ReplacesDefault()
    {
        using var provider = Build(Section(), cache => cache.UseSerializer<FakeSerializer>());

        Assert.IsType<FakeSerializer>(provider.GetRequiredService<ICacheSerializer>());
    }

    [Fact, Trait("Requirement", "FR-025")]
    public void UseDeadLetterHandler_AnyHandler_RegistersItAsScoped()
    {
        var services = new ServiceCollection();

        services.AddCacheMe(Section(), cache => cache.UseRedis().UseDeadLetterHandler<FakeHandler>());

        Assert.Contains(services, d => d.ServiceType == typeof(IDeadLetterHandler) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact, Trait("Requirement", "FR-026")]
    public void AddCacheMe_RedisStore_RegistersDeadLetterStore()
    {
        using var provider = Build(Section());

        Assert.NotNull(provider.GetService<IDeadLetterStore>());
    }

    [Fact, Trait("Requirement", "FR-026")]
    public void AddCacheMe_PlainStore_DoesNotRegisterDeadLetterStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheMe(Section(("CacheMe:Redis:ConnectionString", null)), cache =>
        {
            cache.UseStore<PlainStore>();
            cache.ForType<Product>(policy => policy.CacheAside());
        });
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IDeadLetterStore>());
    }

    [Fact, Trait("Requirement", "TD-009")]
    public void AddCacheMe_NoClockRegistered_UsesSystemClock()
    {
        using var provider = Build(Section());

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact, Trait("Requirement", "TD-009")]
    public void AddCacheMe_ClockAlreadyRegistered_KeepsIt()
    {
        var clock = new TestClock();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddCacheMe(Section(), cache => cache.UseRedis());
        using var provider = services.BuildServiceProvider();

        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void AddCacheMe_ValidJson_PassesValidation()
    {
        var section = new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Valid))).Build().GetSection("CacheMe");
        using var provider = Build(section);

        Assert.Equal("shop:", provider.GetRequiredService<IOptions<CacheMeOptions>>().Value.KeyPrefix);
    }
}
