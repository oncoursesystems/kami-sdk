using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kami.Configuration;
using Polly;

namespace Kami;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddKamiClient(configuration, null);
    }

    public static IServiceCollection AddKamiClient(this IServiceCollection services, IConfiguration configuration, Func<PolicyBuilder<HttpResponseMessage>, IAsyncPolicy<HttpResponseMessage>>? errorPolicy = null)
    {
        services.Configure<KamiOptions>(configuration.GetSection(KamiOptions.SectionName));

        services.AddHttpClient<IKamiClient, KamiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Kami:BaseAddress"]);
            client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", configuration["Kami:Token"]);
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
