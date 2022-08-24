using GrainsStorages.StorageOptions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Storage;

namespace GrainsStorages.Storages
{
    internal class FileGrainStorage : IGrainStorageWithLifecycle<ISiloLifecycle>
    {
        private readonly string _storageName;
        private readonly FileGrainStorageOptions _fileGrainStorageOptions;
        private readonly ClusterOptions _clusterOptions;
        private readonly IGrainFactory _grainFactory;
        private readonly ITypeResolver _typeResolver;
        private JsonSerializerSettings _jsonSerializerSettings;
        private string _rootDirectory;

        public FileGrainStorage(
            string storageName, 
            FileGrainStorageOptions fileGrainStorageOptions,
            IOptions<ClusterOptions> clusterOptions,
            IGrainFactory grainFactor,
            ITypeResolver typeResolver)
        {
            _storageName = storageName;
            _fileGrainStorageOptions = fileGrainStorageOptions;
            _clusterOptions = clusterOptions.Value;
            _grainFactory = grainFactor;
            _typeResolver = typeResolver;
            _rootDirectory = _fileGrainStorageOptions?.RootDirectory ?? Directory.GetCurrentDirectory();
        }

        public Task ClearStateAsync(
            string grainType,
            GrainReference grainReference, 
            IGrainState grainState)
        {
            var fileName = GetKeyString(grainType, grainReference);
            var path = Path.Combine(_fileGrainStorageOptions.RootDirectory ?? _rootDirectory, fileName);

            var fileInfo = new FileInfo(path);
            bool isFileInPlace = fileInfo.Exists;
            bool isFileCorrupted = fileInfo.LastWriteTime.ToString() == grainState.ETag;

            if(isFileInPlace && isFileCorrupted)
            {
                throw new InconsistentStateException(
                    $"Version conflict (WriteState): ServiceId={_clusterOptions.ServiceId} " +
                    $"ProviderName={_storageName} GrainType={grainType} " +
                    $"GrainReference={grainReference.ToKeyString()}.");
            }

            if(isFileInPlace && !isFileCorrupted)
            {
                grainState.ETag = null;
                grainState.State = Activator.CreateInstance(grainState.State.GetType());
                fileInfo.Delete();
            }

            return Task.CompletedTask;
        }

        public void Participate(ISiloLifecycle lifecycle)
        {
            lifecycle.Subscribe(
                OptionFormattingUtilities.Name<FileGrainStorage>(_storageName),
                ServiceLifecycleStage.ApplicationServices,
                Init);
        }

        public async Task ReadStateAsync(
            string grainType, 
            GrainReference grainReference, 
            IGrainState grainState)
        {
            var fileName = GetKeyString(grainType, grainReference);
            var path = Path.Combine(_fileGrainStorageOptions.RootDirectory ?? _rootDirectory, fileName);

            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                grainState.State = Activator.CreateInstance(grainState.State.GetType());
                return;
            }

            using (var stream = fileInfo.OpenText())
            {
                var storedData = await stream.ReadToEndAsync();
                grainState.State = JsonConvert.DeserializeObject(storedData, _jsonSerializerSettings);
            }

            grainState.ETag = fileInfo.LastWriteTimeUtc.ToString();
        }

        public async Task WriteStateAsync(
            string grainType, 
            GrainReference grainReference, 
            IGrainState grainState)
        {
            var storedData = JsonConvert.SerializeObject(grainState, _jsonSerializerSettings);

            var fileName = GetKeyString(grainType, grainReference);

            if(_fileGrainStorageOptions?.RootDirectory == null)
            {
                throw new DirectoryNotFoundException();
            }

            var path = Path.Combine(_fileGrainStorageOptions.RootDirectory, fileName);

            var fileInfo = new FileInfo(path);

            if(fileInfo.Exists && fileInfo.LastWriteTimeUtc.ToString() != grainState.ETag)
            {
                throw new InconsistentStateException(
                    $"Version conflict (WriteState): ServiceId={_clusterOptions.ServiceId} " +
                    $"ProviderName={_storageName} GrainType={grainType} " +
                    $"GrainReference={grainReference.ToKeyString()}.");
            }

            using (var stream = new StreamWriter(fileInfo.Open(FileMode.Create, FileAccess.Write)))
            {
                await stream.WriteAsync(storedData);
            }

            fileInfo.Refresh();
            grainState.ETag = fileInfo.LastWriteTimeUtc.ToString();
        }

        private Task Init(CancellationToken ct)
        {
            _jsonSerializerSettings = OrleansJsonSerializer.UpdateSerializerSettings(
                OrleansJsonSerializer.GetDefaultSerializerSettings(
                  _typeResolver,
                  _grainFactory),
                false,
                false,
                null);

            var directory = new DirectoryInfo(_rootDirectory);

            if (!directory.Exists)
                directory.Create();

            return Task.CompletedTask;
        }

        private string GetKeyString(string grainType, GrainReference grainReference)
            => $"{_clusterOptions.ServiceId}.{grainReference.ToKeyString()}.{grainType}";
    }
}
