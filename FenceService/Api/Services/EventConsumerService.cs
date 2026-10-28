using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using WhereUAt.SharedKernel;

namespace Api.Services;

public abstract class EventConsumerService<T>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<KafkaSettings> kafkaSettings) : BackgroundService where T : class, IMessage<T>, new()
{
    protected abstract string TopicId { get; }
    protected abstract string GroupId { get; }
    protected abstract Task<Result> Handle(IServiceScopeFactory serviceScopeFactory, T text);

    private IConsumer<string, T> _consumer = null!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer = new ConsumerBuilder<string, T>(new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).SetValueDeserializer(new ProtobufDeserializer<T>().AsSyncOverAsync()).Build();
        _consumer.Subscribe(TopicId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer.Consume(stoppingToken);
                Console.WriteLine($"Consumed message '{cr.Message.Key}':{cr.Message.Value} at: '{cr.TopicPartitionOffset}'");
                await Handle(serviceScopeFactory, cr.Message.Value);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}