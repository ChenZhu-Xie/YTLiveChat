# Git Workflow

This repository uses a fork-based workflow:

- `upstream` is the source repository
- `origin` is your fork
- local `master` tracks the latest `upstream/master`
- daily development happens on short-lived `feat/*` branches

This document keeps the command list minimal and practical for the current setup.

## Quick Reference

Sync upstream into the current feature branch:

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git switch <feature-branch>
git rebase master
git push --force-with-lease origin <feature-branch>
```

Start a new feature branch from the latest upstream master:

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git push origin master
git switch -c <new-feature-branch>
```

Continue after a rebase conflict:

```powershell
git status
git add <resolved-files>
git rebase --continue
```

Create a backup branch before a risky rebase:

```powershell
git switch <feature-branch>
git branch backup/<feature-branch>-before-rebase
git rebase master
```

## Assumptions

- `git-swap auto` updates the repo-local SSH configuration for the current machine
- `upstream/master` is the true integration baseline
- feature branches are primarily single-owner branches, so `rebase` is preferred over `merge`

## 1. Sync upstream into the current feature branch

Use this when you are still working on the same feature branch and upstream has new commits.

```powershell
git-swap auto
git fetch upstream
git fetch origin
git switch master
git merge --ff-only upstream/master
git switch <feature-branch>
git rebase master
git push --force-with-lease origin <feature-branch>
```

Example:

```powershell
git-swap auto
git fetch upstream
git fetch origin
git switch master
git merge --ff-only upstream/master
git switch feat/add-overlay-project
git rebase master
git push --force-with-lease origin feat/add-overlay-project
```

Notes:

- Run `git-swap auto` before any fetch/push/rebase step.
- `git fetch origin` is optional, but useful for checking fork state.
- After a `rebase`, push with `--force-with-lease`, not plain `push`.

## 2. Start a new feature branch from the latest upstream master

Use this when the current feature is done and you want a clean branch for new work.

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git push origin master
git switch -c <new-feature-branch>
```

Example:

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git push origin master
git switch -c feat/next-work
```

Notes:

- `git push origin master` is recommended so the fork's default branch stays aligned.
- Prefer creating a new branch instead of reusing an old feature branch.

## 3. Push the current branch to the fork

If the branch has not been rebased:

```powershell
git-swap auto
git push origin <branch>
```

If the branch has been rebased:

```powershell
git-swap auto
git push --force-with-lease origin <branch>
```

## 4. Resolve rebase conflicts

When `git rebase master` stops with conflicts:

```powershell
git status
```

Resolve the files, then:

```powershell
git add <file1> <file2>
git rebase --continue
```

Abort the rebase if needed:

```powershell
git rebase --abort
```

If `git rebase --continue` opens Vim and you want to keep the default commit message:

1. Press `Esc`
2. Type `:wq`
3. Press Enter

If you want to skip the editor prompt:

```powershell
git -c core.editor=true rebase --continue
```

## 4.1 Backup branch strategy

For this repository, the recommended rule is:

- keep local `master` as a clean upstream mirror
- do daily work on `feat/*`
- create a backup branch before a large `rebase` or risky conflict resolution session

Example:

```powershell
git switch feat/add-overlay-project
git branch backup/feat-add-overlay-project-before-rebase
git rebase master
```

If the rebase goes bad, you can either abort it:

```powershell
git rebase --abort
```

Or switch back to the backup branch:

```powershell
git switch backup/feat-add-overlay-project-before-rebase
```

This is safer than doing regular development directly on local `master`.

## 5. Useful read-only checks

Check remotes:

```powershell
git remote -v
```

Check repo-local and global SSH command configuration:

```powershell
git config --show-origin --get-regexp "core\\.sshCommand"
```

Check local branches and tracking state:

```powershell
git branch -vv
```

Check current branch status:

```powershell
git status --short --branch
```

Check divergence between local and fork branch:

```powershell
git log --oneline --left-right --cherry origin/<branch>...<branch>
```

## 6. Repo-local SSH guidance

This repository may use repo-local `core.sshCommand`, managed by `git-swap auto`.

Recommended rule:

- run `git-swap auto` before any command that talks to GitHub

If Git starts using the wrong identity, check:

```powershell
git config --show-origin --get-regexp "core\\.sshCommand"
ssh -G github.com | Select-String identityfile
```

## 7. Default habit

For ongoing feature work:

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git switch <feature-branch>
git rebase master
git push --force-with-lease origin <feature-branch>
```

For new work:

```powershell
git-swap auto
git fetch upstream
git switch master
git merge --ff-only upstream/master
git push origin master
git switch -c <new-feature-branch>
```
