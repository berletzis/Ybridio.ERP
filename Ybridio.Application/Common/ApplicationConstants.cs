namespace Ybridio.Application.Common;

/// <summary>
/// Constantes de la capa Application.
/// Los IDs de TipoMovimiento deben coincidir con los seeds de la base de datos.
/// </summary>
public static class ApplicationConstants
{
    /// <summary>IDs de tipos de movimiento de inventario (tabla inventario.TipoMovimientoInventario).</summary>
    public static class TipoMovimientoInventario
    {
        /// <summary>Salida por venta en POS.</summary>
        public const int SalidaVenta = 2;

        /// <summary>Entrada por compra / recepción.</summary>
        public const int EntradaCompra = 1;

        /// <summary>Ajuste de inventario manual.</summary>
        public const int Ajuste = 3;
    }

    /// <summary>IDs de tipos de movimiento de caja (tabla finanzas.TipoMovimientoCaja).</summary>
    public static class TipoMovimientoCaja
    {
        /// <summary>Ingreso por cobro de venta.</summary>
        public const int Venta = 1;

        /// <summary>Egreso por devolución.</summary>
        public const int Devolucion = 2;

        /// <summary>Egreso por gastos de caja.</summary>
        public const int Gasto = 3;
    }

    /// <summary>Claves de permisos del sistema (tabla seguridad.Permiso.Clave).</summary>
    public static class Claves
    {
        public const string VentasCrear = "ventas.crear";
        public const string VentasVer = "ventas.ver";
        public const string CajaAbrir = "caja.abrir";
        public const string CajaCerrar = "caja.cerrar";
        public const string InventarioVer = "inventario.ver";
        public const string InventarioAjustar = "inventario.ajustar";
    }
}
