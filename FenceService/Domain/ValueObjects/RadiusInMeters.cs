using Vogen;

namespace Domain.ValueObjects;

[ValueObject<int>]
public readonly partial struct RadiusInMeters
{
    private static Validation Validate(int input)
    {
        if (input < 1)
            return Validation.Invalid("Radius must be greater than 0");

        if (input > 1000)
            return Validation.Invalid("Radius must be less than 1000");

        return Validation.Ok;
    }
}