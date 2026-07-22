# Contributing to Square

Square is an experimental, compile-first desktop UI framework. Contributions should preserve its NativeAOT-oriented, reflection-free core and explicit platform/backend boundaries.

## Before Opening Work

- Search existing issues and the roadmap.
- Open an issue before large public API, architecture, dependency, rendering, or platform changes.
- Keep pull requests focused. Do not combine unrelated formatting or generated-file changes.

## Development Setup

Requirements:

- .NET SDK selected by `global.json`
- Windows 10+ for the Win32 sample, or Linux with X11 and `libX11`

Run:

```bash
dotnet restore Square.slnx
dotnet build Square.slnx
dotnet test Square.slnx
```

## Engineering Rules

- Add tests for behavior changes and regressions.
- Avoid runtime reflection and dynamic code generation in runtime packages.
- Keep platform APIs isolated in platform projects.
- Treat `.sqx` and `.sqv` generator diagnostics as user-facing API.
- Document public API changes in `docs/API-Reference.md` and capability changes in `docs/Roadmap.md`.
- Do not commit `bin`, `obj`, screenshots, logs, local scripts, packages, tokens, or machine-specific paths.
- Regenerate checked-in generated files only through their documented tool.

## Pull Requests

Describe the problem, implementation, compatibility impact, and validation. CI must pass on Windows and Linux. Changes to package consumption or NativeAOT behavior should include a clean package or publish smoke test.

By contributing, you agree that your contribution is licensed under the repository's MIT License.
