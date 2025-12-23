# Repository Guidelines

## Project Structure & Module Organization
- `VrcxPredictor.App/`: WPF UI app (entry point, XAML views, view models, services).
- `VrcxPredictor.Core/`: Core analysis logic, models, and time utilities.
- `VrcxPredictor.Data/`: Data access (SQLite repository).
- `VrcxPredictor.sln`: Visual Studio solution file.

## Build, Test, and Development Commands
- `dotnet restore VrcxPredictor.sln`: Restore NuGet packages.
- `dotnet build VrcxPredictor.sln -c Debug`: Build all projects.
- `dotnet run --project VrcxPredictor.App`: Run the desktop app.
- Visual Studio: open `VrcxPredictor.sln`, set `VrcxPredictor.App` as startup, run.

## Coding Style & Naming Conventions
- Language: C# (net8.0-windows, WPF). Use 4-space indentation.
- XAML: keep layout readable; use descriptive `x:Name` values (e.g., `RootNavigation`).
- C#: PascalCase for types/methods, camelCase for locals/parameters, private fields use `_camelCase`.
- Prefer MVVM patterns as shown in `VrcxPredictor.App/ViewModels`.

## Testing Guidelines
- No test project is present in this repository.
- If adding tests, use a dedicated test project (e.g., `VrcxPredictor.Tests`) and document the runner in this file.

## Commit & Pull Request Guidelines
- Git history is not available here; use clear, consistent commit messages.
- Suggested pattern: `type: short summary` (e.g., `feat: add heatmap export`).
- PRs should include: a short description, linked issues (if any), and screenshots for UI changes.

## Configuration & Data Files
- Default VRCX DB location: `%APPDATA%\VRCX\VRCX.sqlite3`.
- App config persists to `%APPDATA%\vrcx_predictor\config.json`.
