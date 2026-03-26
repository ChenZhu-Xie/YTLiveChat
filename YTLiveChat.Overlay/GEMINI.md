# YTLiveChat.Overlay

A web-based, high-performance YouTube Live Chat overlay designed for OBS and other broadcasting software. It uses the `YTLiveChat` library to monitor YouTube live streams via InnerTube and pushes messages to a browser-based frontend via WebSockets.

## Project Overview

*   **Purpose:** Provides a visually appealing, low-latency chat overlay for YouTube creators.
*   **Architecture:**
    *   **Backend:** ASP.NET Core (`net10.0`) server that manages the YouTube chat connection and broadcasts messages via WebSockets.
    *   **Frontend:** A transparent HTML/CSS/JS interface (`wwwroot/index.html`) that renders chat messages with dynamic animations.
*   **Key Features:**
    *   **Monitor Mode:** Automatically detects when a channel (e.g., `@xczphysics`) goes live and starts/restarts the chat stream.
    *   **WebSocket Streaming:** Real-time message delivery from backend to frontend.
    *   **Message History:** Buffers the last 30 messages so new overlay instances can populate immediately.
    *   **Visual Enhancements:** 10+ unique entry animations, special styling for Super Chats (Gold/Orange gradient) and Memberships (Green gradient), and support for custom fonts and avatars.
    *   **OBS Optimization:** Configured for transparent backgrounds, GPU-accelerated rendering, and font smoothing.

## Building and Running

*   **Build:** `dotnet build`
*   **Run:** `dotnet run` (Runs the web server and starts the YouTube monitor).
*   **Test UI:** Access `http://localhost:5000/test` to trigger a fake Super Chat message for testing OBS layout/styling.
*   **Overlay URL:** `http://localhost:5000/index.html` (Add this as a "Browser Source" in OBS).

## Development Conventions

*   **Backend (Program.cs):**
    *   Uses `YTLiveChat.DependencyInjection` for service setup.
    *   Maintains a `ConcurrentDictionary<Guid, WebSocket>` for active connections.
    *   Includes a Windows-specific cleanup routine to prevent file locking during rapid development cycles.
*   **Frontend (wwwroot/):**
    *   `index.html` contains all styling and logic.
    *   Uses `backdrop-filter: blur(8px)` for a modern "glassmorphism" look.
    *   Animations are assigned based on a hash of the author's name to ensure consistent but varied entry effects.
    *   Colors for regular users are procedurally generated from their names.

## Key Files

*   `Program.cs`: The "brain" of the overlay. Handles the YT connection, WS lifecycle, and monitor logic.
*   `YTLiveChat.Overlay.csproj`: Project configuration and dependencies.
*   `wwwroot/index.html`: The visual overlay template.
*   `wwwroot/Inconsolata-LXGWMono-Medium.ttf`: The primary font for the overlay.
