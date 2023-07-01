using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Utils
{
    public static class ServiceActivator
    {
        public static IServiceCollection AddDiscordServices(this IServiceCollection collection)
        {
            foreach (var type in typeof(Program).Assembly.GetTypes())
            {
                if (typeof(Service).IsAssignableFrom(type) && !type.IsAbstract)
                    collection.AddSingleton(type);
            }
            return collection;
        }

        public static void ActivateAllDiscordServices(this IServiceProvider provider)
        {
            foreach (var type in typeof(Program).Assembly.GetTypes())
            {
                if (typeof(Service).IsAssignableFrom(type) && !type.IsAbstract)
                    ((Service)provider.GetRequiredService(type)).Activate();
            }
        }
    }
}
