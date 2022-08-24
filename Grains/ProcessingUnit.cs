using GrainInterfaces;
using Grains.GrainStateContainers;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace Grains
{
    [StorageProvider(ProviderName = "HostFileStorage")]
    public class ProcessingUnit : Orleans.Grain<ProcessingUnitState>, IProcessingUnit
    {
        private readonly ILogger _logger;

        public ProcessingUnit(ILogger<ProcessingUnit> logger)
        {
            _logger = logger;
        }

        public async Task<string> Process(string payload)
        {
            _logger.LogInformation("ProcessingUnit payload received: '{Payload}'", payload);
            // ... Processing take place here ... //

            if(this.State.ProcessingUnitHistory == null)
            {
                this.State.ProcessingUnitHistory = new List<string>();
            }    

            this.State.ProcessingUnitHistory.Add(payload);

            await this.WriteStateAsync();

            return $"\n Client send payload to process: '{payload}', processing status: PROCESSED";
        }
    }
}
