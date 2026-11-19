using Vogen;

namespace Domain.ValueObjects;

[ValueObject<double>]
public readonly partial struct Latitude
{
    private static Validation Validate(double input)
    {
        if (input is < -90 or > 90)
            return Validation.Invalid("Latitude must be between -90 and 90");

        return Validation.Ok;
    }
}

[ValueObject<double>]
public readonly partial struct Longitude
{
    private static Validation Validate(double input)
    {
        if (input is < -180 or > 180)
            return Validation.Invalid("Longitude must be between -180 and 180");

        return Validation.Ok;
    }
}

public readonly record struct Location(Latitude Lat, Longitude Lng)
{
    private const double EarthRadiusInMeters = 6_371_000;

    public double DistanceTo(Location other)
    {
        var lat1 = ToRadians(Lat.Value);
        var lat2 = ToRadians(other.Lat.Value);
        var dLat = lat2 - lat1;
        var dLng = ToRadians(other.Lng.Value - Lng.Value);

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLng / 2), 2);

        var c = 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));

        return EarthRadiusInMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}