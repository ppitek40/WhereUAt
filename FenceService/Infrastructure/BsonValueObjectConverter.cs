using Domain.ValueObjects;
using Vogen;

namespace Infrastructure;

[BsonSerializer<FenceId>]
[BsonSerializer<FenceName>]
[BsonSerializer<Latitude>]
[BsonSerializer<Longitude>]
[BsonSerializer<CreatorId>]
[BsonSerializer<RadiusInMeters>]
[BsonSerializer<TargetId>]
public partial class BsonValueObjectConverter;