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

    private IConsumer<string, T>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer = new ConsumerBuilder<string, T>(new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoOffsetStore = false
        }).SetValueDeserializer(new ProtobufDeserializer<T>().AsSyncOverAsync()).Build();
        _consumer.Subscribe(TopicId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // TODO: Dead-letter topic. Today a failed message is skipped silently once a later
                    // message stores its offset. Retry transient failures, then produce to <topic>.dlq
                    // with error headers and StoreOffset. Pending ADR on poison-message handling.
                    var consumeResult = _consumer.Consume(stoppingToken);
                    Console.WriteLine($"Consumed message '{consumeResult.Message.Key}':{consumeResult.Message.Value} at: '{consumeResult.TopicPartitionOffset}'");
                    var result = await Handle(serviceScopeFactory, consumeResult.Message.Value);

                    if (result.IsSuccess)
                        _consumer.StoreOffset(consumeResult);
                    else
                    {
                        Console.WriteLine($"Failed to handle message '{consumeResult.Message.Key}':{consumeResult.Message.Value} at: '{consumeResult.TopicPartitionOffset}'");
                    }

                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Consumer for {TopicId} stopping");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        finally
        {
            _consumer?.Close();
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}