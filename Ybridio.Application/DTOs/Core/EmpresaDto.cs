namespace Ybridio.Application.DTOs.Core;

/// <summary>DTO de lectura para Empresa.</summary>
public sealed record EmpresaDto(int Id, string Nombre, string? RFC);

/// <summary>DTO para crear o actualizar una Empresa.</summary>
public sealed record UpsertEmpresaDto(string Nombre, string? RFC);
