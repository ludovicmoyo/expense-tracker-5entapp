using System.Globalization;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StringEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Color hint for a status badge — keeps colors out of every XAML page.
/// </summary>
public sealed class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExpenseStatus s ? s switch
        {
            ExpenseStatus.Draft => Color.FromArgb("#9CA3AF"),       // gray
            ExpenseStatus.Submitted => Color.FromArgb("#2563EB"),   // blue
            ExpenseStatus.Resubmitted => Color.FromArgb("#7C3AED"), // violet
            ExpenseStatus.Approved => Color.FromArgb("#16A34A"),    // green
            ExpenseStatus.Rejected => Color.FromArgb("#DC2626"),    // red
            _ => Colors.Transparent
        } : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Formats a decimal amount with its currency code: "42.50 EUR".
/// Use as: {Binding Amount, Converter={StaticResource AmountConverter}, ConverterParameter={Binding Currency}}
/// Currency must be passed separately because MultiBinding is verbose in MAUI.
/// </summary>
public sealed class AmountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d) return d.ToString("0.00", CultureInfo.InvariantCulture);
        return value?.ToString() ?? string.Empty;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
