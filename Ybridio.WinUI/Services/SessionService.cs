using System;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.WinUI.Services;

/// <summary>
/// Mantiene el estado de la sesión activa del usuario en toda la aplicación.
/// Es un singleton que se resetea al hacer logout.
/// Implementa <see cref="ISessionContext"/> para que ErpDbContext pueda aplicar filtros globales
/// y los servicios de Application puedan acceder al usuario y empresa activos.
/// </summary>
public sealed class SessionService : ISessionContext
{
    /// <summary>
    /// Se dispara cuando el usuario cambia la sucursal activa.
    /// Los ViewModels se suscriben para recargar sus datos automáticamente.
    /// </summary>
    public event Action<int>? SucursalChanged;

    /// <summary>Usuario autenticado. Null si no hay sesión activa.</summary>
    public UsuarioDto? Usuario { get; private set; }

    /// <summary>ID de la empresa activa (tomado del usuario o selección posterior).</summary>
    public int EmpresaId { get; private set; }

    /// <summary>ID de la sucursal activa seleccionada por el usuario.</summary>
    public int SucursalId { get; private set; }

    /// <summary>Nombre de la sucursal activa (para mostrar en TopBar).</summary>
    public string SucursalNombre { get; private set; } = string.Empty;

    /// <summary>Apertura de caja activa. Null si no hay caja abierta.</summary>
    public AperturaCajaDto? CajaActiva { get; private set; }

    /// <summary>Indica si hay una sesión de usuario activa.</summary>
    public bool IsAuthenticated => Usuario is not null;

    /// <summary>
    /// ID del usuario autenticado. Implementa <see cref="ISessionContext.UsuarioId"/>
    /// para que los servicios de Application puedan evaluar permisos del usuario activo.
    /// </summary>
    public Guid? UsuarioId => Usuario?.Id;

    /// <summary>
    /// Activa/desactiva el modo desarrollador para el Runtime Diagnostic Panel.
    /// Solo accesible mediante Ctrl+Shift+D en ShellPage — invisible para usuarios finales.
    /// </summary>
    public bool IsDeveloperMode { get; private set; }

    /// <summary>Alterna el modo desarrollador.</summary>
    public void ToggleDeveloperMode() => IsDeveloperMode = !IsDeveloperMode;

    /// <summary>Establece la sesión tras un login exitoso.</summary>
    public void SetUsuario(UsuarioDto usuario)
    {
        Usuario   = usuario;
        EmpresaId = usuario.EmpresaId;
    }

    /// <summary>Establece la sucursal activa (puede cambiarse en configuración).</summary>
    public void SetTienda(int tiendaId, string nombre)
    {
        SucursalId     = tiendaId;
        SucursalNombre = nombre;
        SucursalChanged?.Invoke(tiendaId);
    }

    /// <summary>Registra la apertura de caja activa.</summary>
    public void SetCajaActiva(AperturaCajaDto? apertura) => CajaActiva = apertura;

    /// <summary>Limpia toda la sesión (logout).</summary>
    public void Clear()
    {
        Usuario        = null;
        EmpresaId      = 0;
        SucursalId     = 0;
        SucursalNombre = string.Empty;
        CajaActiva     = null;
    }
}
