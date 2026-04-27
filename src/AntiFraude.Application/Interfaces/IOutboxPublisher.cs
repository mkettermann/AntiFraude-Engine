namespace AntiFraude.Application.Interfaces;

public interface IOutboxPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
