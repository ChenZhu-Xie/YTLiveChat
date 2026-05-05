# Feature Branch vs Upstream

This document summarizes the current difference between:

- current feature branch: `feat/add-overlay-project`
- upstream baseline: `upstream/master`

For the current repository state, the practical comparison is:

- feature branch head: `4117a51`
- upstream master: `c6b7c1d`

## Scope

This is not a raw `git diff` dump. It is a grouped summary of what each side introduced and how the branch differs at a product and codebase level.

---

## 1. Upstream Mainline Additions

These changes came from upstream and were brought into the local branch during rebase.

### 1.1 Stream discovery and stream metadata

Upstream added stream-page parsing and stream metadata contracts, including:

- `StreamInfo`
- `GetStreamsAsync`
- parsing for channel streams page candidates
- support for distinguishing live, upcoming, and past streams

Relevant areas:

- `YTLiveChat/Contracts/Models/StreamInfo.cs`
- `YTLiveChat/Contracts/Services/IYTLiveChat.cs`
- `YTLiveChat/Helpers/Parser.cs`
- `YTLiveChat/Services/YTLiveChat.cs`

### 1.2 Gift and Jewels support

Upstream added support for YouTube virtual gifting, including:

- `GiftItem`
- `GiftReceived` event
- parsing gift item name and Jewel amount from live chat content

Relevant areas:

- `YTLiveChat/Contracts/Models/GiftItem.cs`
- `YTLiveChat/Contracts/Services/IYTLiveChat.cs`
- `YTLiveChat/Helpers/Parser.cs`
- `YTLiveChat/Services/YTLiveChat.cs`

### 1.3 Banner, poll, engagement, and redirect model expansion

Upstream significantly expanded live chat event coverage, including:

- `BannerItem`
- `PollItem`
- `EngagementItem`
- redirect-related contract additions
- richer parser coverage for live chat action types

Relevant areas:

- `YTLiveChat/Contracts/Models/BannerItem.cs`
- `YTLiveChat/Contracts/Models/PollItem.cs`
- `YTLiveChat/Contracts/Models/EngagementItem.cs`
- `YTLiveChat/Models/Response/LiveChatResponse.cs`
- `YTLiveChat/Helpers/Parser.cs`

### 1.4 Tools and diagnostics expansion

Upstream expanded the tooling layer substantially:

- `watch`
- `analyze`
- `snapshot`
- richer field-level baselines
- better handling of unknown and partially parsed actions

Relevant areas:

- `YTLiveChat.Tools/AnalyzeMode.cs`
- `YTLiveChat.Tools/SnapshotMode.cs`
- `YTLiveChat.Tools/WatchMode.cs`
- `YTLiveChat.Tools/LogReader.cs`
- `YTLiveChat.Tools/Program.cs`

### 1.5 Test and fixture growth

Upstream added a large amount of test and fixture coverage, especially around:

- banners
- polls
- streams page snapshots
- action parsing
- membership and live page scenarios

Relevant areas:

- `YTLiveChat.Tests/Helpers/ParserTests.cs`
- `YTLiveChat.Tests/Services/YTLiveChatServiceTests.cs`
- `YTLiveChat.Tests/TestData/*`

### 1.6 Build, framework, and dependency updates

Upstream also changed the repository baseline:

- added `net9.0` targets in core projects
- updated dependency versions from the older `10.0.3` line to `10.0.6`
- changed some build and packaging settings
- updated `global.json` roll-forward behavior

Relevant areas:

- `YTLiveChat/YTLiveChat.csproj`
- `YTLiveChat.DependencyInjection/YTLiveChat.DependencyInjection.csproj`
- `YTLiveChat.Example/YTLiveChat.Example.csproj`
- `global.json`
- `.github/workflows/*`

---

## 2. Feature Branch Additions

These are the major things introduced by `feat/add-overlay-project` on top of upstream.

### 2.1 OBS overlay application

The feature branch adds a dedicated browser-source overlay application:

- `YTLiveChat.Overlay`
- web host entry point
- live chat browser rendering
- OBS-oriented page styling and rendering behavior

Relevant areas:

- `YTLiveChat.Overlay/Program.cs`
- `YTLiveChat.Overlay/YTLiveChat.Overlay.csproj`
- `YTLiveChat.Overlay/wwwroot/index.html`
- `YTLiveChat.Overlay.ps1`

This is the most important branch-level product addition. Upstream does not provide this app layer.

### 2.2 Terminal status board application

The feature branch also adds a separate terminal-status board for OBS usage:

- `YTLiveChat.TerminalStatus`
- live status persistence
- admin input page
- display page
- SignalR-based state propagation

Relevant areas:

- `YTLiveChat.TerminalStatus/Program.cs`
- `YTLiveChat.TerminalStatus/Hubs/StatusHub.cs`
- `YTLiveChat.TerminalStatus/wwwroot/admin.html`
- `YTLiveChat.TerminalStatus/wwwroot/index.html`
- `YTLiveChat.TerminalStatus/YTLiveChat.TerminalStatus.csproj`

