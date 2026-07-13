using System;
using System.Collections.Generic;
using OOGB;
using UnityEngine;

public sealed class OogbTwoClientHarness : MonoBehaviour
{
    private enum HarnessMode
    {
        InMemorySimulation,
        UdpLoopback
    }

    private enum UdpRole
    {
        ClientAOwner,
        ClientBPeer
    }

    private static readonly OogbProtocolVersion ProtocolVersion = new OogbProtocolVersion(1, 0, 0);
    private static readonly OogbSessionId SessionId = new OogbSessionId("local-two-client-test");
    private static readonly OogbPeerId OwnerPeerId = new OogbPeerId("Client-A");
    private static readonly OogbPeerId ClientPeerId = new OogbPeerId("Client-B");

    private readonly List<string> logLines = new List<string>();

    private HarnessMode mode = HarnessMode.InMemorySimulation;
    private UdpRole udpRole = UdpRole.ClientAOwner;
    private OogbInMemoryNetwork network;
    private OogbUdpLoopbackTransportPeer udpTransport;
    private OogbP2PSession ownerSession;
    private OogbP2PSession clientSession;
    private OogbP2PSession localUdpSession;
    private Vector2 logScroll;
    private int inputValueA = 1;
    private int inputValueB = 2;
    private ulong matchingHash = 100;
    private ulong mismatchHashA = 100;
    private ulong mismatchHashB = 999;
    private string ownerPortText = "7777";
    private string clientPortText = "7778";

    private void Awake()
    {
        ResetInMemoryHarness();
    }

    private void OnDestroy()
    {
        DisposeUdpTransport();
    }

    private void Update()
    {
        PollSessions();
    }

    private void OnGUI()
    {
        const int margin = 16;
        var width = Mathf.Min(Screen.width - margin * 2, 1080);
        GUILayout.BeginArea(new Rect(margin, margin, width, Screen.height - margin * 2));
        GUILayout.Label("OOGB Two Client Test Harness", HeaderStyle());
        GUILayout.Space(8);

        DrawModeControls();
        GUILayout.Space(8);

        if (mode == HarnessMode.InMemorySimulation)
            DrawInMemoryHarness();
        else
            DrawUdpHarness();

        GUILayout.Space(12);
        DrawLog();
        GUILayout.EndArea();
    }

    private void DrawModeControls()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Mode", SubHeaderStyle());
        GUILayout.BeginHorizontal();

        if (GUILayout.Toggle(mode == HarnessMode.InMemorySimulation, "In-Memory Simulation", GUI.skin.button))
            SetMode(HarnessMode.InMemorySimulation);

        if (GUILayout.Toggle(mode == HarnessMode.UdpLoopback, "UDP Loopback", GUI.skin.button))
            SetMode(HarnessMode.UdpLoopback);

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawInMemoryHarness()
    {
        GUILayout.BeginHorizontal();
        DrawClientPanel("Client A / Session Owner", ownerSession);
        GUILayout.Space(12);
        DrawClientPanel("Client B", clientSession);
        GUILayout.EndHorizontal();

        GUILayout.Space(12);
        DrawInMemoryControls();
    }

    private void DrawUdpHarness()
    {
        GUILayout.BeginHorizontal();
        DrawUdpSetupPanel();
        GUILayout.Space(12);
        DrawClientPanel("Local UDP Session", localUdpSession);
        GUILayout.EndHorizontal();

        GUILayout.Space(12);
        DrawUdpControls();
    }

