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

public readonly record struct Location(Latitude Lat, Longitude Lng);