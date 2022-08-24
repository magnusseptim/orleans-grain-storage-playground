using Orleans;
using Orleans.Runtime;
using Orleans.Storage;

namespace GrainsStorages.Storages
{
    internal interface IGrainStorageWithLifecycle<T> : IGrainStorage, ILifecycleParticipant<T> where T : ISiloLifecycle
    {
    }
}
