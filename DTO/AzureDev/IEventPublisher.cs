namespace PasadorScript.DTO.AzureDev
{
    public interface IEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent evento);
    }
}
