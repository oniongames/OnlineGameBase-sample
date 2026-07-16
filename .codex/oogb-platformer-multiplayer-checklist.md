# OOGB Platformer Multiplayer Checklist

## Waiting Room

- [x] Add owner waiting-room UI for creating a room.
- [x] Show owner room state, peers, session state, and start action.
- [x] Prevent owner start until OOGB peers are ready.
- [x] Add client waiting-room UI for joining a room.
- [x] Support localhost owner/client ports for v1.
- [x] Show connected, ready, and started state through OOGB diagnostics.
- [ ] Add automatic LAN owner discovery.

## Multiplayer Scene And Players

- [x] Auto-bootstrap multiplayer controller in `SampleScene`.
- [x] Reuse the existing platformer scene for the first multiplayer race.
- [x] Convert the solo player to a local network-controlled player during multiplayer.
- [x] Instantiate a remote player replica at runtime.
- [x] Use separate owner/client spawn offsets around the existing spawn point.
- [x] Make the camera follow the local player.
- [x] Disable direct local input on remote player replicas.
- [ ] Add authored scene spawn points for more than two players.
- [ ] Add a dedicated multiplayer scene asset after the flow is stable.

## Gameplay Sync

- [x] Encode per-tick input payloads with horizontal move, jump pressed, jump released, and finish flag.
- [x] Submit local input through `OogbP2PSession.SubmitLocalInput`.
- [x] Add `OogbP2PSession.TryGetInput` for game-side payload application.
- [x] Apply owner/client inputs to the matching player controllers.
- [x] Advance race ticks only after both required peer inputs are available.
- [x] Keep OOGB owner as session-control only.
- [ ] Add periodic state-hash validation for platformer race state.
- [ ] Refactor the platformer physics path toward fully deterministic lockstep.

## Race Goal And Results

- [x] Route `VictoryZone` through multiplayer result handling when a multiplayer race is active.
- [x] Record local finish as a ticked input flag.
- [x] Record remote finish from received tick input.
- [x] Decide win, lose, or draw from finish tick order.
- [x] Show result in the multiplayer overlay.
- [x] Add debug finish buttons for local, remote/ghost, and draw result checks.
- [x] Preserve original solo victory behavior outside multiplayer race state.
- [ ] Add polished result UI separate from the debug overlay.
- [ ] Add restart/rematch session flow without reloading the scene.

## Ghost Client Replay

- [x] Add `Ghost` as a third network role next to `Owner` and `Client`.
- [x] Run ghost as the client peer in a two-instance network test.
- [x] Record only current local-user input frames.
- [x] Allow local ghost recording during network play.
- [x] Save ghost recordings in `PlayerPrefs` by ghost name.
- [x] Load saved ghost input as the ghost client's local input source.
- [x] Add clear-ghost control.
- [ ] Add import/export ghost files outside `PlayerPrefs`.
- [ ] Add ghost client replay validation tests.

## Tests And Manual Verification

- [ ] Add edit-mode tests for race input payload encode/decode.
- [ ] Run fresh Unity EditMode tests.
- [ ] Manual test: owner creates room, client joins, both ready, owner starts.
- [ ] Manual test: both players spawn at separate positions and move independently.
- [ ] Manual test: record a ghost, then replay it from a second instance using `Ghost` role.
- [ ] Manual test: record local ghost during network mode.
- [ ] Manual test: ghost client ignores keyboard/gamepad input and uses only recorded input.
- [ ] Manual test: finish buttons produce local win, remote/ghost loss, and draw.
- [ ] Manual test: first player to goal wins.
- [ ] Manual test: same-tick finish draws.
- [ ] Manual test: disconnect during waiting room or play shows correct session result.
