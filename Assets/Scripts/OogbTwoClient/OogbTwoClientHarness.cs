using System;
using System.Collections.Generic;
using OOGB;
using UnityEngine;

public sealed class OogbTwoClientHarness : MonoBehaviour
{
    private static readonly OogbProtocolVersion ProtocolVersion = new OogbProtocolVersion(1, 0, 0);
    private static readonly OogbSessionId SessionId = new OogbSessionId("local-two-client-test");
    private static readonly OogbPeerId OwnerPeerId = new OogbPeerId("Client-A");
    private static readonly OogbPeerId ClientPeerId = new OogbPeerId("Client-B");

    private readonly List<string> logLines = new List<string>();

    private OogbInMemoryNetwork network;
    private OogbP2PSession ownerSession;
    private OogbP2PSession clientSession;
    private Vector2 logScroll;
    private int inputValueA = 1;
    private int inputValueB = 2;
    private ulong matchingHash = 100;
    private ulong mismatchHashA = 100;
    private ulong mismatchHashB = 999;

    private void Awake()
    {
        ResetHarness();
    }

    private void Update()
    {
        PollSessions();
    }

    private void OnGUI()
    {
        const int margin = 16;
        var width = Mathf.Min(Screen.width - margin * 2, 960);
        GUILayout.BeginArea(new Rect(margin, margin, width, Screen.height - margin * 2));
        GUILayout.Label("OOGB Two Client Test Harness", HeaderStyle());
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        DrawClientPanel("Client A / Session Owner", ownerSession);
        GUILayout.Space(12);
        DrawClientPanel("Client B", clientSession);
        GUILayout.EndHorizontal();

        GUILayout.Space(12);
        DrawControls();
        GUILayout.Space(12);
        DrawLog();
        GUILayout.EndArea();
    }

    private void DrawClientPanel(string title, OogbP2PSession session)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(460));
        GUILayout.Label(title, SubHeaderStyle());
        GUILayout.Label("Peer: " + session.LocalPeerId);
        GUILayout.Label("State: " + session.State);
        GUILayout.Label("Tick: " + session.CurrentTick);
        GUILayout.Label("End Reason: " + session.EndReason);
        GUILayout.Label("Peers: " + string.Join(", ", session.Peers));
        GUILayout.EndVertical();
    }

    private void DrawControls()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Session Controls", SubHeaderStyle());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
            ResetHarness();

        if (GUILayout.Button("Ready + Start"))
            ReadyAndStart();

        if (GUILayout.Button("Poll"))
            PollSessions();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Input Tick Test");
        GUILayout.BeginHorizontal();
        inputValueA = DrawIntField("Client A Input", inputValueA);
        inputValueB = DrawIntField("Client B Input", inputValueB);

        if (GUILayout.Button("Submit Both Inputs + Advance Tick"))
            SubmitBothInputsAndAdvance();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("State Hash Test");
        GUILayout.BeginHorizontal();
        matchingHash = DrawUlongField("Matching Hash", matchingHash);

        if (GUILayout.Button("Publish Matching Hash"))
            PublishMatchingHash();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        mismatchHashA = DrawUlongField("Hash A", mismatchHashA);
        mismatchHashB = DrawUlongField("Hash B", mismatchHashB);

        if (GUILayout.Button("Publish Mismatched Hash"))
            PublishMismatchedHash();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Disconnect Test");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Disconnect Client A"))
            Disconnect(OwnerPeerId);

        if (GUILayout.Button("Disconnect Client B"))
            Disconnect(ClientPeerId);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private int DrawIntField(string label, int value)
    {
        GUILayout.Label(label, GUILayout.Width(90));
        var text = GUILayout.TextField(value.ToString(), GUILayout.Width(60));
        return int.TryParse(text, out var parsed) ? parsed : value;
    }

    private ulong DrawUlongField(string label, ulong value)
    {
        GUILayout.Label(label, GUILayout.Width(90));
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

    private void ResetHarness()
    {
        network = new OogbInMemoryNetwork();
        ownerSession = new OogbP2PSession(network.CreatePeer(OwnerPeerId), ProtocolVersion);
        clientSession = new OogbP2PSession(network.CreatePeer(ClientPeerId), ProtocolVersion);
        ownerSession.Diagnostic += message => AddLog("A: " + message);
        clientSession.Diagnostic += message => AddLog("B: " + message);

        ownerSession.CreateOwned(SessionId);
        ownerSession.AddPeer(ClientPeerId);
        clientSession.JoinKnownSession(SessionId, OwnerPeerId, new[] { OwnerPeerId, ClientPeerId });
        AddLog("Harness reset");
    }

    private void ReadyAndStart()
    {
        ownerSession.SetLocalReady();
        clientSession.SetLocalReady();
        PollSessions();

        var started = ownerSession.TryStart();
        AddLog("Owner TryStart: " + started);
        PollSessions();
    }

    private void SubmitBothInputsAndAdvance()
    {
        if (!IsPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.SubmitLocalInput(tick, new[] { (byte)Mathf.Clamp(inputValueA, 0, 255) });
        clientSession.SubmitLocalInput(tick, new[] { (byte)Mathf.Clamp(inputValueB, 0, 255) });

        var ownerAdvanced = ownerSession.TryAdvanceTick();
        var clientAdvanced = clientSession.TryAdvanceTick();
        AddLog("Advance tick " + tick + ": A=" + ownerAdvanced + ", B=" + clientAdvanced);
    }

    private void PublishMatchingHash()
    {
        if (!IsPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.PublishStateHash(tick, matchingHash);
        clientSession.PublishStateHash(tick, matchingHash);
        PollSessions();
        AddLog("Published matching hash " + matchingHash + " for tick " + tick);
    }

    private void PublishMismatchedHash()
    {
        if (!IsPlaying())
            return;

        var tick = ownerSession.CurrentTick;
        ownerSession.PublishStateHash(tick, mismatchHashA);
        clientSession.PublishStateHash(tick, mismatchHashB);
        PollSessions();
        AddLog("Published mismatched hashes for tick " + tick);
    }

    private void Disconnect(OogbPeerId peerId)
    {
        network.Disconnect(peerId);
        PollSessions();
        AddLog("Disconnected " + peerId);
    }

    private bool IsPlaying()
    {
        if (ownerSession.State == OogbSessionState.Playing && clientSession.State == OogbSessionState.Playing)
            return true;

        AddLog("Session must be playing first");
        return false;
    }

    private void PollSessions()
    {
        ownerSession?.Poll();
        clientSession?.Poll();
    }

    private void AddLog(string message)
    {
        logLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);

        if (logLines.Count > 80)
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
