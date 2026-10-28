namespace Api.Services;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public required string BootstrapServers { get; init; }
}