This is also branch-specific and does not exist upstream.

### 2.3 OBS/browser-source rendering optimization

The feature branch adds a substantial amount of application-layer behavior specifically for broadcast usage:

- browser-source friendly transparent rendering
- high-resolution OBS source handling
- text/background/cursor styling for on-stream readability
- chat rendering tuned for OBS placement and clarity

This work lives primarily in:

- `YTLiveChat.Overlay/wwwroot/index.html`
- `YTLiveChat.TerminalStatus/wwwroot/index.html`
- `YTLiveChat.TerminalStatus/wwwroot/admin.html`

### 2.4 Console/logging experience for local operators

The branch adds operational quality-of-life improvements for local execution, including:

- startup banners
- URL formatting
- console color formatting
- reduced noise in selected logs

Relevant areas:

- `YTLiveChat.Overlay/Program.cs`
- `YTLiveChat.TerminalStatus/Program.cs`

### 2.5 Terminal selection/cursor synchronization

The terminal board implementation adds branch-specific state synchronization behavior:

- cursor position tracking
- selection start/end tracking
- shift-selection and mouse-drag restoration behavior
- persistent saved state

Relevant areas:

- `YTLiveChat.TerminalStatus/Hubs/StatusHub.cs`
- `YTLiveChat.TerminalStatus/wwwroot/admin.html`
- `YTLiveChat.TerminalStatus/wwwroot/index.html`

### 2.6 Branch-local repository documentation and workflow helpers

The branch also introduces branch-specific repository operation notes:

- Git workflow guidance
- local operator instructions
- `.verysync` ignore handling

Relevant areas:

- `docs/git-workflow.md`
- `AGENTS.md`
- `GEMINI.md`
- `.gitignore`

---

## 3. Shared Files with Different Intent

Some files exist on both sides but differ because the branch is building an application layer while upstream is building library/tooling depth.

### 3.1 `Parser.cs`

This file contains both:

- upstream parser expansion for streams, gifts, banners, polls, and newer chat structures
- branch-specific live-page fallback handling for more robust live ID extraction

Branch-specific intent in this file includes:

- more tolerant live-page ID extraction
- extra fallback paths for `watch?v=...` detection
- resilience for browser/page variants seen in real deployment

### 3.2 `YTLiveChat.cs`

This file contains both:

- upstream library behavior and new API/event surface
- branch-specific operational handling for monitored live targets and inaccessible/no-live situations

### 3.3 Solution and repo shape

The branch extends the repo upward into runnable apps:

- upstream provides the reusable core and tooling
- the feature branch provides stream-ready application surfaces on top of that core

---

## 4. Practical Difference in Product Terms

### Upstream is now stronger at:

- core library coverage
- live chat model breadth
- streams metadata
- gifts, banners, polls, engagement events
- testing and analysis tooling

### The feature branch is stronger at:

- immediate OBS usability
- browser-source presentation
- terminal-status display workflows
- local operator experience
- stream overlay application behavior

In short:

- upstream is primarily a richer library + tools codebase
- this branch is that library plus actual broadcast-facing application layers

---

## 5. Files Mostly Unique to the Feature Branch

These files or areas are effectively branch-owned additions:

- `YTLiveChat.Overlay/*`
- `YTLiveChat.TerminalStatus/*`
- `YTLiveChat.Overlay.ps1`
- `docs/git-workflow.md`
- parts of `.gitignore`
- local operator docs such as `AGENTS.md` and `GEMINI.md`

---

## 6. Files Mostly Driven by Upstream

These files changed mainly because upstream evolved the core library and tooling:

- `YTLiveChat/Contracts/*`
- `YTLiveChat/Models/*`
- `YTLiveChat.Tools/*`
- `YTLiveChat.Tests/*`
- `YTLiveChat.Example/*`
- project files and dependency baselines

---

## 7. Integration Risk Areas

These are the files that deserve the most care during future rebases:

- `YTLiveChat/Helpers/Parser.cs`
- `YTLiveChat/Services/YTLiveChat.cs`
- `YTLiveChat/YTLiveChat.csproj`
- `YTLiveChat.DependencyInjection/YTLiveChat.DependencyInjection.csproj`
- `global.json`

Reason:

- upstream changes them frequently
- the feature branch also depends on them operationally
- parser and service changes can affect the overlay apps indirectly

Recommended rule:

- treat application-layer files and core library files separately during conflict resolution
- if a commit is clearly about overlay/UI behavior, be cautious before carrying unrelated parser/service changes into it

---

## 8. Suggested Mental Model

Use this repository model going forward:

- `upstream/master` = evolving library and tooling baseline
- local `master` = clean mirror of that baseline
- `feat/add-overlay-project` = application layer for OBS/browser-source usage

That framing makes future conflict resolution much easier:

- upstream changes usually improve the core
- branch changes usually improve the operator-facing runtime experience
