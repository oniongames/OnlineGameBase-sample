# OOGB Local Network Test Flow

## Two-client UDP loopback harness (one PC)

This is a real two-process UDP test on one computer, not a LAN test.

1. Open `OOGBTwoClientTest` in two processes, such as the Unity Editor and a standalone build.
2. Select `UDP Loopback` in each process.
3. Configure the same ports: Client A / Owner uses `7777`; Client B uses `7778`.
4. Start the corresponding UDP role in each process. The harness sends packets through `127.0.0.1`.
5. Press `Set Ready` in both processes.
6. In Client A, press `Owner Start`.
7. Submit local input in both processes. A tick advances only after every peer's input for that tick arrives.
8. Publish matching hashes to confirm continued operation, or different hashes to confirm the session ends as `Desync`.

The harness polls every frame and auto-advances a tick after the required inputs arrive.

## Platformer LAN UDP test (multiple machines)

1. Run the platformer build on every machine on the same LAN.
2. Select `LAN UDP` on every instance.
3. Use the same room size and base port on every instance.
4. Configure the owner as slot 1; every client selects a different slot.
5. Allow LAN discovery to populate `LAN Hosts`. If the network blocks discovery, enter each machine's LAN IP address manually.
6. The slot ports are `basePort + slot - 1`; each instance binds its own slot port and sends to the other slot endpoints.
7. Owner presses `Create Room`; all peers press `Ready`; then the owner presses `Owner Start`.
8. During the race, each peer broadcasts deterministic local input. Simulation advances only when all peer inputs exist for the current tick, and periodic state hashes detect desynchronization.

## Scope and verification

- The `OogbTwoClientHarness` loopback mode uses `127.0.0.1`, so it cannot validate cross-machine LAN reachability.
- The platformer LAN mode supplies configured LAN host IP addresses to the same UDP transport.
- Transport configuration is not a reachability check. Confirm a real LAN test by completing Ready -> Owner Start -> bidirectional input/tick advancement on separate machines.
- Edit-mode UDP tests validate loopback message delivery and owner-start behavior only; they are not a multi-machine integration test.

## Internet signaling through Cloudflare Tunnel

Cloudflare Tunnel can publish the OOGB WebSocket signaling service as a public `wss://` endpoint. It is only for signaling (room roster plus WebRTC SDP/ICE forwarding), not for gameplay packets.

```text
Unity client -> WSS -> Cloudflare Tunnel -> OOGB signaling server :8080
                                          |
                                  SDP / ICE exchange only
                                          |
Unity peers -------- WebRTC data channels / TURN -------- Unity peers
```

### Install and initialize cloudflared

Use a remotely managed tunnel for this service. Cloudflare keeps its configuration in the dashboard and the host only needs the tunnel token.

1. Prerequisites: create a Cloudflare account, add an owned domain to Cloudflare, and run the signaling service on a host with outbound Internet access.
2. Install `cloudflared` on that host.

   Debian / Ubuntu:

   ```sh
   sudo mkdir -p --mode=0755 /usr/share/keyrings
   curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg | sudo tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null
   echo "deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main" | sudo tee /etc/apt/sources.list.d/cloudflared.list
   sudo apt-get update && sudo apt-get install cloudflared
   cloudflared --version
   ```

   Windows: download the current `cloudflared.exe` from Cloudflare's downloads page, place it on `PATH`, and verify with `cloudflared.exe --version` in PowerShell.

   Docker alternative: no host installation is required; run the `cloudflare/cloudflared` container in step 5.

3. In the Cloudflare dashboard, go to `Networking` -> `Tunnels`, create a tunnel such as `oogb-signaling`, and select the host operating system. Copy the generated tunnel token or installation command. Treat the token as a secret.
4. Add a public hostname such as `signal.example.com` and set its service URL to `http://localhost:8080`.

   The OOGB signaling server listens as plain HTTP/WebSocket on port 8080. Cloudflare supplies the public TLS/WSS endpoint.

5. Run the tunnel on the signaling host:

   ```sh
   cloudflared tunnel --no-autoupdate run --token <TUNNEL_TOKEN>
   ```

   For a persistent Linux service, use the installation command offered by the Cloudflare dashboard, or:

   ```sh
   sudo cloudflared service install <TUNNEL_TOKEN>
   ```

   Docker alternative:

   ```sh
   docker run --pull always cloudflare/cloudflared:latest \
     tunnel --no-autoupdate run --token <TUNNEL_TOKEN>
   ```

6. Verify that the tunnel status is `Healthy` in the dashboard and that `https://signal.example.com/health` returns the signaling service health response.
7. In the platformer, select `Internet WebRTC` and enter `wss://signal.example.com` as the signaling URL before creating or joining a room.

### Scope and security

- Cloudflare Tunnel creates an outbound connection, so the signaling host does not need public inbound access to port 8080.
- Cloudflare Tunnel is not a TURN server and does not transport gameplay UDP traffic.
- Keep STUN/TURN configured in the signaling service for peer NAT traversal. For reliable internet play, provide a public TURN server and have signaling return short-lived TURN credentials.
- Never embed the TURN shared secret in a Unity client build.
- For temporary development only, `cloudflared tunnel --url http://localhost:8080` provides a random `trycloudflare.com` hostname. Use a named tunnel and owned hostname for ongoing deployment.
