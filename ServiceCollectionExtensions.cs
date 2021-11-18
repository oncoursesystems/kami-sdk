using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnCourse.Kami.Configuration;
using Polly;

namespace OnCourse.Kami;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseKami(this IServiceCollection services, IConfiguration configuration, Func<PolicyBuilder<HttpResponseMessage>, IAsyncPolicy<HttpResponseMessage>>? errorPolicy)
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
