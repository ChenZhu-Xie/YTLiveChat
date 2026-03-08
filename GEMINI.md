# YTLiveChat Project Context

Unofficial .NET library for reading YouTube live chat via InnerTube (YouTube's internal web API), bypassing Data API quotas and OAuth requirements.

## Project Overview

*   **Purpose:** Provides a lightweight, high-performance way to monitor YouTube live chat messages, Super Chats, Stickers, and Membership events.
*   **Key Features:**
    *   Supports text messages, Super Chats, Super Stickers, and Membership (new/milestone/gift).
    *   Ticker support for paid messages and memberships.
    *   Viewer leaderboard rank extraction (e.g., #1 crown tags).
    *   Async streaming APIs (`StreamChatItemsAsync`, `StreamRawActionsAsync`) and event-based APIs.
    *   Debug mode for capturing raw InnerTube JSON for schema analysis.
*   **Architecture:**
    *   `YTLiveChat`: Core library containing polling logic, InnerTube parsers, and public contracts.
    *   `YTLiveChat.DependencyInjection`: Integration helpers for `Microsoft.Extensions.DependencyInjection`.
    *   `YTLiveChat.Example`: A feature-rich console application demonstrating library usage.
    *   `YTLiveChat.Tests`: Comprehensive test suite with MSTest and Moq, using raw JSON fixtures.
    *   `YTLiveChat.Tools`: Diagnostic tool for analyzing captured JSON logs to detect schema changes.

## Tech Stack

*   **Language:** C# (Latest / C# 13+)
*   **Runtime/SDK:** .NET 10.0 (Global SDK `10.0.103`)
*   **Target Frameworks:** `net10.0`, `netstandard2.1`, `netstandard2.0`
*   **Key Libraries:**
    *   `System.Text.Json` (High-performance JSON)
    *   `System.Threading.Channels` (Internal event streaming)
    *   `Microsoft.Extensions.Logging.Abstractions`
    *   `Moq` (Testing mocks)
    *   `MinVer` (Versioning via Git tags)

## Building and Running

*   **Build Solution:** `dotnet build`
*   **Run Example:** `dotnet run --project YTLiveChat.Example`
*   **Run Tests:** `dotnet test` (Uses `Microsoft.Testing.Platform`)
*   **Run Diagnostic Tool:** `dotnet run --project YTLiveChat.Tools -- <logPath>`
*   **Check Code Style:** `dotnet build` (Enforced via `EnforceCodeStyleInBuild`)

## Development Conventions

*   **Coding Style:**
    *   Strict adherence to C# conventions (PascalCase for public members, camelCase for private fields).
    *   Nullable reference types enabled across all projects.
    *   `RequiredMemberAttributes` polyfills used for compatibility with older targets.
*   **Parser Updates:**
    *   InnerTube schema changes should be reflected in `YTLiveChat/Models/Response/LiveChatResponse.cs`.
    *   Logic for projecting raw JSON into contracts resides in `YTLiveChat/Helpers/Parser.cs`.
*   **Testing:**
    *   New features or parser updates **must** include corresponding tests in `YTLiveChat.Tests`.
    *   Utilize `YTLiveChat.Tests/TestData` for adding new raw JSON samples captured from live streams.
    *   Internal members are visible to the test project via `InternalsVisibleTo`.
*   **Beta/Obsolete APIs:**
    *   The `EnableContinuousLivestreamMonitor` feature is considered BETA and marked with `[Obsolete]` to warn consumers of potential breaking changes.

## Key Files

*   `YTLiveChat.sln`: Main solution file.
*   `README.md`: Comprehensive usage guide and quick start.
*   `YTLiveChat/Services/YTLiveChat.cs`: Core polling and lifecycle logic.
*   `YTLiveChat/Helpers/Parser.cs`: The "heart" of the library that maps JSON to models.
*   `YTLiveChat/Contracts/Models/ChatItem.cs`: The primary data contract for consumers.
*   `Directory.Build.props`: Global build settings (Analysis level, style enforcement).
