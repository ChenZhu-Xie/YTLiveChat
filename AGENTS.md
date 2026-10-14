# Repository Guidelines

## Project Structure & Module Organization
`YTLiveChat.sln` is the entry point for the full solution. Core parser and contracts live in `YTLiveChat/` (`Helpers/`, `Services/`, `Contracts/`, `Models/Response/`). Supporting packages and apps are split by responsibility: `YTLiveChat.DependencyInjection/` for DI registration, `YTLiveChat.Example/` for console usage, `YTLiveChat.Overlay/` and `YTLiveChat.TerminalStatus/` for UI hosts, and `YTLiveChat.Tools/` for utility tooling. Tests and fixture data live in `YTLiveChat.Tests/`, with raw payload samples under `YTLiveChat.Tests/TestData/`.

## Build, Test, and Development Commands
Use the .NET CLI from the repository root:

- `dotnet restore YTLiveChat.sln` restores all projects.
- `dotnet build YTLiveChat.sln -c Debug` builds the full solution.
- `dotnet test YTLiveChat.Tests/YTLiveChat.Tests.csproj -c Debug` runs the MSTest suite.
- `dotnet run --project YTLiveChat.Example` launches the sample console app.
- `.\YTLiveChat.Overlay.ps1` is the default command for routine Overlay development and use.
- `.\YTLiveChat.TerminalStatus\YTLiveChat.TerminalStatus.ps1` is the default command for routine TerminalStatus development and use.

Both launcher scripts stop the existing app instance, rebuild the Debug configuration, and restart it in a hidden window. Use `-Console` for a foreground session, `-Release` for a Release build, or `-StopOnly` to stop the app without restarting it. Prefer these scripts over direct `dotnet run` commands when rebuilding or restarting either UI host.

## Coding Style & Naming Conventions
This repo uses C# with nullable reference types and implicit usings enabled. Follow `.editorconfig`: 4-space indentation, CRLF line endings, file-scoped namespaces, and `System` usings first. Prefer explicit types over `var` unless the type is obvious, and use PascalCase for types/methods/properties; interfaces must start with `I`. Keep new parser logic close to the existing structure in `Helpers/Parser.cs` and response models in `Models/Response/LiveChatResponse.cs`.

## Testing Guidelines
Tests use MSTest (`MSTest.Sdk`) with Moq for doubles. Name test files by target area, e.g. `ParserTests.cs`, `ConverterTests.cs`, `YTLiveChatServiceTests.cs`. When adding support for new YouTube payloads, include both parser assertions and representative fixtures in `YTLiveChat.Tests/TestData/`. Run `dotnet test` before opening a PR.

## Commit & Pull Request Guidelines
Recent history follows short Conventional Commit style such as `fix(overlay): ...` and `feat(overlay): ...`; keep using `type(scope): summary`. PRs should explain the behavioral change, call out affected projects, and include screenshots for `Overlay` or `TerminalStatus` UI work. For parser changes, link the raw payload sample or fixture that justifies the update.
