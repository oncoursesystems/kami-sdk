using Ardalis.GuardClauses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OnCourse.Kami;
using OnCourse.Kami.Configuration;
using Polly;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddKamiClient(configuration, null);
    }

    /// <param name="configureResilience">
    /// Optional hook to customize the Polly v8 resilience pipeline (retry + timeout) applied to the Kami client.
    /// Leave null to use the retry/timeout values from <see cref="KamiOptions"/>.
    /// </param>
    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration, Action<ResiliencePipelineBuilder<HttpResponseMessage>>? configureResilience = null)
    {
        services.Configure<KamiOptions>(configuration.GetSection(KamiOptions.SectionName));

        var address = configuration["Kami:BaseAddress"];
        var token = configuration["Kami:Token"];

        Guard.Against.NullOrEmpty(address, nameof(address), "Missing Kami BaseAddress in settings.");
        Guard.Against.NullOrEmpty(token, nameof(token), "Missing Kami Token in settings.");

        services.AddHttpClient<IKamiClient, KamiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KamiOptions>>().Value;
            client.BaseAddress = new Uri(address);
            client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", token);

            // Cap the whole call (including retries) with HttpClient.Timeout rather than a Polly timeout
            // strategy. A timeout then surfaces to callers as a plain TaskCanceledException instead of a
            // Polly TimeoutRejectedException, and it still applies if a host opts the client out of
            // resilience handlers (e.g. RemoveAllResilienceHandlers under .NET Aspire defaults).
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddResilienceHandler("kami", (pipeline, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<KamiOptions>>().Value;

            // Retry transient failures (5xx, 408, network errors) with exponential backoff and jitter.
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });

            configureResilience?.Invoke(pipeline);
        });

        return services;
    }
}
