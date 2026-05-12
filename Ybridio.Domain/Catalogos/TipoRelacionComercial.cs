namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Tipo de vínculo comercial que representa una RelacionComercial con la empresa.
/// Un mismo socio puede evolucionar de Prospecto a Cliente, o ser Proveedor simultáneamente (Mixto).
/// </summary>
public enum TipoRelacionComercial
{
    /// <summary>Prospecto: interés comercial sin historial de transacciones.</summary>
    Prospecto = 1,

    /// <summary>Cliente: compra productos o servicios a la empresa.</summary>
    Cliente = 2,

    /// <summary>Proveedor: vende productos o servicios a la empresa.</summary>
    Proveedor = 3,

    /// <summary>Mixto: actúa como cliente y proveedor simultáneamente.</summary>
    Mixto = 4,
}
