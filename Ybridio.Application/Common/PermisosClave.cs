namespace Ybridio.Application.Common;

/// <summary>
/// Catálogo de claves de permisos del sistema.
/// Formato: {entidad}.{accion} en minúsculas.
/// Deben coincidir con los valores de seguridad.Permiso.Clave en la base de datos.
/// </summary>
/// <remarks>
/// NO usar if(rol == "X") — siempre evaluar permisos via IErpAuthorizationService.PuedeAsync(clave).
/// Estas constantes son la referencia tipada de las claves; la lógica es DATA, no código.
/// </remarks>
public static class PermisosClave
{
    public static class Venta
    {
        public const string Ver      = "venta.ver";
        public const string Crear    = "venta.crear";
        public const string Cancelar = "venta.cancelar";
        public const string Aprobar  = "venta.aprobar";
    }

    public static class Entrada
    {
        public const string Ver      = "entrada.ver";
        public const string Crear    = "entrada.crear";
        public const string Cancelar = "entrada.cancelar";
        public const string Aprobar  = "entrada.aprobar";
    }

    public static class Salida
    {
        public const string Ver       = "salida.ver";
        public const string Crear     = "salida.crear";
        public const string Cancelar  = "salida.cancelar";
        public const string Autorizar = "salida.autorizar";
    }

    public static class Traspaso
    {
        public const string Ver      = "traspaso.ver";
        public const string Crear    = "traspaso.crear";
        public const string Cancelar = "traspaso.cancelar";
    }

    public static class Ajuste
    {
        public const string Ver     = "ajuste.ver";
        public const string Crear   = "ajuste.crear";
        public const string Aprobar = "ajuste.aprobar";
    }

    public static class Existencia
    {
        public const string Ver = "existencia.ver";
    }

    public static class Producto
    {
        public const string Ver      = "producto.ver";
        public const string Crear    = "producto.crear";
        public const string Editar   = "producto.editar";
        public const string Eliminar = "producto.eliminar";
    }

    public static class Caja
    {
        public const string Ver               = "caja.ver";
        public const string Abrir             = "caja.abrir";
        public const string Cerrar            = "caja.cerrar";
        public const string MovimientoEgreso  = "caja.movimiento.egreso";
        public const string MovimientoIngreso = "caja.movimiento.ingreso";
    }

    public static class Compra
    {
        public const string Ver      = "compra.ver";
        public const string Crear    = "compra.crear";
        public const string Aprobar  = "compra.aprobar";
        public const string Cancelar = "compra.cancelar";
        public const string Recibir  = "compra.recibir";
    }

    public static class Cliente
    {
        public const string Ver    = "cliente.ver";
        public const string Crear  = "cliente.crear";
        public const string Editar = "cliente.editar";
    }

    public static class Proveedor
    {
        public const string Ver    = "proveedor.ver";
        public const string Crear  = "proveedor.crear";
        public const string Editar = "proveedor.editar";
    }

    public static class Configuracion
    {
        public const string GlobalVer      = "configuracion.global.ver";
        public const string GlobalEditar   = "configuracion.global.editar";
        public const string SucursalVer    = "configuracion.sucursal.ver";
        public const string SucursalEditar = "configuracion.sucursal.editar";
    }

    public static class Seguridad
    {
        public const string UsuariosVer       = "seguridad.usuarios.ver";
        public const string UsuariosGestionar = "seguridad.usuarios.gestionar";
        public const string RolesVer          = "seguridad.roles.ver";
        public const string RolesGestionar    = "seguridad.roles.gestionar";
        public const string PermisosVer       = "seguridad.permisos.ver";
        public const string PermisosGestionar = "seguridad.permisos.gestionar";
    }

    public static class Reporte
    {
        public const string Ver      = "reporte.ver";
        public const string Exportar = "reporte.exportar";
    }

    /// <summary>
    /// Retorna todos los claves definidos en esta clase. Útil para seed y validaciones.
    /// </summary>
    public static IReadOnlyList<string> Todos() =>
    [
        Venta.Ver, Venta.Crear, Venta.Cancelar, Venta.Aprobar,
        Entrada.Ver, Entrada.Crear, Entrada.Cancelar, Entrada.Aprobar,
        Salida.Ver, Salida.Crear, Salida.Cancelar, Salida.Autorizar,
        Traspaso.Ver, Traspaso.Crear, Traspaso.Cancelar,
        Ajuste.Ver, Ajuste.Crear, Ajuste.Aprobar,
        Existencia.Ver,
        Producto.Ver, Producto.Crear, Producto.Editar, Producto.Eliminar,
        Caja.Ver, Caja.Abrir, Caja.Cerrar, Caja.MovimientoEgreso, Caja.MovimientoIngreso,
        Compra.Ver, Compra.Crear, Compra.Aprobar, Compra.Cancelar, Compra.Recibir,
        Cliente.Ver, Cliente.Crear, Cliente.Editar,
        Proveedor.Ver, Proveedor.Crear, Proveedor.Editar,
        Configuracion.GlobalVer, Configuracion.GlobalEditar,
        Configuracion.SucursalVer, Configuracion.SucursalEditar,
        Seguridad.UsuariosVer, Seguridad.UsuariosGestionar,
        Seguridad.RolesVer, Seguridad.RolesGestionar,
        Seguridad.PermisosVer, Seguridad.PermisosGestionar,
        Reporte.Ver, Reporte.Exportar,
    ];
}