    private void DrawUdpSetupPanel()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(520));
        GUILayout.Label("UDP Loopback Setup", SubHeaderStyle());
        GUILayout.Label("Run one process as Client A and another as Client B.");
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Role", GUILayout.Width(70));
        if (GUILayout.Toggle(udpRole == UdpRole.ClientAOwner, "Client A / Owner", GUI.skin.button))
            udpRole = UdpRole.ClientAOwner;

        if (GUILayout.Toggle(udpRole == UdpRole.ClientBPeer, "Client B", GUI.skin.button))
            udpRole = UdpRole.ClientBPeer;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Client A Port", GUILayout.Width(100));
        ownerPortText = GUILayout.TextField(ownerPortText, GUILayout.Width(80));
        GUILayout.Label("Client B Port", GUILayout.Width(100));
        clientPortText = GUILayout.TextField(clientPortText, GUILayout.Width(80));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start UDP Role"))
            StartUdpRole();

        if (GUILayout.Button("Stop UDP Role"))
            StopUdpRole();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawClientPanel(string title, OogbP2PSession session)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(520));
        GUILayout.Label(title, SubHeaderStyle());

        if (session == null)
        {
            GUILayout.Label("Not started");
            GUILayout.EndVertical();
            return;
        }

        GUILayout.Label("Peer: " + session.LocalPeerId);
        GUILayout.Label("State: " + session.State);
        GUILayout.Label("Tick: " + session.CurrentTick);
        GUILayout.Label("End Reason: " + session.EndReason);
        GUILayout.Label("Peers: " + string.Join(", ", session.Peers));
        GUILayout.EndVertical();
    }

    private void DrawInMemoryControls()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("In-Memory Session Controls", SubHeaderStyle());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
            ResetInMemoryHarness();

        if (GUILayout.Button("Ready + Start"))
            InMemoryReadyAndStart();

        if (GUILayout.Button("Poll"))
            PollSessions();
        GUILayout.EndHorizontal();

        DrawSharedTestControls(
            "Submit Both Inputs + Advance Tick",
            SubmitBothInMemoryInputsAndAdvance,
            PublishInMemoryMatchingHash,
            PublishInMemoryMismatchedHash,
            () => DisconnectInMemory(OwnerPeerId),
            () => DisconnectInMemory(ClientPeerId));
        GUILayout.EndVertical();
    }

    private void DrawUdpControls()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("UDP Session Controls", SubHeaderStyle());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Ready"))
            UdpSetReady();

        if (GUILayout.Button("Owner Start"))
            UdpOwnerStart();

        if (GUILayout.Button("Poll"))
            PollSessions();
        GUILayout.EndHorizontal();

        DrawSharedTestControls(
            "Submit Local Input + Advance Tick",
            SubmitUdpInputAndAdvance,
            PublishUdpMatchingHash,
            PublishUdpMismatchedHash,
            StopUdpRole,
            StopUdpRole);
        GUILayout.EndVertical();
    }

    private void DrawSharedTestControls(
        string inputButtonLabel,
        Action submitInput,
        Action publishMatchingHash,
        Action publishMismatchedHash,
        Action disconnectClientA,
        Action disconnectClientB)
    {
        GUILayout.Space(8);
        GUILayout.Label("Input Tick Test");
        GUILayout.BeginHorizontal();
        inputValueA = DrawIntField("Client A Input", inputValueA);
        inputValueB = DrawIntField("Client B Input", inputValueB);

        if (GUILayout.Button(inputButtonLabel))
            submitInput();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("State Hash Test");
        GUILayout.BeginHorizontal();
        matchingHash = DrawUlongField("Matching Hash", matchingHash);

        if (GUILayout.Button("Publish Matching Hash"))
            publishMatchingHash();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        mismatchHashA = DrawUlongField("Hash A", mismatchHashA);
        mismatchHashB = DrawUlongField("Hash B", mismatchHashB);

        if (GUILayout.Button("Publish Mismatched Hash"))
            publishMismatchedHash();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Disconnect Test");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Disconnect Client A"))
            disconnectClientA();

        if (GUILayout.Button("Disconnect Client B"))
            disconnectClientB();
        GUILayout.EndHorizontal();
    }

    private int DrawIntField(string label, int value)
    {
        GUILayout.Label(label, GUILayout.Width(100));
        var text = GUILayout.TextField(value.ToString(), GUILayout.Width(60));
        return int.TryParse(text, out var parsed) ? parsed : value;
    }

    private ulong DrawUlongField(string label, ulong value)
    {
        GUILayout.Label(label, GUILayout.Width(100));
        var text = GUILayout.TextField(value.ToString(), GUILayout.Width(80));
        return ulong.TryParse(text, out var parsed) ? parsed : value;
    }

    private void DrawLog()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Event Log", SubHeaderStyle());
        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(220));

        foreach (var line in logLines)
            GUILayout.Label(line);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void SetMode(HarnessMode nextMode)
    {
        if (mode == nextMode)
            return;

        mode = nextMode;
        DisposeUdpTransport();

        if (mode == HarnessMode.InMemorySimulation)
            ResetInMemoryHarness();

        AddLog("Mode set to " + mode);
    }

    private void ResetInMemoryHarness()
    {
        DisposeUdpTransport();
        mode = HarnessMode.InMemorySimulation;
        network = new OogbInMemoryNetwork();
        ownerSession = new OogbP2PSession(network.CreatePeer(OwnerPeerId), ProtocolVersion);
        clientSession = new OogbP2PSession(network.CreatePeer(ClientPeerId), ProtocolVersion);
        ownerSession.Diagnostic += message => AddLog("A: " + message);
        clientSession.Diagnostic += message => AddLog("B: " + message);

        ownerSession.CreateOwned(SessionId);
        ownerSession.AddPeer(ClientPeerId);
        clientSession.JoinKnownSession(SessionId, OwnerPeerId, new[] { OwnerPeerId, ClientPeerId });
        AddLog("In-memory harness reset");
    }

    private void StartUdpRole()
    {
        if (!TryParsePorts(out var ownerPort, out var clientPort))
            return;

        DisposeUdpTransport();
        mode = HarnessMode.UdpLoopback;

        var localPeerId = udpRole == UdpRole.ClientAOwner ? OwnerPeerId : ClientPeerId;
        var remotePeerId = udpRole == UdpRole.ClientAOwner ? ClientPeerId : OwnerPeerId;
        var localPort = udpRole == UdpRole.ClientAOwner ? ownerPort : clientPort;
        var remotePort = udpRole == UdpRole.ClientAOwner ? clientPort : ownerPort;

        try
        {
            udpTransport = new OogbUdpLoopbackTransportPeer(
                localPeerId,
                localPort,
                new Dictionary<OogbPeerId, int> { { remotePeerId, remotePort } });
        }
        catch (Exception ex)
        {
            AddLog("Failed to start UDP role: " + ex.Message);
            return;
        }

        localUdpSession = new OogbP2PSession(udpTransport, ProtocolVersion);
        localUdpSession.Diagnostic += message => AddLog(localPeerId + ": " + message);

        if (udpRole == UdpRole.ClientAOwner)
        {
            localUdpSession.CreateOwned(SessionId);
            localUdpSession.AddPeer(ClientPeerId);
        }
        else
        {
            localUdpSession.JoinKnownSession(SessionId, OwnerPeerId, new[] { OwnerPeerId, ClientPeerId });
        }

        AddLog("Started UDP " + localPeerId + " on 127.0.0.1:" + localPort + " -> " + remotePort);
    }

    private void StopUdpRole()
    {
        DisposeUdpTransport();
        AddLog("Stopped UDP role");
    }

    private void InMemoryReadyAndStart()
    {
        ownerSession.SetLocalReady();
        clientSession.SetLocalReady();
        PollSessions();

        var started = ownerSession.TryStart();
        AddLog("Owner TryStart: " + started);
        PollSessions();
    }

    private void UdpSetReady()
    {
        if (!HasUdpSession())
            return;

        localUdpSession.SetLocalReady();
        AddLog(localUdpSession.LocalPeerId + " ready sent");
    }

    private void UdpOwnerStart()
    {
        if (!HasUdpSession())
            return;

        if (localUdpSession.LocalPeerId != OwnerPeerId)
        {
            AddLog("Only Client A can start the session");
            return;
        }

        var started = localUdpSession.TryStart();
        AddLog("Client A TryStart: " + started);
    }

    private void SubmitBothInMemoryInputsAndAdvance()
    {
        if (!IsInMemoryPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.SubmitLocalInput(tick, new[] { (byte)Mathf.Clamp(inputValueA, 0, 255) });
        clientSession.SubmitLocalInput(tick, new[] { (byte)Mathf.Clamp(inputValueB, 0, 255) });

        var ownerAdvanced = ownerSession.TryAdvanceTick();
        var clientAdvanced = clientSession.TryAdvanceTick();
        AddLog("Advance tick " + tick + ": A=" + ownerAdvanced + ", B=" + clientAdvanced);
    }

    private void SubmitUdpInputAndAdvance()
    {
        if (!IsUdpPlaying())
            return;

        var tick = localUdpSession.CurrentTick;
        var value = localUdpSession.LocalPeerId == OwnerPeerId ? inputValueA : inputValueB;
        localUdpSession.SubmitLocalInput(tick, new[] { (byte)Mathf.Clamp(value, 0, 255) });
        var advanced = localUdpSession.TryAdvanceTick();
        AddLog(localUdpSession.LocalPeerId + " advance tick " + tick + ": " + advanced);
    }

    private void PublishInMemoryMatchingHash()
    {
        if (!IsInMemoryPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.PublishStateHash(tick, matchingHash);
        clientSession.PublishStateHash(tick, matchingHash);
        PollSessions();
        AddLog("Published matching hash " + matchingHash + " for tick " + tick);
    }

    private void PublishUdpMatchingHash()
    {
        if (!IsUdpPlaying())
            return;

        var tick = localUdpSession.CurrentTick;
        localUdpSession.PublishStateHash(tick, matchingHash);
        AddLog(localUdpSession.LocalPeerId + " published matching hash " + matchingHash + " for tick " + tick);
    }

    private void PublishInMemoryMismatchedHash()
    {
        if (!IsInMemoryPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.PublishStateHash(tick, mismatchHashA);
        clientSession.PublishStateHash(tick, mismatchHashB);
        PollSessions();
        AddLog("Published mismatched hashes for tick " + tick);
    }

    private void PublishUdpMismatchedHash()
    {
        if (!IsUdpPlaying())
            return;

        var tick = localUdpSession.CurrentTick;
        var hash = localUdpSession.LocalPeerId == OwnerPeerId ? mismatchHashA : mismatchHashB;
        localUdpSession.PublishStateHash(tick, hash);
        AddLog(localUdpSession.LocalPeerId + " published mismatch hash " + hash + " for tick " + tick);
    }

    private void DisconnectInMemory(OogbPeerId peerId)
    {
        network.Disconnect(peerId);
        PollSessions();
        AddLog("Disconnected " + peerId);
    }

    private bool IsInMemoryPlaying()
    {
        if (ownerSession.State == OogbSessionState.Playing && clientSession.State == OogbSessionState.Playing)
            return true;

        AddLog("In-memory session must be playing first");
        return false;
    }

    private bool IsUdpPlaying()
    {
        if (!HasUdpSession())
            return false;

        if (localUdpSession.State == OogbSessionState.Playing)
            return true;

        AddLog("UDP session must be playing first");
        return false;
    }

    private bool HasUdpSession()
    {
        if (localUdpSession != null)
            return true;

        AddLog("Start a UDP role first");
        return false;
    }

    private bool TryParsePorts(out int ownerPort, out int clientPort)
    {
        if (!int.TryParse(ownerPortText, out ownerPort) || ownerPort <= 0 || ownerPort > 65535)
        {
            AddLog("Invalid Client A port");
            clientPort = 0;
            return false;
        }

        if (!int.TryParse(clientPortText, out clientPort) || clientPort <= 0 || clientPort > 65535)
        {
            AddLog("Invalid Client B port");
            return false;
        }

        if (ownerPort == clientPort)
        {
            AddLog("Client ports must be different");
            return false;
        }

        return true;
    }

    private void PollSessions()
    {
        if (mode == HarnessMode.InMemorySimulation)
        {
            ownerSession?.Poll();
            clientSession?.Poll();
        }
        else
        {
            localUdpSession?.Poll();
            TryAutoAdvanceUdpTick();
        }
    }

    private void TryAutoAdvanceUdpTick()
    {
        if (localUdpSession == null || localUdpSession.State != OogbSessionState.Playing)
            return;

        var tick = localUdpSession.CurrentTick;

        if (localUdpSession.TryAdvanceTick())
            AddLog(localUdpSession.LocalPeerId + " auto-advanced tick " + tick);
    }

    private void DisposeUdpTransport()
    {
        if (udpTransport != null)
        {
            udpTransport.Dispose();
            udpTransport = null;
        }

        localUdpSession = null;
    }

    private void AddLog(string message)
    {
        logLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);

        if (logLines.Count > 120)
            logLines.RemoveAt(0);
    }

    private static GUIStyle HeaderStyle()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        return style;
    }

    private static GUIStyle SubHeaderStyle()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        return style;
    }
}
