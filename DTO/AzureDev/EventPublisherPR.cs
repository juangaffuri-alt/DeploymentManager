namespace PasadorScript.DTO.AzureDev
{
    public class EventPublisherPR : IEventPublisher
    {
        private readonly IServiceProvider _serviceProvider;

        public EventPublisherPR(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync<TEvent>(TEvent evento)
        {
            using var scope = _serviceProvider.CreateScope();

            var handlers = scope.ServiceProvider
                .GetServices<IEventObserver<TEvent>>()
                .ToList();

            foreach (var handler in handlers)
            {
                await handler.OnEventAsync(evento);
            }
        }
    }
}
