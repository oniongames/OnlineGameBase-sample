# Eight-Player Internet P2P Plan

## Target 1: Eight Players

1. Enforce a maximum of eight total peers, including ghost peers.
2. Replace fixed Player A/B handling with peer slots Player 1 through Player 8.
3. Configure localhost UDP as a full mesh using one port per slot.
4. Create one local player and up to seven remote player objects per application.
5. Apply, reset, finish, and clean up player state through peer-to-player maps.
6. Validate 1-, 2-, 4-, and 8-peer rooms, disconnects, ghost roles, and room restart.

## Target 2: Internet NAT Traversal

### Architecture

1. Keep `IOogbTransportPeer` as the sole gameplay-facing transport contract.
2. Add platform transport adapters; all adapters exchange the same OOGB session-control, deterministic-input, and state-hash messages.
3. Keep signaling limited to room membership, SDP, and ICE candidate exchange. Signaling and relays have no gameplay authority.
4. Use STUN to discover direct peer paths and TURN only as a non-authoritative relay fallback.

### Development and Release Services

1. Use `stun:stun.l.google.com:19302` for development only.
2. Do not depend on Google's public STUN service for the Steam or Switch release.
3. Deploy the existing WSS signaling service and self-hosted coturn for release testing and production.
4. Issue short-lived TURN credentials and do not embed a long-lived TURN secret in a client build.

### Platform Adapters

1. Steam/Windows: implement a Unity WebRTC data-channel adapter behind `IOogbTransportPeer`.
2. Switch: implement a separate adapter using Nintendo-approved networking/WebRTC APIs that supports ICE with the same STUN/TURN services.
3. Do not make `com.unity.webrtc` a required dependency of the shared OOGB core, because it is not the Switch transport solution.
4. Keep platform-specific code outside the deterministic session layer so Steam and Switch peers remain protocol-compatible.

### Delivery Order

1. Define signaling and ICE configuration contracts, including the transport adapter lifecycle and error states.
2. Implement and test the Steam/Windows WebRTC adapter against local signaling and Google STUN.
3. Deploy and test the WSS signaling service plus coturn with self-hosted STUN/TURN.
4. Implement the Switch adapter after Nintendo's approved networking SDK/API path is available.
5. Validate direct and relayed connections between separate internet networks on Steam, then perform Switch platform certification-oriented testing.

## Constraints

- Gameplay remains pure P2P; signaling and TURN have no gameplay authority.
- Eight is the total room capacity; a ghost uses one peer slot.
- No mid-race join or host migration in this target.
- `com.unity.webrtc` is subject to the Unity Companion License; preserve required license and third-party notices.
- Release transport selection must be approved by Nintendo for the Switch build.
