namespace PasadorScript.DTO.AzureDev
{
    public interface IEventObserver<TEvent>
    {
        Task OnEventAsync(TEvent evento);
    }
}
