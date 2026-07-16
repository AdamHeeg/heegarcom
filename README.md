# heegarcom

Personal portfolio site for Adam Heeg — resume, case studies, and small interactive demos.

## Tech

- **.NET 10** Blazor Web App
- **Static SSR** by default with **interactive WebAssembly islands** (e.g. the Projects tag filter)
- **Tailwind CSS v4** via the standalone CLI (no Node) — compiled on build by an MSBuild target
- Deploy target: **SmarterASP.NET** (Windows / IIS)

## Projects

- `heegarcom/` — server app (static SSR pages, layout, styles)
- `heegarcom.Client/` — WebAssembly project (interactive island components)

## Running locally

```bash
cd heegarcom
dotnet run
```

Notes:
- The Tailwind CLI (`tools/tailwindcss.exe`, pinned to v4.3.3) is **not** committed because it
  exceeds GitHub's 100 MB file limit. The build downloads it automatically if missing
  (see the `EnsureTailwindCli` target in `heegarcom/heegarcom.csproj`). Windows-only for now.
- `NuGet.config` pins the package source to nuget.org so restore is independent of any
  machine-level private feeds.

## Palette

Brand colors live as CSS variables in `heegarcom/Styles/app.css` (navy `#0A3161`,
orange `#D24B14`). Change them there to re-theme the whole site.
