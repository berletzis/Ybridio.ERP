using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Formatea una fecha como texto operacional legible: "8 Junio 2026".
/// </summary>
/// <remarks>
/// Operational Date Display Pattern:
/// - Formato: {día} {MesCompleto} {año}
/// - Mes: nombre completo en español, primera letra mayúscula
/// - Evita formatos ambiguos numéricos como "06/08/26"
/// - Acepta DateTimeOffset, DateTimeOffset?, DateTime, DateTime?
/// Reutilizable en cualquier campo de fecha del ERP.
/// </remarks>
public sealed class OperationalDateConverter : IValueConverter
{
    private static readonly string[] _meses =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    /// <summary>
    /// Formatea la fecha en "8 Junio 2026".
    /// Devuelve string vacío si la fecha es null.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var date = ExtractDate(value);
        return date.HasValue
            ? FormatOperationalDate(date.Value)
            : string.Empty;
    }

    /// <summary>No soportado — solo se usa en dirección UI.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();

    /// <summary>
    /// Formatea una fecha como texto operacional: "8 Junio 2026".
    /// Método público para uso programático (ej. en code-behind).
    /// </summary>
    public static string FormatOperationalDate(DateTimeOffset date)
        => $"{date.Day} {_meses[date.Month - 1]} {date.Year}";

    /// <summary>
    /// Sobrecarga para DateTime.
    /// </summary>
    public static string FormatOperationalDate(DateTime date)
        => FormatOperationalDate(new DateTimeOffset(date));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateTimeOffset? ExtractDate(object? value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime        dt => new DateTimeOffset(dt),
        _                  => null
    };
}
