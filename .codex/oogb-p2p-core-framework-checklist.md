# OOGB P2P Core Framework Checklist

Source plan: `.codex/oogb-p2p-core-framework-plan.md`

## Scope

- [x] Keep OOGB implementation isolated under `Assets/OOGB`.
- [x] Treat v1 as a reusable pure-P2P core framework, not a playable LAN or online backend.
- [x] Keep session owner limited to room/session flow control.
- [x] Avoid Listen Server, Dedicated Server, and host-owned gameplay authority messages.
- [x] Keep the first transport layer adapter-first and testable without a production backend.

## Package Structure

- [x] Add `Assets/OOGB/Runtime` for reusable runtime code.
- [x] Add `Assets/OOGB/Tests/EditMode` for deterministic edit-mode tests.
- [x] Add runtime assembly definition.
- [x] Add edit-mode test assembly definition.
- [x] Preserve Unity `.meta` files for new OOGB assets.
- [x] Add OOGB README/documentation under `Assets/OOGB`.

## Core Types

- [x] Define `OogbPeerId`.
- [x] Define `OogbSessionId`.
- [x] Define tick representation.
- [x] Define `OogbProtocolVersion`.
- [x] Define session states.
- [x] Define message categories for session control, player input, state hash, and heartbeat.

## Transport

- [x] Define OOGB-owned transport abstraction.
- [x] Support direct peer send.
- [x] Support broadcast.
- [x] Support received-message polling.
- [x] Support peer connection/disconnection state.
- [x] Avoid direct dependency on Unity Transport, Relay, Steam, Switch SDK, or raw UDP in the core session layer.
- [x] Implement in-memory test transport.
- [x] Cover simulated multi-peer messaging in tests.
- [x] Re-run tests after the current UDP loopback transport addition is included in the test assembly.

## Session Layer

- [x] Implement session creation.
- [x] Implement peer join/peer tracking flow.
- [x] Implement ready state flow.
- [x] Implement owner-controlled start flow.
- [x] Keep owner as session-control only, not gameplay authority.
- [x] End session when owner disconnects in v1.
- [x] Define deterministic non-owner disconnect behavior.

## Deterministic Tick And Input

- [x] Let each peer submit local input for a tick.
- [x] Exchange input messages between peers.
- [x] Advance simulation only when required peer inputs are available.
- [x] Use opaque byte-array payloads for v1 input data.
- [x] Cover two-peer tick advancement in tests.
- [x] Cover three-peer tick advancement in tests.
- [x] Cover blocked advancement when inputs are missing in tests.

## State Hash Validation

- [x] Exchange state hashes between peers.
- [x] Continue when state hashes match.
- [x] Mark session desynced when hashes mismatch.
- [x] End match/session on desync in v1.
- [x] Leave automatic desync recovery out of v1.

## Diagnostics

- [x] Expose framework-level diagnostic events/logs.
- [x] Report protocol mismatch.
- [x] Report owner disconnect.
- [x] Report hash mismatch/desync.
- [x] Report session end reason.
- [x] Review whether peer join/leave diagnostics are complete enough for integration debugging.

## Tests

- [x] Test session creation and peer ready/start flow.
- [x] Test input broadcast and tick advancement.
- [x] Test protocol version mismatch rejection.
- [x] Test owner disconnect ends session.
- [x] Test non-owner disconnect behavior.
- [x] Test matching state hashes continue the session.
- [x] Test mismatched state hashes end session as desync.
- [x] Keep tests under `Assets/OOGB/Tests/EditMode`.
- [x] Last saved Unity edit-mode result: `11 total, 11 passed, 0 failed` at `2026-07-13 01:35:07Z`.
- [x] Run a fresh Unity edit-mode test pass after the latest local changes.

## Repository State Follow-Up

- [x] Decide whether `Assets/OOGB` should be committed in this repo, added as a submodule, or left untracked for now.
- [x] Review the modified `Assets/Scripts/OogbTwoClient/OogbTwoClientHarness.cs` separately from the core framework checklist.
