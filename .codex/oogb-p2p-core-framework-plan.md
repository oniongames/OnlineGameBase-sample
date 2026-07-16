# OOGB P2P Core Framework Plan

## Summary

Build OOGB as a reusable Unity sub-repository under `Assets/OOGB` only. V1 will implement the pure P2P core framework, not a playable LAN/online backend yet. The system will be transport-agnostic, with an in-memory test adapter so session flow, peer messaging, tick input sync, and hash validation can be tested before choosing Unity Transport, Relay, or another production backend.

## Key Changes

- Add an OOGB package-style structure under `Assets/OOGB`:
  - `Runtime` for reusable networking code.
  - `Tests/EditMode` for deterministic unit tests.
  - Assembly definitions for runtime and tests.
- Update root `AGENTS.md` with OOGB-specific guidance: keep OOGB code isolated under `Assets/OOGB`, follow pure P2P design, and do not add gameplay authority hosts.
- Do not create or update Codex long-term memory in this implementation pass. Treat OOGB design notes as repo documentation unless explicitly requested separately.

## Implementation Steps

1. Define core types:
   - `OogbPeerId`, `OogbSessionId`, `OogbTick`, `OogbProtocolVersion`.
   - Session states: created, peer check, ready, playing, finished, closed.
   - Message categories: session control, player input, state hash, heartbeat.
2. Define transport abstraction:
   - Interface for sending messages to one peer, broadcasting, polling received messages, and reporting connection state.
   - No direct dependency on Unity Transport, Relay, Steam, Switch SDK, or raw UDP in v1.
3. Implement in-memory test transport:
   - Simulate multiple peers in one process.
   - Support broadcast, disconnect, dropped peer, and message ordering tests.
4. Implement session layer:
   - Session owner controls room creation, peer list, ready state, initial parameters, start tick, and close/end flow.
   - Session owner is not gameplay authority.
   - Owner disconnect ends the match/session in v1.
5. Implement deterministic tick/input layer:
   - Each peer submits local input for a tick.
   - Peers exchange input messages.
   - Simulation can advance only when required peer inputs for the tick are available or the configured input-delay rule allows it.
   - V1 input payload can be a small opaque byte array so game-specific input schemas are added later.
6. Implement validation layer:
   - Peers exchange periodic state hashes.
   - Matching hashes continue.
   - Mismatch marks the session desynced and ends the match in v1.
   - No automatic desync recovery in v1.
7. Add diagnostics:
   - Lightweight logs/events for peer join/leave, protocol mismatch, owner disconnect, hash mismatch, and session end reason.
   - Keep logs framework-level; do not add analytics or backend dependencies.
8. Add edit-mode tests:
   - Session creation and peer join/ready/start flow.
   - Input broadcast and tick advancement across 2-4 simulated peers.
   - Protocol version mismatch rejection.
   - Owner disconnect ends session.
   - Non-owner disconnect follows deterministic session-end or leave behavior selected in code default.
   - State hash match continues; mismatch ends session.
   - No authoritative game-state messages are accepted as core gameplay truth.

## Public Interfaces

- Runtime API should expose only OOGB-owned abstractions:
  - Session manager/facade for creating, joining, starting, ticking, and closing sessions.
  - Transport interface and in-memory implementation.
  - Message/input/hash structs.
  - Diagnostic event callbacks.
- Game projects using OOGB should provide:
  - Local input bytes per tick.
  - State hash per validation interval.
  - Optional handlers for session events and end reasons.

## Test Plan

- Run Unity edit-mode tests:
  `Unity -batchmode -projectPath . -runTests -testPlatform editmode -quit -logFile Logs/editmode.log`
- Check Unity errors with:
  `~/bin/unitylog_failed.sh`
- No play-mode or scene test is required for v1 because integration scope is `Assets/OOGB` only.

## Assumptions

- V1 target is "core framework," not playable LAN or platform online.
- Transport is adapter-first with only an in-memory test backend.
- Implementation must stay under `Assets/OOGB`; existing Platformer Microgame gameplay and scenes remain untouched.
- Pure P2P means no Listen Server gameplay authority and no Dedicated Server.
- Session owner is room/session control only.
- Initial tick target from design docs is 60Hz, but exact runtime tuning values can remain configurable constants.
