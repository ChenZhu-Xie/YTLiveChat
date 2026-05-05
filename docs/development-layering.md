# Development Layering

This note is for the current working style of this repository:

- single feature branch
- small daily increments
- prompt-driven development
- frequent upstream sync by `rebase`

The goal is not to force a different workflow. The goal is to reduce rebase pain and keep change scope easier to reason about.

## 1. The Four Layers

Think about the repository in four layers.

### 1.1 Core layer

This is the library and parser foundation.

Typical files:

- `YTLiveChat/Contracts/*`
- `YTLiveChat/Models/*`
- `YTLiveChat/Services/*`
- `YTLiveChat/Helpers/Parser.cs`
- `YTLiveChat*.csproj`

Characteristics:

- high rebase risk
- upstream changes this area frequently
- mistakes here can break multiple projects at once

Recommended rule:

- touch this layer only when the app truly needs a new capability or a real parsing fix
- keep changes here as small as possible

### 1.2 App layer

This is the branch's main product surface.

Typical files:

- `YTLiveChat.Overlay/*`
- `YTLiveChat.TerminalStatus/*`

Characteristics:

- lowest conflict risk relative to upstream
- highest direct value for OBS/browser-source usage
- best place for daily incremental work

Recommended rule:

- prefer solving feature requests here first
- if the request can be handled entirely in app code, do not push it down into core

### 1.3 Ops / DX layer

This is the operator and repository support layer.

Typical files:

- `docs/*`
- local scripts
- startup banners
- console formatting
- `AGENTS.md`
- `GEMINI.md`
- `.gitignore`

Characteristics:

- low product risk
- useful for local workflow quality
- usually safe to evolve independently

Recommended rule:

- keep this layer separate from parser/service fixes when possible

### 1.4 Bridge layer

This is not always a separate directory. It is the seam between app code and core code.

Examples:

- event payloads needed by Overlay
- state shape needed by TerminalStatus
- minimal parser/service additions required to support app behavior

Characteristics:

- medium risk
- easy to overgrow
- often the real source of future rebase pain

Recommended rule:

- only add bridge behavior when app code cannot solve the problem by itself
- keep bridge contracts narrow and explicit

## 2. Practical Change Priority

When a new request arrives, use this order of preference:

1. solve it in the App layer
2. if needed, add a small Bridge layer change
3. only then modify the Core layer
4. keep Ops / DX changes separate unless they are directly part of the same task

Short version:

- App first
- Bridge only when needed
- Core last

## 3. High-Risk Files

These files deserve extra caution before rebase and during conflict resolution:

- `YTLiveChat/Helpers/Parser.cs`
- `YTLiveChat/Services/YTLiveChat.cs`
- `YTLiveChat/YTLiveChat.csproj`
- `YTLiveChat.DependencyInjection/YTLiveChat.DependencyInjection.csproj`
- `global.json`

Why:

- upstream changes them often
- local branch behavior also depends on them
- small mistakes here can break the whole repo

Recommended rule:

- if a commit is obviously about UI, OBS, rendering, or local operator behavior, be skeptical before allowing it to drag parser/service changes into the same resolution

## 4. Commit Discipline for Single-Branch Development

You can keep using a single long-lived feature branch, but the commit intent should still stay narrow.

Good commit shapes:

- `feat(overlay): ...`
- `fix(terminalstatus): ...`
- `fix(parser): ...`
- `fix(integration): ...`
- `docs(git): ...`

Avoid combining these in one commit:

- parser fix
- OBS style tweak
- console banner change
- workflow docs

Even in a single branch, narrow commits make rebases much easier to reason about.

## 5. Rebase Safety Rule

Before a risky rebase:

```powershell
git switch <feature-branch>
git branch backup/<feature-branch>-before-rebase
git rebase master
```

This repository should treat backup branches as normal operating procedure, not as an emergency trick.

## 6. Working Style Recommendation

For this repository, the recommended long-term style is:

- keep `master` as a clean upstream mirror
- do daily work in `feat/*`
- spend most effort in:
  - `YTLiveChat.Overlay`
  - `YTLiveChat.TerminalStatus`
- treat `Parser.cs`, `YTLiveChat.cs`, and project files as high-risk core areas

That means you can keep the current "single prompt, single day, small step" development style, while still reducing integration damage.

## 7. Short Mental Model

Use this simple model:

- upstream grows the library downward
- this branch grows the application layer upward

When both sides try to grow in the same core file, conflicts get expensive.

When app work stays in app files and core work stays minimal, rebases are much easier.
