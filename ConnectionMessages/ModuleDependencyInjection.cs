using ConnectionMessages.Configuration;
using ConnectionMessages.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectionMessages;

internal static class ModuleDependencyInjection
{
    internal static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMessagesConfig, ConnectionMessagesConfig>();

        services.AddSingleton<ConnectionMessagesModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<ConnectionMessagesModule>());

        return services;
    }
}
