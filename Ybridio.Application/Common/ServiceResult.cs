namespace Ybridio.Application.Common;

/// <summary>
/// Categorías de error de negocio para que la UI pueda reaccionar de forma específica
/// sin interpretar cadenas de texto.
/// </summary>
public enum ErrorCode
{
    None = 0,

    // ── Genérico ──────────────────────────────────────────────────────────────
    NotFound,
    ValidationFailed,
    Unauthorized

    // ── Autenticación ─────────────────────────────────────────────────────────
    InvalidCredentials,
    UserInactive,

    // ── Caja ──────────────────────────────────────────────────────────────────
    CajaNotFound,
    CajaAlreadyOpen,
    CajaAlreadyClosed,
    CajaNotOpen,
    CajaTiendaMismatch,

    // ── Venta ─────────────────────────────────────────────────────────────────
    VentaNotFound,
    VentaNoDetalles,
    VentaUsuarioTiendaMismatch,
    VentaCajaTiendaMismatch,
    VentaCajaNotOpen,

    // ── Inventario ────────────────────────────────────────────────────────────
    StockInsuficiente,
    ExistenciaNotFound,
    ConcurrencyConflict,

    // ── Infraestructura / inesperado ──────────────────────────────────────────
    Unknown,
}

/// <summary>
/// Encapsula el resultado de una operación de servicio, evitando el uso de excepciones
/// para flujos de negocio esperados.
/// </summary>
/// <typeparam name="T">Tipo del valor retornado en caso de éxito.</typeparam>
public sealed class ServiceResult<T>
{
    private ServiceResult() { }

    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public ErrorCode ErrorCode { get; private init; }

    /// <summary>
    /// Información adicional de diagnóstico (p.ej. "ProductoId:123").
    /// Nunca mostrar directamente al usuario final; usar para logs y depuración.
    /// </summary>
    public string? Details { get; private init; }

    /// <summary>Crea un resultado exitoso con el valor dado.</summary>
    public static ServiceResult<T> Ok(T value) =>
        new() { Success = true, Value = value, ErrorCode = ErrorCode.None };

    /// <summary>Crea un resultado de fallo con mensaje y código de error.</summary>
    public static ServiceResult<T> Fail(string error, ErrorCode code = ErrorCode.ValidationFailed) =>
        new() { Success = false, Error = error, ErrorCode = code };

    /// <summary>Crea un resultado de fallo con mensaje, código de error y detalle de diagnóstico.</summary>
    public static ServiceResult<T> Fail(string error, ErrorCode code, string? details) =>
        new() { Success = false, Error = error, ErrorCode = code, Details = details };

    /// <summary>Proyecta el valor en caso de éxito; propaga error, código y detalles si falló.</summary>
    public ServiceResult<TOut> Map<TOut>(Func<T, TOut> selector) =>
        Success
            ? ServiceResult<TOut>.Ok(selector(Value!))
            : ServiceResult<TOut>.Fail(Error!, ErrorCode, Details);
}

/// <summary>
/// Resultado de operación sin valor de retorno.
/// </summary>
public sealed class ServiceResult
{
    private ServiceResult() { }

    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public ErrorCode ErrorCode { get; private init; }

    /// <summary>
    /// Información adicional de diagnóstico (p.ej. "ProductoId:123").
    /// Nunca mostrar directamente al usuario final; usar para logs y depuración.
    /// </summary>
    public string? Details { get; private init; }

    public static ServiceResult Ok() =>
        new() { Success = true, ErrorCode = ErrorCode.None };

    public static ServiceResult Fail(string error, ErrorCode code = ErrorCode.ValidationFailed) =>
        new() { Success = false, Error = error, ErrorCode = code };

    /// <summary>Fallo con detalle de diagnóstico adicional.</summary>
    public static ServiceResult Fail(string error, ErrorCode code, string? details) =>
        new() { Success = false, Error = error, ErrorCode = code, Details = details };
}
