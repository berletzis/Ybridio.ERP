using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.WinUI.Services;

/// <summary>
/// Mantiene el estado de la sesión activa del usuario en toda la aplicación.
/// Es un singleton que se resetea al hacer logout.
/// </summary>
public sealed class SessionService
{
    /// <summary>Usuario autenticado. Null si no hay sesión activa.</summary>
    public UsuarioDto? Usuario { get; private set; }

    /// <summary>ID de la empresa activa (tomado del usuario o selección posterior).</summary>
    public int EmpresaId { get; private set; }

    /// <summary>ID de la tienda activa seleccionada por el usuario.</summary>
    public int TiendaId { get; private set; }

    /// <summary>Nombre de la tienda activa (para mostrar en TopBar).</summary>
    public string TiendaNombre { get; private set; } = string.Empty;

    /// <summary>Apertura de caja activa. Null si no hay caja abierta.</summary>
    public AperturaCajaDto? CajaActiva { get; private set; }

    /// <summary>Indica si hay una sesión de usuario activa.</summary>
    public bool IsAuthenticated => Usuario is not null;

    /// <summary>
    /// Establece la sesión tras un login exitoso.
    /// </summary>
    public void SetUsuario(UsuarioDto usuario)
    {
        Usuario = usuario;
        EmpresaId = usuario.EmpresaId;
    }

    /// <summary>
    /// Establece la tienda activa (puede cambiarse en configuración).
    /// </summary>
    public void SetTienda(int tiendaId, string nombre)
    {
        TiendaId = tiendaId;
        TiendaNombre = nombre;
    }

    /// <summary>
    /// Registra la apertura de caja activa.
    /// </summary>
    public void SetCajaActiva(AperturaCajaDto? apertura) => CajaActiva = apertura;

    /// <summary>
    /// Limpia toda la sesión (logout).
    /// </summary>
    public void Clear()
    {
        Usuario = null;
        EmpresaId = 0;
        TiendaId = 0;
        TiendaNombre = string.Empty;
        CajaActiva = null;
    }
}
