using Vogen;

namespace Domain.ValueObjects;

[ValueObject<string>]
public readonly partial struct FenceName
{
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Fence name cannot be empty");

        if (input.Length > 200)
            return Validation.Invalid("Fence name cannot be longer than 200 characters");

        return Validation.Ok;
    }
}