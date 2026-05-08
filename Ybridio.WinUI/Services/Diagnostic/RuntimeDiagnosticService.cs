using System;
using System.Collections.Generic;
using System.Linq;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.WinUI.Services.Workspace;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Agrega el estado runtime del ERP en un <see cref="RuntimeContextSnapshot"/> inmutable.
/// Combina: sesión, workspace, contexto activo (ICurrentContextTracker),
/// última operación (IOperationalObservabilityService) y seguridad runtime,
/// sin ejecutar queries adicionales a BD (usa datos precalculados de SecurityContextDto).
/// </summary>
public sealed class RuntimeDiagnosticService
{
    private readonly SessionService                    _session;
    private readonly IWorkspaceService                 _workspace;
    private readonly IOperationalObservabilityService  _observability;
    private readonly ICurrentContextTracker            _contextTracker;

    public RuntimeDiagnosticService(
        SessionService                   session,
        IWorkspaceService                workspace,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
    {
        _session        = session;
        _workspace      = workspace;
        _observability  = observability;
        _contextTracker = contextTracker;
    }

    /// <summary>
    /// Captura el estado actual en un snapshot inmutable. Sin queries a BD.
    /// El parámetro <paramref name="securityContext"/> es opcional: si se provee,
    /// se incluye el snapshot de seguridad en el resultado.
    /// </summary>
    public RuntimeContextSnapshot GetSnapshot(SecurityContextDto? securityContext = null)
    {
        var tabs = _workspace.Tabs
            .Select(t => new WorkspaceTabInfo(t.Key, t.Title, t.IsClosable, t.CreatedAt))
            .ToList();

        var securitySnapshot = securityContext is not null
            ? new SecurityRuntimeSnapshot(
                UsuarioNombre:        securityContext.UsuarioNombre,
                Roles:                securityContext.Roles,
                Perfiles:             securityContext.Perfiles,
                CantidadPermisos:     securityContext.PermisosEfectivos.Count,
                EsSuperAdmin:         securityContext.EsSuperAdmin,
                SucursalesPermitidas: securityContext.SucursalesPermitidas,
                AlmacentesPermitidos: securityContext.AlmacentesPermitidos,
                GeneradoEn:           securityContext.GeneradoEn)
            : null;

        return new RuntimeContextSnapshot(
            IsAuthenticated:    _session.IsAuthenticated,
            UsuarioId:          _session.Usuario?.Id,
            UsuarioNombre:      _session.Usuario?.Nombre   ?? "—",
            UsuarioEmail:       _session.Usuario?.Email,
            UsuarioUserName:    _session.Usuario?.UserName,
            EmpresaId:          _session.EmpresaId,
            SucursalId:         _session.SucursalId,
            SucursalNombre:     _session.SucursalNombre,
            HasCajaActiva:      _session.CajaActiva is not null,
            CajaNombre:         _session.CajaActiva?.CajaNombre,
            WorkspaceTabCount:  _workspace.Tabs.Count,
            ActiveTabKey:       _workspace.ActiveTab?.Key   ?? "—",
            ActiveTabTitle:     _workspace.ActiveTab?.Title ?? "—",
            WorkspaceTabs:      tabs,
            HasEmpresaFilter:   _session.EmpresaId  != 0,
            HasSucursalFilter:  _session.SucursalId != 0,
            CurrentContext:     _contextTracker.GetCurrent(),
            NavigationHistory:  _contextTracker.GetNavigationHistory(),
            ActiveModuleKey:    _contextTracker.ActiveModuleKey,
            LastOperation:      _observability.GetLatest(),
            RecentOperations:   _observability.GetHistory(),
            SecuritySnapshot:   securitySnapshot,
            Alerts:             BuildAlerts(securitySnapshot),
            Timestamp:          DateTime.Now
        );
    }

    private List<DiagnosticAlert> BuildAlerts(SecurityRuntimeSnapshot? security)
    {
        var list = new List<DiagnosticAlert>();

        if (!_session.IsAuthenticated)
            list.Add(new("✗", "Sin sesión autenticada — acceso bloqueado", AlertLevel.Error));

        if (_session.EmpresaId == 0)
            list.Add(new("⚠", "EmpresaId = 0 — filtros globales de empresa desactivados", AlertLevel.Warning));

        if (_session.SucursalId == 0)
            list.Add(new("⚠", "SucursalId = 0 — consultas sin filtro de sucursal", AlertLevel.Warning));

        if (_session.CajaActiva is null && _session.IsAuthenticated)
            list.Add(new("ℹ", "Sin caja activa — operaciones de POS limitadas", AlertLevel.Info));

        if (_workspace.Tabs.Count == 0)
            list.Add(new("ℹ", "WorkspaceService vacío — módulos visibles en ModuleFrame (navegación sidebar)", AlertLevel.Info));

        // Alertas del contexto operacional activo
        var ctx = _contextTracker.GetCurrent();
        if (ctx is not null && !ctx.HasViewModelContext)
            list.Add(new("ℹ", $"Módulo activo: {ctx.Module} — esperando carga de ViewModel", AlertLevel.Info));

        // Alertas de operaciones
        var op = _observability.GetLatest();
        if (op is not null)
        {
            if (op.EmpresaFilter.State == FilterState.Missing)
                list.Add(new("❌", $"OPERACIONAL: {op.ViewModel} — consulta sin filtro EmpresaId", AlertLevel.Error));
            if (op.SoftDeleteFilter.State == FilterState.Missing)
                list.Add(new("❌", $"OPERACIONAL: {op.ViewModel} — consulta sin filtro SoftDelete", AlertLevel.Error));
        }

        // Alertas de seguridad
        if (security is not null)
        {
            if (security.CantidadPermisos == 0)
                list.Add(new("⚠", "Usuario sin permisos efectivos — acceso a módulos será denegado", AlertLevel.Warning));

            if (security.EsSuperAdmin)
                list.Add(new("★", "Modo SuperAdmin activo — sin restricciones de scope", AlertLevel.Info));

            if (security.Roles.Count == 0 && !security.EsSuperAdmin)
                list.Add(new("⚠", "Usuario sin roles asignados", AlertLevel.Warning));
        }
        else if (_session.IsAuthenticated)
        {
            list.Add(new("ℹ", "Contexto de seguridad no cargado — usa el panel para refrescar", AlertLevel.Info));
        }

        if (_session.IsAuthenticated && _session.EmpresaId != 0 && _session.SucursalId != 0)
            list.Add(new("✓", "Contexto de sesión completo y coherente", AlertLevel.Info));

        return list;
    }
}
