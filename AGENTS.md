# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 6 project using editor version `6000.3.19f1`. Runtime source lives in `Assets/Scripts`, grouped by purpose: `Core` for simulation utilities, `Gameplay` for event-style behaviours, `Mechanics` for player/enemy/world components, `Model` for shared state, `UI` for menus and HUD, and `View` for presentation helpers. Editor-only code belongs in `Assets/Editor`. OOGB (Onion Online Game Base) is isolated under `Assets/OOGB` and should remain reusable as a sub-repository for other Unity projects. Unity package dependencies are tracked in `Packages/manifest.json`; project-level Unity settings are in `ProjectSettings`.

Do not edit generated or local cache folders such as `Library`, `Temp`, `Logs`, `obj`, or `UserSettings` unless a task explicitly requires it.

## OOGB Architecture Rules

Keep OOGB code and tests inside `Assets/OOGB`. The initial architecture is pure P2P: no Listen Server gameplay authority, no Dedicated Server gameplay authority, and no host-owned authoritative `Position`, `HP`, `Damage`, or `Win/Loss` messages. The session owner may control room/session flow only. Gameplay synchronization should use deterministic input exchange plus periodic state-hash validation.

## Build, Test, and Development Commands

- Open locally with Unity Hub or the Unity Editor matching `ProjectSettings/ProjectVersion.txt`.
- Run edit-mode tests from the command line:
  `Unity -batchmode -projectPath . -runTests -testPlatform editmode -quit -logFile Logs/editmode.log`
- Run play-mode tests:
  `Unity -batchmode -projectPath . -runTests -testPlatform playmode -quit -logFile Logs/playmode.log`
- Check recent Unity logs with `~/bin/unitylog.sh`; check recent Unity error logs with `~/bin/unitylog_failed.sh`. Both scripts default to the last 1024 lines and accept a line-count argument, for example `~/bin/unitylog_failed.sh 2000`. Prefer `unitylog_failed.sh` when only compact Console-like error summaries are needed.

## Coding Style & Naming Conventions

Use C# conventions consistent with the existing scripts: `PascalCase` for classes, methods, properties, and Unity event methods; `camelCase` for local variables and private fields. Keep MonoBehaviour classes focused on one scene responsibility and place new files in the folder that matches their role. Preserve Unity `.meta` files when adding, moving, or renaming assets.

## Testing Guidelines

The Unity Test Framework package is installed. Add OOGB edit-mode tests under `Assets/OOGB/Tests/EditMode` for deterministic networking logic. Use play-mode tests only for scene or MonoBehaviour behaviour. Name test files after the unit under test, for example `OogbP2PSessionTests.cs`.

## Commit & Pull Request Guidelines

Git history is not available in this checkout, so use concise imperative commit messages such as `Add player jump input test` or `Fix enemy patrol reset`. Pull requests should describe the gameplay or editor-facing change, list test results, link related issues, and include screenshots or short clips for visible scene, prefab, UI, or animation changes.
