using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateFenceHandler>();
        services.AddScoped<ModifyFenceHandler>();
        services.AddScoped<DeleteFenceHandler>();

        return services;
    }
}