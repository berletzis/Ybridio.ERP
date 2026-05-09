namespace Ybridio.Application.DTOs.Ventas;

/// <summary>
/// DTO de lectura de un cliente. Incluye los campos extendidos del Sales Core.
/// </summary>
/// <remarks>
/// SaldoPendiente no se persiste — se calcula en runtime sumando CxC pendientes.
/// Ver §25 CLAUDE_RULES.md y docs/VENTAS_OPERATIVAS.md → Fórmulas.
/// </remarks>
public sealed record ClienteDto(
    int      Id,
    int      EmpresaId,
    string   Nombre,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    decimal  LimiteCredito);

/// <summary>DTO para crear un cliente nuevo.</summary>
public sealed record CrearClienteDto(
    int      EmpresaId,
    string   Nombre,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    decimal  LimiteCredito);

/// <summary>DTO para actualizar datos de un cliente existente.</summary>
public sealed record ActualizarClienteDto(
    string   Nombre,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    decimal  LimiteCredito);
