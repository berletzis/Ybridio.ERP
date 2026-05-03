using System.Collections.ObjectModel;

namespace Ybridio.WinUI.Controls.Navigation;

/// <summary>
/// Nodo de clasificación reutilizable. Sin dependencias de dominio.
/// </summary>
public class ClassificationItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>Texto del conteo formateado para mostrar en el panel: "(N)".</summary>
    public string CountDisplay => $"({Count})";
    /// <summary>
    /// Id real de la CategoriaProducto en BD (int). Null en el nodo raíz TODOS.
    /// Usado directamente en FiltrarPorClasificacion — sin parsing de strings.
    /// </summary>
    public int? CategoriaId { get; set; }
    /// <summary>True solo en el nodo "TODOS".</summary>
    public bool IsRoot { get; set; }
    public ObservableCollection<ClassificationItem> Children { get; set; } = [];
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public object? Tag { get; set; }
}
