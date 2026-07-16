using System;
using System.Collections.Generic;
using OOGB;
using Platformer.Mechanics;
using Platformer.Model;
using Platformer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Platformer.Multiplayer
{
    public sealed class OogbPlatformerMultiplayerController : MonoBehaviour
    {
        private enum NetworkRole
        {
            Owner,
            Client,
            Ghost
        }

        private enum RacePhase
        {
            WaitingRoom,
            Playing,
            Finished
        }

        private struct GhostFrame
        {
            public int Tick;
            public float Horizontal;
            public bool JumpPressed;
            public bool JumpReleased;
            public bool Finished;
            [NonSerialized]
            public bool HasSnapshot;
            [NonSerialized]
            public Vector2 Position;
            [NonSerialized]
            public Vector2 Velocity;
            [NonSerialized]
            public int JumpState;
            [NonSerialized]
            public bool ControlEnabled;
            [NonSerialized]
            public bool FacingLeft;
        }

        private static readonly Dictionary<string, List<GhostFrame>> SavedGhosts = new Dictionary<string, List<GhostFrame>>();
        private static readonly OogbProtocolVersion ProtocolVersion = new OogbProtocolVersion(1, 0, 0);
        private static readonly OogbSessionId SessionId = new OogbSessionId("platformer-race");
        private static readonly OogbPeerId OwnerPeerId = new OogbPeerId("Player-A");
        private static readonly OogbPeerId ClientPeerId = new OogbPeerId("Player-B");

        private readonly List<string> logLines = new List<string>();
        private readonly HashSet<int> submittedTicks = new HashSet<int>();
        private readonly List<GhostFrame> ghostRecording = new List<GhostFrame>();
        private readonly List<GhostFrame> ghostReplay = new List<GhostFrame>();

        private PlatformerModel model;
        private PlayerController localPlayer;
        private PlayerController remotePlayer;
        private OogbUdpLoopbackTransportPeer transport;
        private OogbP2PSession session;
        private InputAction moveAction;
        private InputAction jumpAction;
        private NetworkRole role = NetworkRole.Owner;
        private RacePhase phase = RacePhase.WaitingRoom;
        private Vector2 logScroll;
        private Vector3 playerBaseScale = Vector3.one;
        private string ownerPortText = "7777";
        private string clientPortText = "7778";
        private string resultText = "";
        private string ghostName = "default";
        private string dialogTitleTimestamp;
        private bool localFinishRequested;
        private bool recordLocalGhost;
        private bool ghostRecordingSaved;
        private bool ownerStartRequested;
        private bool soloReplayGhost;
        private bool hasPlayerBaseScale;
        private float nextRaceTickTime;
        private float nextGhostRecordTime;
        private float nextSoloReplayTime;
        private int soloReplayTick;
        private int ghostReplayTick;
        private int? localFinishTick;
        private int? remoteFinishTick;

        public static OogbPlatformerMultiplayerController Active { get; private set; }

        public static bool TryHandleVictory(PlayerController player)
        {
            if (Active == null)
                return false;

            return Active.HandleVictory(player);
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Active = this;
            model = Simulation.GetModel<PlatformerModel>();
            CachePlayerBaseScale(model.player);
            moveAction = InputSystem.actions.FindAction("Player/Move");
            jumpAction = InputSystem.actions.FindAction("Player/Jump");
            dialogTitleTimestamp = DateTime.Now.ToString("yyMMdd-HHmmss");
            moveAction?.Enable();
            jumpAction?.Enable();
            AddLog("Multiplayer race controller ready");
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;

            DisposeTransport();
        }

        private void Update()
        {
            TickSoloGhostReplayAtFixedRate();

            if (session == null)
            {
                RecordLocalGhostOutsideRaceAtFixedRate();
                return;
            }

            session.Poll();

            if (phase == RacePhase.WaitingRoom && session.State == OogbSessionState.Playing)
                BeginRace();

            if (phase == RacePhase.Playing)
                TickRaceAtFixedRate();
            else if (ownerStartRequested)
                TryOwnerStartNow(false);

            RecordLocalGhostOutsideRaceAtFixedRate();
        }

        private void OnGUI()
        {
            const int margin = 16;
            var width = Mathf.Min(Screen.width - margin * 2, 720);

            GUILayout.BeginArea(new Rect(margin, margin, width, Screen.height - margin * 2));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("OOGB Platformer Race " + dialogTitleTimestamp, HeaderStyle());
            GUILayout.Label("Phase: " + phase);
            GUILayout.Label("Role: " + role);
            GUILayout.Label("Session: " + (session == null ? "Not connected" : session.State.ToString()));
            DrawGhostRecorderControls();

            if (session != null)
            {
                GUILayout.Label("Tick: " + session.CurrentTick);
                GUILayout.Label("Peers: " + string.Join(", ", session.Peers));
            }
            if (phase == RacePhase.WaitingRoom)
                DrawWaitingRoom();
            else
                DrawRaceStatus();

            GUILayout.Space(8);
            DrawLog();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawWaitingRoom()
        {
            GUILayout.Space(8);
            GUILayout.Label("Waiting Room", SubHeaderStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(role == NetworkRole.Owner, "Owner", GUI.skin.button))
                SetRole(NetworkRole.Owner);

            if (GUILayout.Toggle(role == NetworkRole.Client, "Client", GUI.skin.button))
                SetRole(NetworkRole.Client);

            if (GUILayout.Toggle(role == NetworkRole.Ghost, "Ghost", GUI.skin.button))
                SetRole(NetworkRole.Ghost);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Owner Port", GUILayout.Width(90));
            ownerPortText = GUILayout.TextField(ownerPortText, GUILayout.Width(80));
            GUILayout.Label("Client Port", GUILayout.Width(90));
            clientPortText = GUILayout.TextField(clientPortText, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(role == NetworkRole.Owner ? "Create Room" : "Join Room"))
                StartSession();

            if (GUILayout.Button("Ready"))
                SetReady();

            if (GUILayout.Button("Owner Start"))
                OwnerStart();

            if (GUILayout.Button("Stop"))
                StopSession();
            GUILayout.EndHorizontal();
        }

        private void DrawGhostRecorderControls()
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ghost File", GUILayout.Width(70));
            ghostName = GUILayout.TextField(ghostName, GUILayout.Width(140));

            GUILayout.BeginVertical();
            GUI.enabled = role != NetworkRole.Ghost;
            if (recordLocalGhost)
            {
                if (GUILayout.Button("Stop and Save"))
                    StopAndSaveLocalGhostRecording();
            }
            else
            {
                if (GUILayout.Button("Record Local Ghost"))
                    StartLocalGhostRecording();
            }
            GUI.enabled = true;

            if (role == NetworkRole.Ghost && recordLocalGhost)
                StopLocalGhostRecordingForGhostClient();

            GUILayout.Label(role == NetworkRole.Ghost ? "Ghost client replays only" : "Records this local player");
            GUILayout.Label("Recording: " + ghostRecording.Count + " Replay: " + ghostReplay.Count + " Solo: " + (soloReplayGhost ? soloReplayTick.ToString() : "-"));
            GUILayout.EndVertical();

            GUILayout.Label("Saved: " + GetSavedGhostCount(), GUILayout.Width(90));

            if (soloReplayGhost)
            {
                if (GUILayout.Button("Stop Solo Replay", GUILayout.Width(120)))
                    StopSoloGhostReplay();
            }
            else if (GUILayout.Button("Solo Replay", GUILayout.Width(95)))
            {
                StartSoloGhostReplay();
            }

            if (GUILayout.Button("Clear Ghost", GUILayout.Width(95)))
                ClearGhost();

            GUILayout.EndHorizontal();
        }

        private void SetRole(NetworkRole nextRole)
        {
            if (role == nextRole)
                return;

            role = nextRole;

            if (role == NetworkRole.Ghost)
            {
                recordLocalGhost = false;
                ghostRecording.Clear();
                ghostRecordingSaved = false;
                LoadGhost();
            }
        }

        private void StartLocalGhostRecording()
        {
            StopSoloGhostReplay();

            if (session == null && phase != RacePhase.Playing)
                SetupSoloGhostRecorderPlayer();

            ghostRecording.Clear();
            ghostRecordingSaved = false;
            nextGhostRecordTime = Time.time;
            recordLocalGhost = true;
            AddLog("Local ghost recording enabled");
        }

        private void StopAndSaveLocalGhostRecording()
        {
            SaveRecordedGhostIfNeeded();
            recordLocalGhost = false;
            if (session == null)
                RestoreSoloGhostRecorderPlayerControl();
            AddLog("Local ghost recording disabled");
        }

        private void StopLocalGhostRecordingForGhostClient()
        {
            recordLocalGhost = false;
            ghostRecording.Clear();
            ghostRecordingSaved = false;
            AddLog("Local ghost recording disabled for ghost client");
        }

        private void StartSoloGhostReplay()
        {
            if (session != null)
            {
                AddLog("Stop session before solo replay");
                return;
            }

            recordLocalGhost = false;
            LoadGhost();

            if (ghostReplay.Count == 0)
            {
                AddLog("No ghost frames to solo replay");
                return;
            }

            SetupSoloGhostRecorderPlayer();
            soloReplayTick = 0;
            nextSoloReplayTime = Time.time;
            soloReplayGhost = true;
            AddLog("Solo replay started: " + ghostReplay.Count);
        }

        private void StopSoloGhostReplay()
        {
            if (!soloReplayGhost)
                return;

            soloReplayGhost = false;
            soloReplayTick = 0;
            RestoreSoloGhostRecorderPlayerControl();
            AddLog("Solo replay stopped");
        }

        private void DrawRaceStatus()
        {
            GUILayout.Space(8);
            GUILayout.Label("Race", SubHeaderStyle());
            GUILayout.Label("Local Finish Tick: " + FormatTick(localFinishTick));
            GUILayout.Label("Remote Finish Tick: " + FormatTick(remoteFinishTick));

            if (!string.IsNullOrEmpty(resultText))
                GUILayout.Label("Result: " + resultText);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Local Finish"))
                RequestLocalFinish();

            if (GUILayout.Button("Remote Finish"))
                RequestRemoteFinish();

            if (GUILayout.Button("Draw Finish"))
                RequestDrawFinish();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back To Room"))
                StopSession();

            if (GUILayout.Button("Restart Local Scene"))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            GUILayout.EndHorizontal();
        }

        private void StartSession()
        {
            if (!TryParsePorts(out var ownerPort, out var clientPort))
                return;

            DisposeTransport();
            submittedTicks.Clear();
            ghostRecording.Clear();
            ghostReplay.Clear();
            ghostRecordingSaved = false;
            ownerStartRequested = false;
            nextRaceTickTime = Time.time;
            nextGhostRecordTime = Time.time;
            ghostReplayTick = 0;
            resultText = "";
            localFinishRequested = false;
            localFinishTick = null;
            remoteFinishTick = null;
            phase = RacePhase.WaitingRoom;

            var isOwner = role == NetworkRole.Owner;
            var localPeerId = isOwner ? OwnerPeerId : ClientPeerId;
            var remotePeerId = isOwner ? ClientPeerId : OwnerPeerId;
            var localPort = isOwner ? ownerPort : clientPort;
            var remotePort = isOwner ? clientPort : ownerPort;

            try
            {
                transport = new OogbUdpLoopbackTransportPeer(
                    localPeerId,
                    localPort,
                    new Dictionary<OogbPeerId, int> { { remotePeerId, remotePort } });
            }
            catch (Exception ex)
            {
                AddLog("Failed to start UDP: " + ex.Message);
                return;
            }

            session = new OogbP2PSession(transport, ProtocolVersion);
            session.Diagnostic += AddLog;

            if (isOwner)
            {
                session.CreateOwned(SessionId);
                session.AddPeer(ClientPeerId);
            }
            else
            {
                session.JoinKnownSession(SessionId, OwnerPeerId, new[] { OwnerPeerId, ClientPeerId });
            }

            if (role == NetworkRole.Ghost)
            {
                LoadGhost();
                session.SetLocalReady();
                AddLog("Ghost client ready");
            }

            AddLog("Started " + localPeerId + " on 127.0.0.1:" + localPort + " -> " + remotePort);
        }

        private void SetReady()
        {
            if (session == null)
            {
                AddLog("Create or join a room first");
                return;
            }

            ResetPlayersForRoomStart();
            session.SetLocalReady();
            AddLog("Ready sent");
        }

        private void OwnerStart()
        {
            if (session == null)
            {
                AddLog("Create a room first");
                return;
            }

            if (role != NetworkRole.Owner)
            {
                AddLog("Only owner can start");
                return;
            }

            ownerStartRequested = true;
            TryOwnerStartNow(true);
        }

        private void TryOwnerStartNow(bool logFailure)
        {
            if (session == null || role != NetworkRole.Owner || session.State == OogbSessionState.Playing)
                return;

            session.Poll();
            var started = session.TryStart();

            if (!started)
            {
                if (logFailure)
                    AddLog("Owner start: False");
                return;
            }

            AddLog("Owner start: True");
            ownerStartRequested = false;
            BeginRace();
        }

        private void StopSession()
        {
            SaveRecordedGhostIfNeeded();
            DisposeTransport();
            phase = RacePhase.WaitingRoom;
            resultText = "";
            submittedTicks.Clear();
            ghostRecording.Clear();
            ghostReplay.Clear();
            ghostRecordingSaved = false;
            ownerStartRequested = false;
            nextRaceTickTime = Time.time;
            nextGhostRecordTime = Time.time;
            ghostReplayTick = 0;
            localFinishRequested = false;
            recordLocalGhost = false;
            StopSoloGhostReplay();
            ResetPlayersForRoomStart();
            ConfigurePlayerControl(false);
            AddLog("Stopped session");
        }

        private void TickSoloGhostReplayAtFixedRate()
        {
            if (!soloReplayGhost)
                return;

            if (Time.time < nextSoloReplayTime)
                return;

            nextSoloReplayTime += Time.fixedDeltaTime;
            TickSoloGhostReplay();
        }

        private void TickSoloGhostReplay()
        {
            if (soloReplayTick < 0 || soloReplayTick >= ghostReplay.Count)
            {
                AddLog("Solo replay finished");
                StopSoloGhostReplay();
                return;
            }

            if (localPlayer == null)
                SetupSoloGhostRecorderPlayer();

            var input = ToRaceInput(ghostReplay[soloReplayTick]);
            ApplyRaceInput(localPlayer, input);
            soloReplayTick++;
        }

        private void TickRaceAtFixedRate()
        {
            if (Time.time < nextRaceTickTime)
                return;

            nextRaceTickTime += Time.fixedDeltaTime;
            TickRace();
        }

        private void TickRace()
        {
            if (session.State == OogbSessionState.Finished)
            {
                FinishRace("Session ended: " + session.EndReason);
                return;
            }

            if (session.State == OogbSessionState.Playing && localPlayer == null)
                BeginRace();

            if (session.State != OogbSessionState.Playing)
                return;

            var tick = session.CurrentTick;

            if (!submittedTicks.Contains(tick.Value))
            {
                var localInput = role == NetworkRole.Ghost ? GetNextGhostInput() : CaptureLocalInput(tick.Value);
                session.SubmitLocalInput(tick, localInput.Encode());
                submittedTicks.Add(tick.Value);

                if (role == NetworkRole.Ghost)
                    ApplyRaceInput(localPlayer, localInput);
            }

            var localPeerId = role == NetworkRole.Owner ? OwnerPeerId : ClientPeerId;
            var remotePeerId = role == NetworkRole.Owner ? ClientPeerId : OwnerPeerId;

            if (!session.TryGetInput(tick, localPeerId, out var localPayload) ||
                !session.TryGetInput(tick, remotePeerId, out var remotePayload))
                return;

            if (!OogbPlatformerRaceInput.TryDecode(localPayload, out var decodedLocal) ||
                !OogbPlatformerRaceInput.TryDecode(remotePayload, out var decodedRemote))
            {
                FinishRace("Invalid input payload");
                return;
            }

            ApplyRaceInput(localPlayer, decodedLocal);
            ApplyRaceInput(remotePlayer, decodedRemote);
            RecordFinishFlags(tick, decodedLocal, decodedRemote);
            session.TryAdvanceTick();
            ResolveWinnerIfPossible(session.CurrentTick.Value);
        }

        private OogbPlatformerRaceInput CaptureLocalInput(int tick)
        {
            var horizontal = moveAction == null ? 0f : moveAction.ReadValue<Vector2>().x;
            var jumpPressed = jumpAction != null && jumpAction.WasPressedThisFrame();
            var jumpReleased = jumpAction != null && jumpAction.WasReleasedThisFrame();
            var finished = localFinishRequested;
            localFinishRequested = false;
            var input = new OogbPlatformerRaceInput(horizontal, jumpPressed, jumpReleased, finished);
            RecordLocalGhostFrame(tick, input);
            return input;
        }

        private void RecordLocalGhostOutsideRaceAtFixedRate()
        {
            if (!recordLocalGhost || role == NetworkRole.Ghost || phase == RacePhase.Playing)
                return;

            if (Time.time < nextGhostRecordTime)
                return;

            nextGhostRecordTime += Time.fixedDeltaTime;
            RecordLocalGhostOutsideRace();
        }

        private void RecordLocalGhostOutsideRace()
        {
            if (!recordLocalGhost || role == NetworkRole.Ghost || phase == RacePhase.Playing)
                return;

            var input = CaptureLocalInput(ghostRecording.Count);
            ApplyInput(localPlayer, input);
        }

        private OogbPlatformerRaceInput GetGhostInput(int tick)
        {
            if (tick < 0 || tick >= ghostReplay.Count)
                return new OogbPlatformerRaceInput(0f, false, false, false);

            var frame = ghostReplay[tick];
            return ToRaceInput(frame);
        }

        private OogbPlatformerRaceInput GetNextGhostInput()
        {
            if (ghostReplayTick < 0 || ghostReplayTick >= ghostReplay.Count)
                return new OogbPlatformerRaceInput(0f, false, false, false);

            var frame = ghostReplay[ghostReplayTick];
            ghostReplayTick++;
            return ToRaceInput(frame);
        }

        private void ApplyInput(PlayerController player, OogbPlatformerRaceInput input)
        {
            if (player == null)
                return;

            player.SetSnapshotPlayback(false);
            player.SetExternalInput(input.Horizontal, input.JumpPressed, input.JumpReleased);
        }

        private void ApplyRaceInput(PlayerController player, OogbPlatformerRaceInput input)
        {
            if (player == null)
                return;

            if (!input.HasSnapshot)
            {
                ApplyInput(player, input);
                return;
            }

            ApplySnapshot(player, input.Position, input.Velocity, input.JumpState, input.ControlEnabled, input.FacingLeft);
        }

        private void ApplySnapshot(PlayerController player, Vector2 position, Vector2 velocity, int jumpState, bool controlEnabled, bool facingLeft)
        {
            player.ApplySnapshotPlaybackFrame(position, velocity, jumpState, controlEnabled, facingLeft);
        }

        private void RecordFinishFlags(OogbTick tick, OogbPlatformerRaceInput localInput, OogbPlatformerRaceInput remoteInput)
        {
            if (localInput.Finished && !localFinishTick.HasValue)
                localFinishTick = tick.Value;

            if (remoteInput.Finished && !remoteFinishTick.HasValue)
                remoteFinishTick = tick.Value;
        }

        private void ResolveWinnerIfPossible(int currentTick)
        {
            if (!localFinishTick.HasValue && !remoteFinishTick.HasValue)
                return;

            if (localFinishTick.HasValue && remoteFinishTick.HasValue)
            {
                if (localFinishTick.Value == remoteFinishTick.Value)
                    FinishRace("Draw");
                else if (localFinishTick.Value < remoteFinishTick.Value)
                    FinishRace("Win");
                else
                    FinishRace("Lose");
            }
            else if (localFinishTick.HasValue && currentTick > localFinishTick.Value)
            {
                FinishRace("Win");
            }
            else if (remoteFinishTick.HasValue && currentTick > remoteFinishTick.Value)
            {
                FinishRace("Lose");
            }
        }

        private void BeginRace()
        {
            SetupPlayers();
            ConfigurePlayerControl(true);
            phase = RacePhase.Playing;
            nextRaceTickTime = Time.time;
            AddLog("Race started");
        }

        private void FinishRace(string result)
        {
            if (phase == RacePhase.Finished)
                return;

            resultText = result;
            phase = RacePhase.Finished;
            ConfigurePlayerControl(false);

            SaveRecordedGhostIfNeeded();

            AddLog("Race result: " + result);
        }

        private void SaveRecordedGhostIfNeeded()
        {
            if (ghostRecordingSaved)
                return;

            if (ghostRecording.Count == 0)
            {
                AddLog("No ghost frames to save");
                return;
            }

            SaveGhost();
        }

        private void SaveGhost()
        {
            SavedGhosts[ghostName] = new List<GhostFrame>(ghostRecording);
            ghostRecordingSaved = true;
            AddLog("Saved ghost frames in memory: " + ghostRecording.Count);
        }

        private void RecordLocalGhostFrame(int tick, OogbPlatformerRaceInput input)
        {
            if (!recordLocalGhost)
                return;

            ghostRecording.Add(new GhostFrame
            {
                Tick = tick,
                Horizontal = input.Horizontal,
                JumpPressed = input.JumpPressed,
                JumpReleased = input.JumpReleased,
                Finished = input.Finished,
                HasSnapshot = localPlayer != null,
                Position = localPlayer == null ? Vector2.zero : (Vector2)localPlayer.transform.position,
                Velocity = localPlayer == null ? Vector2.zero : localPlayer.velocity,
                JumpState = localPlayer == null ? 0 : (int)localPlayer.jumpState,
                ControlEnabled = localPlayer == null || localPlayer.controlEnabled,
                FacingLeft = localPlayer != null && localPlayer.FacingLeft
            });
        }

        private void LoadGhost()
        {
            ghostReplay.Clear();

            if (SavedGhosts.TryGetValue(ghostName, out var savedFrames))
            {
                ghostReplay.AddRange(savedFrames);
                ghostReplayTick = 0;
                AddLog("Loaded memory ghost frames: " + ghostReplay.Count);
                return;
            }

            var count = PlayerPrefs.GetInt(GetGhostCountKey(), 0);

            for (var i = 0; i < count; i++)
            {
                var prefix = GetGhostFrameKey(i);
                ghostReplay.Add(new GhostFrame
                {
                    Tick = PlayerPrefs.GetInt(prefix + ".tick", i),
                    Horizontal = PlayerPrefs.GetFloat(prefix + ".horizontal", 0f),
                    JumpPressed = PlayerPrefs.GetInt(prefix + ".jumpPressed", 0) != 0,
                    JumpReleased = PlayerPrefs.GetInt(prefix + ".jumpReleased", 0) != 0,
                    Finished = PlayerPrefs.GetInt(prefix + ".finished", 0) != 0,
                    HasSnapshot = PlayerPrefs.GetInt(prefix + ".hasSnapshot", 0) != 0,
                    Position = new Vector2(
                        PlayerPrefs.GetFloat(prefix + ".positionX", 0f),
                        PlayerPrefs.GetFloat(prefix + ".positionY", 0f)),
                    Velocity = new Vector2(
                        PlayerPrefs.GetFloat(prefix + ".velocityX", 0f),
                        PlayerPrefs.GetFloat(prefix + ".velocityY", 0f)),
                    JumpState = PlayerPrefs.GetInt(prefix + ".jumpState", 0),
                    ControlEnabled = PlayerPrefs.GetInt(prefix + ".controlEnabled", 1) != 0,
                    FacingLeft = PlayerPrefs.GetInt(prefix + ".facingLeft", 0) != 0
                });
            }

            ghostReplayTick = 0;
            AddLog("Loaded ghost frames: " + ghostReplay.Count);
        }

        private void ClearGhost()
        {
            SavedGhosts.Remove(ghostName);
            var count = PlayerPrefs.GetInt(GetGhostCountKey(), 0);

            for (var i = 0; i < count; i++)
            {
                var prefix = GetGhostFrameKey(i);
                PlayerPrefs.DeleteKey(prefix + ".tick");
                PlayerPrefs.DeleteKey(prefix + ".horizontal");
                PlayerPrefs.DeleteKey(prefix + ".jumpPressed");
                PlayerPrefs.DeleteKey(prefix + ".jumpReleased");
                PlayerPrefs.DeleteKey(prefix + ".finished");
                PlayerPrefs.DeleteKey(prefix + ".hasSnapshot");
                PlayerPrefs.DeleteKey(prefix + ".positionX");
                PlayerPrefs.DeleteKey(prefix + ".positionY");
                PlayerPrefs.DeleteKey(prefix + ".velocityX");
                PlayerPrefs.DeleteKey(prefix + ".velocityY");
                PlayerPrefs.DeleteKey(prefix + ".jumpState");
                PlayerPrefs.DeleteKey(prefix + ".controlEnabled");
                PlayerPrefs.DeleteKey(prefix + ".facingLeft");
            }

            PlayerPrefs.DeleteKey(GetGhostCountKey());
            ghostRecordingSaved = false;
            AddLog("Cleared ghost");
        }

        private void RequestLocalFinish()
        {
            if (phase != RacePhase.Playing)
                return;

            localFinishRequested = true;
            AddLog("Debug local finish requested");
        }

        private void RequestRemoteFinish()
        {
            if (phase != RacePhase.Playing)
                return;

            if (!remoteFinishTick.HasValue)
                remoteFinishTick = session == null ? submittedTicks.Count : session.CurrentTick.Value;
        }

        private void RequestDrawFinish()
        {
            if (phase != RacePhase.Playing)
                return;

            localFinishRequested = true;
            if (session != null && !remoteFinishTick.HasValue)
                remoteFinishTick = session.CurrentTick.Value;

            AddLog("Debug draw finish requested");
        }

        private bool HandleVictory(PlayerController player)
        {
            if (phase != RacePhase.Playing)
                return false;

            if (player == localPlayer)
            {
                localFinishRequested = true;
                player.controlEnabled = false;
                AddLog("Local player reached goal");
                return true;
            }

            if (player == remotePlayer)
                return true;

            return false;
        }

        private void SetupPlayers()
        {
            SetupPlayers(role);
        }

        private void SetupPlayers(NetworkRole spawnRole)
        {
            if (model.player == null)
            {
                AddLog("No PlatformerModel player found");
                return;
            }

            if (localPlayer == null)
                localPlayer = model.player;

            CachePlayerBaseScale(localPlayer);

            if (remotePlayer == null)
            {
                var remoteObject = Instantiate(model.player.gameObject);
                remoteObject.name = spawnRole == NetworkRole.Owner ? "Remote Player B" : "Remote Player A";
                remotePlayer = remoteObject.GetComponent<PlayerController>();
            }

            ResetPlayerForRoomStart(localPlayer);
            ResetPlayerForRoomStart(remotePlayer);

            localPlayer.name = spawnRole == NetworkRole.Owner ? "Local Player A" : "Local Player B";
            TintPlayer(localPlayer, spawnRole == NetworkRole.Owner ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.35f, 0.9f));
            TintPlayer(remotePlayer, spawnRole == NetworkRole.Owner ? new Color(1f, 0.35f, 0.9f) : new Color(0.2f, 0.9f, 1f));

            var spawnPosition = model.spawnPoint == null ? localPlayer.transform.position : model.spawnPoint.position;
            var ownerSpawn = spawnPosition + Vector3.left * 0.75f;
            var clientSpawn = spawnPosition + Vector3.right * 0.75f;
            localPlayer.Teleport(spawnRole == NetworkRole.Owner ? ownerSpawn : clientSpawn);
            remotePlayer.Teleport(spawnRole == NetworkRole.Owner ? clientSpawn : ownerSpawn);
            model.player = localPlayer;

            if (model.virtualCamera != null)
            {
                model.virtualCamera.Follow = localPlayer.transform;
                model.virtualCamera.LookAt = localPlayer.transform;
            }
        }

        private void SetupSoloGhostRecorderPlayer()
        {
            SetupPlayers(NetworkRole.Ghost);

            if (localPlayer != null)
            {
                localPlayer.useExternalInput = true;
                localPlayer.controlEnabled = true;
            }

            if (remotePlayer != null)
            {
                remotePlayer.useExternalInput = true;
                remotePlayer.controlEnabled = false;
            }

            AddLog("Solo ghost recorder ready");
        }

        private void RestoreSoloGhostRecorderPlayerControl()
        {
            if (localPlayer != null)
            {
                localPlayer.SetSnapshotPlayback(false);
                localPlayer.useExternalInput = false;
                localPlayer.controlEnabled = true;
            }

            if (remotePlayer != null)
            {
                remotePlayer.SetSnapshotPlayback(false);
                remotePlayer.controlEnabled = false;
            }
        }

        private void ConfigurePlayerControl(bool enabled)
        {
            if (localPlayer != null)
            {
                if (!enabled)
                    localPlayer.SetSnapshotPlayback(false);
                localPlayer.useExternalInput = enabled;
                localPlayer.controlEnabled = enabled;
            }

            if (remotePlayer != null)
            {
                if (!enabled)
                    remotePlayer.SetSnapshotPlayback(false);
                remotePlayer.useExternalInput = enabled;
                remotePlayer.controlEnabled = enabled;
            }
        }

        private void CachePlayerBaseScale(PlayerController player)
        {
            if (hasPlayerBaseScale || player == null)
                return;

            playerBaseScale = NormalizePlayerScale(player.transform.localScale);
            hasPlayerBaseScale = true;
        }

        private void ResetPlayersForRoomStart()
        {
            if (localPlayer == null && model.player != null)
                localPlayer = model.player;

            CachePlayerBaseScale(localPlayer);
            ResetPlayerForRoomStart(localPlayer);
            ResetPlayerForRoomStart(remotePlayer);
        }

        private void ResetPlayerForRoomStart(PlayerController player)
        {
            if (player == null)
                return;

            player.SetSnapshotPlayback(false);
            player.transform.localScale = playerBaseScale;
            player.ClearExternalInput();
        }

        private static Vector3 NormalizePlayerScale(Vector3 scale)
        {
            var x = Mathf.Abs(scale.x);
            var y = Mathf.Abs(scale.y);
            var z = Mathf.Abs(scale.z);

            if (x < 0.5f || y < 0.5f || z < 0.5f || x > 2f || y > 2f || z > 2f)
                return Vector3.one;

            return new Vector3(x, y, z);
        }

        private void TintPlayer(PlayerController player, Color color)
        {
            if (player == null)
                return;

            var spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.color = color;
        }

        private void DisposeTransport()
        {
            session = null;

            if (transport != null)
            {
                transport.Dispose();
                transport = null;
            }
        }

        private bool TryParsePorts(out int ownerPort, out int clientPort)
        {
            if (!int.TryParse(ownerPortText, out ownerPort) || ownerPort <= 0 || ownerPort > 65535)
            {
                clientPort = 0;
                AddLog("Invalid owner port");
                return false;
            }

            if (!int.TryParse(clientPortText, out clientPort) || clientPort <= 0 || clientPort > 65535)
            {
                AddLog("Invalid client port");
                return false;
            }

            if (ownerPort == clientPort)
            {
                AddLog("Ports must be different");
                return false;
            }

            return true;
        }

        private void DrawLog()
        {
            GUILayout.Label("Event Log", SubHeaderStyle());
            logScroll = GUILayout.BeginScrollView(logScroll, GUI.skin.box, GUILayout.Height(180));

            foreach (var line in logLines)
                GUILayout.Label(line);

            GUILayout.EndScrollView();
        }

        private void AddLog(string message)
        {
            logLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);

            if (logLines.Count > 80)
                logLines.RemoveAt(0);
        }

        private static string FormatTick(int? tick)
        {
            return tick.HasValue ? tick.Value.ToString() : "-";
        }

        private static OogbPlatformerRaceInput ToRaceInput(GhostFrame frame)
        {
            return new OogbPlatformerRaceInput(
                frame.Horizontal,
                frame.JumpPressed,
                frame.JumpReleased,
                frame.Finished,
                frame.HasSnapshot,
                frame.Position,
                frame.Velocity,
                frame.JumpState,
                frame.ControlEnabled,
                frame.FacingLeft);
        }

        private static PlayerController.JumpState ClampJumpState(int jumpState)
        {
            if (jumpState < 0 || jumpState > (int)PlayerController.JumpState.Landed)
                return PlayerController.JumpState.Grounded;

            return (PlayerController.JumpState)jumpState;
        }

        private int GetSavedGhostCount()
        {
            if (SavedGhosts.TryGetValue(ghostName, out var frames))
                return frames.Count;

            return PlayerPrefs.GetInt(GetGhostCountKey(), 0);
        }

        private string GetGhostCountKey()
        {
            return "OOGB.Platformer.Ghost." + ghostName + ".count";
        }

        private string GetGhostFrameKey(int index)
        {
            return "OOGB.Platformer.Ghost." + ghostName + "." + index;
        }

        private static GUIStyle HeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            return style;
        }

        private static GUIStyle SubHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            return style;
        }
    }
}
