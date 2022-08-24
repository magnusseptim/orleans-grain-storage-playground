using GrainsStorages.Factories;
using GrainsStorages.StorageOptions;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Storage;

namespace GrainsStorages.Extensions
{
    public static class FileSiloBuilderExtensions
    {
        public static ISiloBuilder AddFileGrainStorage(this ISiloBuilder builder, string providerName, Action<FileGrainStorageOptions> options)
        {
            return builder.ConfigureServices(services => services.AddFileGrainStorage(providerName, options));
        }

       public static IServiceCollection AddFileGrainStorage(this IServiceCollection services, string providerName, Action<FileGrainStorageOptions> options)
       {
            services.AddOptions<FileGrainStorageOptions>(providerName).Configure(options);

            return services.AddSingletonNamedService(providerName, FileGrainStorageFactory.Create)
                .AddSingletonNamedService(
                    providerName,
                    (serviceProvider, name) => (ILifecycleParticipant<ISiloLifecycle>)serviceProvider.GetRequiredServiceByName<IGrainStorage>(name));
       }
    }
}
