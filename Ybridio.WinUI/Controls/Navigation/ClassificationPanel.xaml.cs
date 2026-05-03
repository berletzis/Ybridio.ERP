using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace Ybridio.WinUI.Controls.Navigation;

public sealed partial class ClassificationPanel : UserControl
{
    // ── DependencyProperties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<ClassificationItem>),
            typeof(ClassificationPanel),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(ClassificationItem),
            typeof(ClassificationPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(
            nameof(PanelTitle),
            typeof(string),
            typeof(ClassificationPanel),
            new PropertyMetadata(string.Empty, OnPanelTitleChanged));

    public static readonly DependencyProperty IsFilterActiveProperty =
        DependencyProperty.Register(
            nameof(IsFilterActive),
            typeof(bool),
            typeof(ClassificationPanel),
            new PropertyMetadata(false, OnIsFilterActiveChanged));

    // ── Propiedades públicas ─────────────────────────────────────────────────

    public IEnumerable<ClassificationItem>? ItemsSource
    {
        get => (IEnumerable<ClassificationItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ClassificationItem? SelectedItem
    {
        get => (ClassificationItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public bool IsFilterActive
    {
        get => (bool)GetValue(IsFilterActiveProperty);
        set => SetValue(IsFilterActiveProperty, value);
    }

    private Visibility HeaderVisible =>
        string.IsNullOrWhiteSpace(PanelTitle) ? Visibility.Collapsed : Visibility.Visible;

    // ── Evento ───────────────────────────────────────────────────────────────

    public event EventHandler<ClassificationItem?>? SelectionChanged;

    // ── Constructor ──────────────────────────────────────────────────────────

    public ClassificationPanel()
    {
        InitializeComponent();
    }

    // ── Callbacks de DependencyProperty ─────────────────────────────────────

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ClassificationPanel panel) return;

        // Asignar directamente al TreeView.ItemsSource.
        // El TreeView observa CollectionChanged del ObservableCollection internamente:
        // cuando CargarClasificacionAsync añade nodos, el TreeView se actualiza solo.
        // No se necesita suscripción manual a CollectionChanged.
        panel.ClassTree.ItemsSource = e.NewValue as IEnumerable<ClassificationItem>;
    }

    private static void OnPanelTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClassificationPanel panel)
            panel.OnPropertyChanged(nameof(HeaderVisible));
    }

    private static void OnIsFilterActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Actualiza el ToggleButton en el header vía Bindings.Update()
        if (d is ClassificationPanel panel)
            panel.Bindings.Update();
    }

    // ── Manejo de selección ──────────────────────────────────────────────────

    private void ClassTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        // Con TreeView.ItemsSource, AddedItems contiene los objetos fuente directamente
        // (ClassificationItem), no TreeViewNode — por eso el cast funciona.
        ClassificationItem? selected = null;

        if (args.AddedItems.Count > 0)
            selected = args.AddedItems[0] as ClassificationItem;
        else
            selected = ClassTree.SelectedItem as ClassificationItem;

        if (!ReferenceEquals(SelectedItem, selected))
            SetValue(SelectedItemProperty, selected);

        SelectionChanged?.Invoke(this, selected);
    }

    /// <summary>
    /// Deselecciona el nodo actualmente seleccionado en el TreeView.
    /// Llamado desde la Page cuando el filtro se limpia desde el chip o el ViewModel.
    /// </summary>
    public void ClearSelection()
    {
        ClassTree.SelectedItem = null;
    }

    private void OnPropertyChanged(string name) => Bindings.Update();
}
