using System;
using System.Collections.Generic;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>Nivel de severidad de una alerta de diagnóstico.</summary>
public enum AlertLevel { Info, Warning, Error }

/// <summary>Metadata de una pestaña del workspace en snapshot de diagnóstico.</summary>
public sealed record WorkspaceTabInfo(string Key, string Title, bool IsClosable, DateTime CreatedAt);

/// <summary>Alerta o advertencia detectada por el monitor de diagnóstico.</summary>
public sealed record DiagnosticAlert(string Icon, string Message, AlertLevel Level);

/// <summary>
/// Snapshot de seguridad runtime: roles, perfiles y permisos efectivos del usuario activo.
/// Se incluye en <see cref="RuntimeContextSnapshot"/> sin ejecutar queries adicionales
/// cuando ya se calculó previamente; de lo contrario queda como null.
/// </summary>
public sealed record SecurityRuntimeSnapshot(
    string               UsuarioNombre,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Perfiles,
    int                  CantidadPermisos,
    bool                 EsSuperAdmin,
    IReadOnlyList<int>   SucursalesPermitidas,
    IReadOnlyList<int>   AlmacentesPermitidos,
    DateTime             GeneradoEn);

/// <summary>
/// Snapshot inmutable del estado runtime del ERP capturado en un instante.
/// Incluye contexto de sesión, workspace activo, filtros EF aplicados,
/// última operación de grid reportada, contexto de seguridad y alertas derivadas.
/// </summary>
public sealed record RuntimeContextSnapshot(
    // ── Sesión / Contexto ────────────────────────────────────────────────────
    bool IsAuthenticated,
    Guid? UsuarioId,
    string UsuarioNombre,
    string? UsuarioEmail,
    string? UsuarioUserName,
    int EmpresaId,
    int SucursalId,
    string SucursalNombre,
    bool HasCajaActiva,
    string? CajaNombre,
    // ── Workspace ────────────────────────────────────────────────────────────
    int WorkspaceTabCount,
    string ActiveTabKey,
    string ActiveTabTitle,
    IReadOnlyList<WorkspaceTabInfo> WorkspaceTabs,
    // ── Filtros EF derivados ─────────────────────────────────────────────────
    bool HasEmpresaFilter,
    bool HasSucursalFilter,
    // ── Observabilidad operacional ────────────────────────────────────────────
    CurrentOperationalContext? CurrentContext,
    /// <summary>Historial de módulos visitados en la sesión (uno por módulo, más reciente primero).</summary>
    IReadOnlyList<ModuleNavigationEntry> NavigationHistory,
    string? ActiveModuleKey,
    GridOperationContext? LastOperation,
    IReadOnlyList<GridOperationContext> RecentOperations,
    // ── Seguridad runtime ────────────────────────────────────────────────────
    /// <summary>Snapshot de seguridad precalculado. Null si no se cargó o no hay sesión.</summary>
    SecurityRuntimeSnapshot? SecuritySnapshot,
    // ── Alertas ──────────────────────────────────────────────────────────────
    IReadOnlyList<DiagnosticAlert> Alerts,
    DateTime Timestamp
);
