# Eight-Player Internet P2P Plan

## Target 1: Eight Players

1. Enforce a maximum of eight total peers, including ghost peers.
2. Replace fixed Player A/B handling with peer slots Player 1 through Player 8.
3. Configure localhost UDP as a full mesh using one port per slot.
4. Create one local player and up to seven remote player objects per application.
5. Apply, reset, finish, and clean up player state through peer-to-player maps.
6. Validate 1-, 2-, 4-, and 8-peer rooms, disconnects, ghost roles, and room restart.

## Target 2: Internet NAT Traversal

1. Add a WebRTC ICE transport behind the existing OOGB transport interface.
2. Use signaling only for room membership, SDP, and ICE candidate exchange.
3. Use STUN for direct paths and TURN only as a non-authoritative fallback relay.
4. Run a small WSS signaling service and coturn with short-lived TURN credentials.
5. Validate direct and relayed connections from separate internet networks.

## Constraints

- Gameplay remains pure P2P; signaling and TURN have no gameplay authority.
- Eight is the total room capacity; a ghost uses one peer slot.
- No mid-race join or host migration in this target.
