using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;
using WhereUAt.SharedKernel;

namespace Api.Services;

public abstract class EventConsumerService<T>(IServiceScopeFactory serviceScopeFactory) : BackgroundService where T : class, IMessage<T>, new() 
{
    protected abstract string TopicId { get; }
    protected abstract string GroupId { get; }
    protected abstract Task<Result> Handle(IServiceScopeFactory serviceScopeFactory, T text);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, T>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).SetValueDeserializer(new ProtobufDeserializer<T>().AsSyncOverAsync()).Build();
        consumer.Subscribe(TopicId);
        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = consumer.Consume(stoppingToken);
            Console.WriteLine($"Consumed message '{cr.Message.Key}':{cr.Message.Value} at: '{cr.TopicPartitionOffset}'");
            await Handle(serviceScopeFactory, cr.Message.Value);
        }
        consumer.Close();
        consumer.Dispose();
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}