# RespawnSwitch Workspace Archive and GitHub Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the complete RespawnSwitch project in the established desktop Codex workspace and publish a sanitized, reproducible source repository to GitHub.

**Architecture:** Keep a complete local archive under the workspace `项目` area, including historical binaries and records, while placing only directly usable packages under `交付成品`. GitHub receives source, tests, build scripts, requirements, and sanitized history documents; build caches, credentials, local paths, and binary release archives are excluded.

**Tech Stack:** Windows PowerShell, Git, GitHub CLI, .NET 8, Markdown, SHA-256.

## Global Constraints

- Resolve `C:\Users\1\Desktop\Codex长期工作区_2026-08-02` as a junction before copying.
- Name the project `复活切换工具_2026-08-15`.
- The project archive must contain `work`, `outputs`, `项目说明.txt`, and `项目错误记录.txt`.
- Preserve 0.1.0 and 0.2.0 deliverables locally, but do not commit release ZIPs, build caches, NuGet packages, logs, or credentials to GitHub.
- Create a new project overview file plus history, operating requirements, error, and verification records.
- Run secret-pattern scans, Release build/tests, and remote/local parity checks before reporting completion.

---

### Task 1: Inventory and Safety Boundary

**Files:**
- Inspect: repository status, branches, outputs, documentation, and workspace rules.
- Create: `docs/项目历史记录.md`, `docs/操作要求.md`, and `docs/最终验证记录.md`.

- [ ] Verify the exact source worktree and current commit.
- [ ] Inspect all candidate files and exclude credentials, caches, artifacts, and absolute-path logs from GitHub.
- [ ] Record verified development history, user-facing requirements, known limitations, and final checks.

### Task 2: Desktop Workspace Archive

**Files:**
- Create: `项目/复活切换工具_2026-08-15/项目说明.txt`.
- Create: `项目/复活切换工具_2026-08-15/项目错误记录.txt`.
- Create: `项目/复活切换工具_2026-08-15/work/`.
- Create: `项目/复活切换工具_2026-08-15/outputs/`.

- [ ] Copy the complete sanitized source checkout and historical design/plan files into `work/源码与历史`.
- [ ] Copy every prior RespawnSwitch output into the local project `outputs` archive.
- [ ] Copy only final 0.2.0 package, checksum, and quick guide to `交付成品/复活切换工具_2026-08-15`.
- [ ] Verify the copied archive structure and SHA-256.

### Task 3: GitHub Publication

**Files:**
- Modify: `.gitignore` only if required to exclude local and binary artifacts.
- Create: GitHub repository for RespawnSwitch.

- [ ] Confirm `gh` is installed and authenticated.
- [ ] Confirm intended source changes and commit the new history/requirements/verification documents.
- [ ] Run Release build, complete test suite, and independent secret scans.
- [ ] Create a private GitHub repository when no explicit visibility requirement exists.
- [ ] Push the verified branch, establish the intended default branch, and inspect the remote tree.
- [ ] Fetch the remote and compare local `HEAD` with the remote commit before reporting success.

## Plan Self-Review

- Coverage: local archive, previous files, history, operating requirements, new overview file, final delivery, credential exclusion, GitHub publication, and parity verification are assigned.
- Separation: complete historical binaries remain local; GitHub receives reproducible source and sanitized records only.
- Placeholders: none.
