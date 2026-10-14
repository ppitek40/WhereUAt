using Confluent.Kafka;
using Infrastructure;
using WhereUAt.SharedKernel;

namespace Api.Services;

public abstract class EventConsumerService<T>(EventBroker eventBroker) : BackgroundService where T : class 
{
    protected abstract string TopicId { get; }
    protected abstract string GroupId { get; }
    protected abstract Task<Result> Handle(IServiceProvider serviceProvider, T text);
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        consumer.Subscribe(TopicId);
        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = consumer.Consume(stoppingToken);
            Console.WriteLine($"Consumed message '{cr.Message.Key}':{cr.Message.Value} at: '{cr.TopicPartitionOffset}'");
            Handle(cr.Message)
        }
        consumer.Close();
        consumer.Dispose();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}