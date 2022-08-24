namespace GrainInterfaces
{
    public interface IProcessingUnit : Orleans.IGrainWithIntegerKey
    {
        Task<string> Process(string payload);
    }
}
