using Microsoft.Extensions.Configuration;
using Kami.Configuration;
using Polly;
using Kami;
using Ardalis.GuardClauses;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddKamiClient(configuration, null);
    }

    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration, Func<PolicyBuilder<HttpResponseMessage>, IAsyncPolicy<HttpResponseMessage>>? errorPolicy = null)
    {
        services.Configure<KamiOptions>(configuration.GetSection(KamiOptions.SectionName));

        var address = configuration["Kami:BaseAddress"];
        var token = configuration["Kami:Token"];

        Guard.Against.NullOrEmpty(address, "Kami:BassAddress", "Missing Kami BaseAddress in settings.");
        Guard.Against.NullOrEmpty(token, "Kami:Token", "Missing Kami Token in settings.");

        services.AddHttpClient<IKamiClient, KamiClient>(client =>
        {
            client.BaseAddress = new Uri(address);
            client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", token);
        })
        .AddTransientHttpErrorPolicy(errorPolicy ?? (p => p.WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        })));

        return services;
    }
}
