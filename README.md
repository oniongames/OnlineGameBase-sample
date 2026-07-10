# OnlineGameBase Sample

Unity sample project for Onion Online Game Base (OOGB).

This repository contains the sample game project. The reusable OOGB networking
layer lives in `Assets/OOGB` as a Git submodule.

## Requirements

- Unity `6000.3.19f1`
- Git with submodule support

## Clone

```sh
git clone --recurse-submodules git@github.com:oniongames/OnlineGameBase-sample.git
```

If the repository was cloned without submodules:

```sh
git submodule update --init --recursive
```

## Project Layout

- `Assets/Scripts`: sample game runtime scripts
- `Assets/Editor`: editor-only scripts
- `Assets/OOGB`: reusable OOGB submodule
- `Packages`: Unity package dependency files
- `ProjectSettings`: Unity project settings

Do not commit generated Unity cache folders such as `Library`, `Temp`, `Logs`,
`obj`, or `UserSettings`.

## OOGB Architecture

OOGB is pure P2P. The sample should not add Listen Server or Dedicated Server
gameplay authority, and OOGB should not use host-owned authoritative gameplay
messages such as `Position`, `HP`, `Damage`, or `Win/Loss`.

The session owner may control room and session flow only. Gameplay
synchronization should use deterministic input exchange plus periodic state-hash
validation.

## Tests

Run edit-mode tests from the Unity command line:

```sh
Unity -batchmode -projectPath . -runTests -testPlatform editmode -quit -logFile Logs/editmode.log
```

Run play-mode tests:

```sh
Unity -batchmode -projectPath . -runTests -testPlatform playmode -quit -logFile Logs/playmode.log
```
