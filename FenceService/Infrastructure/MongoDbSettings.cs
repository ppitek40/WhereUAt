namespace Infrastructure;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
}