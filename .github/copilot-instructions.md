# Copilot Instructions

## Project Guidelines
- Ybridio.ERP coding conventions (MANDATORY):
- Naming: PascalCase classes/methods/properties, _camelCase private fields, I prefix for interfaces, Async suffix for async methods
- XML documentation comments (///) required on ALL public members: summary, param, returns
- Architecture: Domain→no deps, Application→Domain, Infrastructure→App+Domain, WinUI→Application. No DbContext in WinUI, no business logic in WinUI, no direct entity exposure to UI
- Patterns: thin ViewModels (orchestration only), DTOs between layers, services in Application layer
- Folder structure: Domain(Entities/ValueObjects/Enums), Application(Services/Interfaces/DTOs), Infrastructure(Persistence/Identity/Services), WinUI(Views/ViewModels/Services)
- Minimum change principle: only touch files strictly needed for the task