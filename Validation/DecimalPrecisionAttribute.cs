using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class DecimalPrecisionAttribute : ValidationAttribute
{
    private readonly int _decimalPlaces;

    public DecimalPrecisionAttribute(int decimalPlaces)
    {
        if (decimalPlaces < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalPlaces));
        }

        _decimalPlaces = decimalPlaces;
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not decimal number)
            return false;

        return decimal.Round(number, _decimalPlaces) == number;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must have at most {_decimalPlaces} decimal places.";
    }
}