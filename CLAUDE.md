# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build (x64 debug)
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64

# Build release
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64 -c Release

# Run (requires MSIX packaging or unpackaged deploy in VS)
# Use Visual Studio 2022 with Windows App SDK workload — F5 deploys via MSIX tooling.
# dotnet run does NOT work for WinUI 3 projects; use VS or msix deploy.

# Restore
dotnet restore Ybridio.ERP.slnx
```

No unit test projects exist yet.

## Architecture

Clean Architecture in 4 layers (bottom → top):

```
Ybridio.Domain          → Entities, AuditableEntity base, no dependencies
Ybridio.Infrastructure  → EF Core 8 + SQL Server + ASP.NET Identity
Ybridio.Application     → Service interfaces + implementations + DTOs
Ybridio.WinUI           → WinUI 3 presentation (Windows App SDK 2.0.1)
```

### Domain (`Ybridio.Domain`)
- All entities inherit `AuditableEntity` (FechaCreacion, FechaModificacion, Borrado soft-delete, RowVersion concurrency token)
- Namespaced by business domain: `Catalogos/`, `Inventario/`, `Ventas/`, `Finanzas/`, `Compras/`, `Seguridad/`
- Multi-tenant: every major entity has `EmpresaId` (scopes all queries)

### Infrastructure (`Ybridio.Infrastructure`)
- `ErpDbContext` is the single DbContext (Scoped lifetime)
- Identity via `ApplicationUser` / `ApplicationRole` on the same context
- EF configs in `Persistence/Configurations/`
- Connection string is hardcoded in `App.xaml.cs` (dev environment only)

### Application (`Ybridio.Application`)
- Services registered via `AddApplicationServices()` extension — all **Scoped**
- `ServiceResult<T>` / `ServiceResult` return type for all write operations (has `.Success`, `.Value`, `.Error`, `.ErrorCode`)
- DTOs are `sealed record` types in `DTOs/` subfolders matching domain namespaces
- `ErrorCode` enum for typed error mapping in the UI

### WinUI (`Ybridio.WinUI`)
- **MVVM**: CommunityToolkit.Mvvm 8.4.0 — `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`
- **DI**: `Microsoft.Extensions.DependencyInjection` via `App.Services` static property
- **Navigation**: `INavigationService` (singleton) wraps a WinUI `Frame`; prevents duplicate navigation to the same page type
- **Session**: `SessionService` (singleton) holds logged-in user, tienda, caja activa

## Key Patterns

### Page + ViewModel wiring
**Always set `ViewModel` BEFORE `InitializeComponent()`** — x:Bind compiled bindings evaluate during `InitializeComponent`. Setting ViewModel after causes all Mode=OneWay bindings to evaluate with null and not update until the next PropertyChanged fires.

```csharp
public sealed partial class MyPage : Page
{
    public MyViewModel ViewModel { get; }
    public MyPage()
    {
        ViewModel = App.Services.GetRequiredService<MyViewModel>();
        InitializeComponent();   // ← AFTER ViewModel is assigned
    }
}
```

### ViewModels
- `sealed partial class` inheriting `ObservableObject`
- Async commands: `[RelayCommand] private async Task FooAsync()` → generates `FooCommand` (Async suffix stripped)
- CanExecute: `[RelayCommand(CanExecute = nameof(GuardMethod))]`; call `FooCommand.NotifyCanExecuteChanged()` when guard state changes
- Fire-and-forget partial hooks: `partial void OnPropertyChanged(T value) => _ = SomeAsync();`

### Resource keys (WinUI 3 — NOT UWP)
Use WinUI 3 theme brush names. UWP `SystemControl*` keys throw `XamlParseException` at runtime:

| ❌ UWP (breaks) | ✅ WinUI 3 |
|---|---|
| `SystemControlBackgroundChromeLowBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlBackgroundChromeMediumBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlHighlightAccentBrush` | `AccentFillColorDefaultBrush` |
| `SystemControlForegroundBaseMediumBrush` | `TextFillColorSecondaryBrush` |
| `SystemControlForegroundBaseHighBrush` | `TextFillColorPrimaryBrush` |

### Thickness in WinUI 3
`Thickness` has **only a 4-argument constructor** (`left, top, right, bottom`). WPF's 1- and 2-argument constructors do not exist:
```csharp
new Thickness(8, 4, 8, 4)   // ✓
new Thickness(8, 4)          // ✗ compile error
new Thickness(8)             // ✗ compile error
```

### Programmatic Windows (no XAML)
Secondary windows (detail forms, comparison views) are built entirely in C# — no `.xaml` file. Pattern:
```csharp
public sealed class MyWindow : Window
{
    public MyWindow()
    {
        AppWindow.Resize(new SizeInt32(w, h));
        // Center over main window:
        var main = App.Services.GetRequiredService<MainWindow>();
        main.Closed += (_, _) => this.Close();   // cascade close
        var p = main.AppWindow.Position; var ms = main.AppWindow.Size; var ts = AppWindow.Size;
        AppWindow.Move(new PointInt32(p.X + (ms.Width - ts.Width) / 2, p.Y + (ms.Height - ts.Height) / 2));
        Content = BuildUI();
    }
}
```
- `Grid.SetColumn/Row` require `FrameworkElement`, not `UIElement` — declare helper parameters as `FrameworkElement`
- `Application.Current` is ambiguous with `Ybridio.Application`; use alias: `using XamlApp = Microsoft.UI.Xaml.Application;`

### Adding a new module (full checklist)
1. Domain entity inheriting `AuditableEntity`
2. EF Core configuration in `Infrastructure/Persistence/Configurations/`
3. DTOs (`sealed record`) in `Application/DTOs/`
4. Service interface + implementation in `Application/Services/`; register in `ServiceCollectionExtensions.AddApplicationServices()`
5. ViewModel in `WinUI/ViewModels/<Module>/`; register `services.AddTransient<XxxViewModel>()` in `App.xaml.cs`
6. XAML page in `WinUI/Views/<Module>/`; register `services.AddTransient<XxxPage>()` in `App.xaml.cs`
7. Add `<None Remove>` + `<Page Update Generator="MSBuild:Compile">` entries in `Ybridio.WinUI.csproj`
8. **Every module needs a placeholder page** — if `SelectModule(modulo)` does not call `_navigation.NavigateTo(typeof(XxxPage))`, the Frame retains the last loaded page
9. Add navigation case in `ShellViewModel.SelectModule`

### Shell navigation
`ShellViewModel.SelectModule` must:
1. Collapse **all** `ShowRibbonXxx` properties first
2. Set the target module's ribbon to `Visible`
3. Call `_navigation.NavigateTo(typeof(XxxPage))` — every module case must navigate, even placeholders

`MainWindow` is registered as **singleton** (`services.AddSingleton<MainWindow>()`); `OnLaunched` retrieves it via `Services.GetRequiredService<MainWindow>()`.
