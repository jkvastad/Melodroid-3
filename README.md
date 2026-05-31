# Melodroid 3

A program for music research from first principles — exploring music as the meeting point
between the physics of sound and the biology of human hearing.

## 📖 Documentation

**The full theory and CLI reference live in the docs site:**
<https://jkvastad.github.io/Melodroid-3/>

The site source is under [`website/`](website/). To run it locally:

```bash
cd website
npm install
npm start         # http://localhost:3000/Melodroid-3/ with hot reload
```

Similarly for production build test

```bash
cd website
npm run build
npm run serve         # http://localhost:3000/Melodroid-3/ with hot reload
```

## The C# tool

The `dotnet` CLI is the source of truth for the data the docs describe.

| Action | Command |
| --- | --- |
| Build | `dotnet build` |
| Run | `dotnet run -- <subcommand>` |
| Test | `dotnet test` |

Subcommands are grouped by output type (`table`, `graph`, `plot`); each writes its artifact
under `output/` and prints the path. See the
[CLI reference](https://jkvastad.github.io/Melodroid-3/docs/cli/reference) for the full command
surface, or run `dotnet run -- --help`.

- **Tech stack:** .NET 9.0 / C# 12; `Spectre.Console`, `ScottPlot`, `System.CommandLine`,
  xUnit + FluentAssertions.
- **Project layout:** domain code in `src/` (`Physics`, `Hearing`, `Music`, `Output`), tests in
  `Melodroid.Tests/`, generated artifacts in the gitignored `output/`.