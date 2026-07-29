using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Daraban.Platform.Messaging;

/// <summary>
/// Owns a single, lazily-created, long-lived IConnection for the process. Replaces what
/// MassTransit's bus instance used to manage internally -- with pure RabbitMQ.Client there's
/// no framework doing this for you, so it's a small piece worth having exactly once rather
/// than reimplemented per publisher/consumer.
/// NOTE: written against the RabbitMQ.Client 7.x async connection API. This has not been
/// compiled against a real NuGet restore (no SDK/NuGet access in the environment this was
/// written in) -- verify method names/overloads match your installed RabbitMQ.Client version
/// on first build.
/// </summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) => _options = options.Value;

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
            };
            _connection = await factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
