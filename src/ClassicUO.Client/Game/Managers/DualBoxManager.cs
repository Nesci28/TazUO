// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

internal enum DualBoxRole
{
    Standalone,
    Master,
    Client
}

internal enum DualBoxMessageType
{
    Hello,
    State,
    Sync,
    Step,
    MountState,
    Macro,
    MacroStop,
    Target,
    WarMode,
    Attack,
    GumpResponse,
    ScriptCommand,
    Nack,
    Stop
}

internal enum DualBoxTargetKind
{
    None,
    Entity,
    Location
}

internal enum DualBoxMountAction
{
    None,
    Mount,
    Dismount
}

internal sealed class DualBoxMessage
{
    public DualBoxMessageType Type { get; set; }
    public int Protocol { get; set; } = DualBoxManager.ProtocolVersion;
    public string ServerName { get; set; } = string.Empty;
    public int MapIndex { get; set; } = -1;
    public uint Serial { get; set; }
    public ushort X { get; set; }
    public ushort Y { get; set; }
    public sbyte Z { get; set; }
    public byte Direction { get; set; }
    public bool Ready { get; set; }
    public bool Run { get; set; }
    public long Sequence { get; set; }
    public bool Mounted { get; set; }
    public long MountSequence { get; set; }
    public long MacroSequence { get; set; }
    public string MacroDefinition { get; set; } = string.Empty;
    public DualBoxTargetKind TargetKind { get; set; }
    public uint TargetSerial { get; set; }
    public ushort TargetGraphic { get; set; }
    public ushort TargetX { get; set; }
    public ushort TargetY { get; set; }
    public sbyte TargetZ { get; set; }
    public ushort StartX { get; set; }
    public ushort StartY { get; set; }
    public sbyte StartZ { get; set; }
    public byte StartDirection { get; set; }
    public long ActionSequence { get; set; }
    public bool WarMode { get; set; }
    public uint AttackSerial { get; set; }
    public long GumpSequence { get; set; }
    public uint GumpServerSerial { get; set; }
    public int GumpButton { get; set; }
    public uint[] GumpSwitches { get; set; } = [];
    public DualBoxGumpEntry[] GumpEntries { get; set; } = [];
    public uint[] GroupSerials { get; set; } = [];
    public string Command { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

internal sealed class DualBoxGumpEntry
{
    public ushort Index { get; set; }
    public string Text { get; set; } = string.Empty;
}

internal static class DualBoxProtocol
{
    internal const int MaxMessageSize = 64 * 1024;

    internal static byte[] Encode(DualBoxMessage message)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message);

        if (payload.Length > MaxMessageSize)
            throw new InvalidDataException($"Dual-box message is too large: {payload.Length} bytes.");

        byte[] frame = new byte[payload.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
        payload.CopyTo(frame.AsSpan(sizeof(int)));
        return frame;
    }

    internal static DualBoxMessage Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < sizeof(int))
            throw new InvalidDataException("Dual-box frame is missing its length prefix.");

        int length = BinaryPrimitives.ReadInt32BigEndian(frame);

        if (length < 0 || length > MaxMessageSize || frame.Length != length + sizeof(int))
            throw new InvalidDataException("Dual-box frame length is invalid.");

        return JsonSerializer.Deserialize<DualBoxMessage>(frame[sizeof(int)..])
            ?? throw new InvalidDataException("Dual-box frame has no message payload.");
    }

    internal static async ValueTask<DualBoxMessage> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(prefix);

        if (length < 0 || length > MaxMessageSize)
            throw new InvalidDataException($"Invalid dual-box message length: {length}.");

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<DualBoxMessage>(payload)
            ?? throw new InvalidDataException("Dual-box message payload was empty.");
    }
}

/// <summary>
/// Coordinates movement between local TazUO processes. The master listens only on loopback,
/// and every follower uses its normal walker so UO sequence and fast-walk handling stay intact.
/// </summary>
public sealed class DualBoxManager
{
    public const int ProtocolVersion = 5;
    public const int DefaultPort = 47651;
    public const int SyncRange = 10;
    public const int MaxScriptCommandLength = 4096;
    public const int MaxMacroDefinitionLength = 8192;
    internal const uint MacroTargetCursorTimeout = 5000;
    internal const uint PendingTargetTimeout = 5000;
    internal const uint PendingGumpResponseTimeout = 5000;
    internal const int MaxGumpSwitches = 512;
    internal const int MaxGumpEntries = 32;
    internal const int MaxGumpEntryTextLength = 239;
    internal const uint MountActionTimeout = 3000;
    internal const int MaxMountActionAttempts = 2;

    private sealed class Peer
    {
        public Peer(TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
        }

        public TcpClient Client { get; }
        public NetworkStream Stream { get; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        public DualBoxMessage State { get; set; }
        public bool Selected { get; set; }
        public bool Ready { get; set; }
        public long ExpectedSequence { get; set; }
        public bool MountReady { get; set; }
        public bool ExpectedMounted { get; set; }
        public long ExpectedMountSequence { get; set; }
    }

    private sealed class PendingGumpResponse
    {
        public PendingGumpResponse(DualBoxMessage message)
        {
            Message = message;
            Deadline = Time.Ticks + PendingGumpResponseTimeout;
        }

        public DualBoxMessage Message { get; }
        public uint Deadline { get; }
    }

    private static readonly Lazy<DualBoxManager> _instance = new(() => new DualBoxManager());
    private readonly object _gate = new();
    private readonly List<Peer> _peers = [];
    private readonly Queue<DualBoxMessage> _pendingSteps = [];
    private readonly Queue<PendingGumpResponse> _pendingGumpResponses = [];
    private readonly HashSet<uint> _groupSerials = [];

    private CancellationTokenSource _cancellation;
    private TcpListener _listener;
    private Peer _masterPeer;
    private DualBoxRole _role;
    private string _status = "Stopped";
    private uint _nextHeartbeat;
    private long _stepSequence;
    private long _mountSequence;
    private long _macroSequence;
    private long _actionSequence;
    private long _gumpSequence;
    private long _clientSequence;
    private long _clientMountSequence;
    private long _clientMacroSequence;
    private long _clientActionSequence;
    private long _clientGumpSequence;
    private long _pendingMountSequence;
    private bool? _lastMasterMounted;
    private bool? _expectedClientMounted;
    private bool _mountActionRequested;
    private bool _mountActionTargetMounted;
    private bool? _mountAttemptTargetMounted;
    private int _mountActionAttempts;
    private uint _mountActionDeadline;
    private bool _clientFollowing;
    private bool _clientReady;
    private bool _aligning;
    private bool _alignmentPathStarted;
    private bool _executingRemoteStep;
    private byte? _awaitingWalkSequence;
    private long _activeMasterMacroSequence;
    private uint _masterTargetCursorDeadline;
    private bool _masterTargetCursorActive;
    private DualBoxMessage _pendingTarget;
    private uint _pendingTargetDeadline;
    private ushort _targetX;
    private ushort _targetY;
    private sbyte _targetZ;
    private Direction _targetDirection;

    private DualBoxManager() { }

    public static DualBoxManager Instance => _instance.Value;

    internal event Action<string> ScriptCommandReceived;

    public bool IsMaster
    {
        get
        {
            lock (_gate)
                return _role == DualBoxRole.Master;
        }
    }

    public string ModeText
    {
        get
        {
            lock (_gate)
                return _role switch
                {
                    DualBoxRole.Master => "Master",
                    DualBoxRole.Client => "Client",
                    _ => "Stopped"
                };
        }
    }

    public string StatusText
    {
        get
        {
            lock (_gate)
                return _status;
        }
    }

    public int ConnectedClientCount
    {
        get
        {
            lock (_gate)
                return _role == DualBoxRole.Master ? _peers.Count : _masterPeer == null ? 0 : 1;
        }
    }

    public int SelectedClientCount
    {
        get
        {
            lock (_gate)
                return _peers.Count(p => p.Selected);
        }
    }

    public int ReadyClientCount
    {
        get
        {
            lock (_gate)
                return _peers.Count(p => p.Selected && p.Ready && p.MountReady);
        }
    }

    public bool IsWaitingForClients
    {
        get
        {
            lock (_gate)
                return _role == DualBoxRole.Master
                    && _peers.Any(p => p.Selected && (!p.Ready || !p.MountReady));
        }
    }

    public void StartMaster()
    {
        Stop();

        try
        {
            var cancellation = new CancellationTokenSource();
            var listener = new TcpListener(IPAddress.Loopback, DefaultPort);
            listener.Start();

            lock (_gate)
            {
                _role = DualBoxRole.Master;
                _cancellation = cancellation;
                _listener = listener;
                _status = $"Listening on 127.0.0.1:{DefaultPort}";
            }

            _ = AcceptLoopAsync(listener, cancellation.Token);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not start master: {ex.Message}");
            Log.Error($"Dual box master failed to start: {ex}");
        }
    }

    public void StartClient()
    {
        Stop();

        var cancellation = new CancellationTokenSource();
        DualBoxMessage hello = CreateStateMessage(DualBoxMessageType.Hello, false);

        lock (_gate)
        {
            _role = DualBoxRole.Client;
            _cancellation = cancellation;
            _status = $"Connecting to 127.0.0.1:{DefaultPort}";
        }

        _ = ConnectClientAsync(hello, cancellation.Token);
    }

    public void Stop()
    {
        CancellationTokenSource cancellation;
        TcpListener listener;
        List<Peer> peers;

        lock (_gate)
        {
            cancellation = _cancellation;
            listener = _listener;
            peers = [.. _peers];

            if (_masterPeer != null)
                peers.Add(_masterPeer);

            _cancellation = null;
            _listener = null;
            _masterPeer = null;
            _peers.Clear();
            _role = DualBoxRole.Standalone;
            _status = "Stopped";
            _macroSequence = 0;
            _actionSequence = 0;
            _gumpSequence = 0;
            ClearMasterTargetSyncLocked();
        }

        ResetClientState();
        cancellation?.Cancel();

        try
        {
            listener?.Stop();
        }
        catch (SocketException)
        {
        }

        foreach (Peer peer in peers.Distinct())
            peer.Client.Close();
    }

    public int SyncClients()
    {
        DualBoxMessage masterState = CreateStateMessage(DualBoxMessageType.Sync, false);

        if (!IsUsableState(masterState))
        {
            SetStatus("Enter the game before synchronizing clients.");
            return 0;
        }

        List<Peer> selected;
        List<Peer> deselected;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
            {
                _status = "Start this instance as master first.";
                return 0;
            }

            selected = [];
            deselected = [];
            ClearMasterTargetSyncLocked();
            _stepSequence++;
            masterState.Sequence = _stepSequence;
            masterState.MountSequence = ++_mountSequence;
            _lastMasterMounted = masterState.Mounted;

            foreach (Peer peer in _peers)
            {
                bool inRange = IsCompatible(masterState, peer.State)
                    && IsWithinSyncRange(masterState.X, masterState.Y, peer.State.X, peer.State.Y);

                if (peer.Selected && !inRange)
                    deselected.Add(peer);

                peer.Selected = inRange;
                peer.Ready = false;
                peer.ExpectedSequence = masterState.Sequence;
                peer.MountReady = false;
                peer.ExpectedMounted = masterState.Mounted;
                peer.ExpectedMountSequence = masterState.MountSequence;

                if (inRange)
                    selected.Add(peer);
            }

            masterState.GroupSerials = selected
                .Where(p => p.State != null)
                .Select(p => p.State.Serial)
                .Append(masterState.Serial)
                .Distinct()
                .ToArray();

            _status = selected.Count == 0
                ? $"No compatible client within {SyncRange} tiles."
                : $"Aligning {selected.Count} client(s)...";
        }

        foreach (Peer peer in deselected)
            Send(peer, new DualBoxMessage { Type = DualBoxMessageType.Stop });

        foreach (Peer peer in selected)
            Send(peer, masterState);

        return selected.Count;
    }

    /// <summary>
    /// Sends an application-defined command to every TazUO instance connected as a client.
    /// Commands are delivered to LegionScript callbacks and are never evaluated as code.
    /// </summary>
    public int BroadcastScriptCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            SetStatus("Dual-box script command cannot be empty.");
            return 0;
        }

        if (command.Length > MaxScriptCommandLength)
        {
            SetStatus($"Dual-box script command exceeds {MaxScriptCommandLength} characters.");
            return 0;
        }

        List<Peer> clients;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
            {
                _status = "Only the dual-box master can broadcast script commands.";
                return 0;
            }

            clients = [.. _peers];
        }

        var message = new DualBoxMessage
        {
            Type = DualBoxMessageType.ScriptCommand,
            Command = command
        };

        foreach (Peer client in clients)
            Send(client, message);

        return clients.Count;
    }

    internal int BroadcastMacro(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition) || definition.Length > MaxMacroDefinitionLength)
            return 0;

        List<Peer> clients;
        DualBoxMessage message;
        World world = World.Instance;
        bool targetCursorAlreadyActive = world?.TargetManager?.IsTargeting == true
            && IsSynchronizableTargetCursor(world.TargetManager.TargetingState);

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return 0;

            clients = _peers.Where(p => p.Selected).ToList();

            if (clients.Count == 0)
                return 0;

            _activeMasterMacroSequence = ++_macroSequence;
            _masterTargetCursorActive = targetCursorAlreadyActive;
            _masterTargetCursorDeadline = targetCursorAlreadyActive
                ? 0
                : Time.Ticks + MacroTargetCursorTimeout;

            message = new DualBoxMessage
            {
                Type = DualBoxMessageType.Macro,
                MacroSequence = _activeMasterMacroSequence,
                MacroDefinition = definition
            };
        }

        foreach (Peer client in clients)
            Send(client, message);

        return clients.Count;
    }

    internal int BroadcastMacroStop()
    {
        List<Peer> clients;
        DualBoxMessage message;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return 0;

            clients = _peers.Where(p => p.Selected).ToList();

            if (clients.Count == 0)
                return 0;

            ClearMasterTargetSyncLocked();
            message = new DualBoxMessage
            {
                Type = DualBoxMessageType.MacroStop,
                MacroSequence = ++_macroSequence
            };
        }

        foreach (Peer client in clients)
            Send(client, message);

        return clients.Count;
    }

    internal int BroadcastWarMode(bool enabled)
    {
        if (World.Instance?.Macros?.IsProcessingAction == true)
            return 0;

        return BroadcastAction(
            new DualBoxMessage
            {
                Type = DualBoxMessageType.WarMode,
                WarMode = enabled
            }
        );
    }

    internal int BroadcastAttack(uint serial)
    {
        if (!SerialHelper.IsMobile(serial) || World.Instance?.Macros?.IsProcessingAction == true)
            return 0;

        return BroadcastAction(
            new DualBoxMessage
            {
                Type = DualBoxMessageType.Attack,
                AttackSerial = serial
            }
        );
    }

    private int BroadcastAction(DualBoxMessage message)
    {
        List<Peer> clients;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return 0;

            clients = _peers.Where(p => p.Selected).ToList();

            if (clients.Count == 0)
                return 0;

            message.ActionSequence = ++_actionSequence;
        }

        foreach (Peer client in clients)
            Send(client, message);

        return clients.Count;
    }

    internal int BroadcastGumpResponse(
        uint serverSerial,
        int button,
        uint[] switches,
        Tuple<ushort, string>[] entries
    )
    {
        if (serverSerial == 0 || World.Instance?.Macros?.IsProcessingAction == true)
            return 0;

        List<Peer> clients;
        DualBoxMessage message;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return 0;

            clients = _peers.Where(p => p.Selected).ToList();

            if (clients.Count == 0)
                return 0;

            message = new DualBoxMessage
            {
                Type = DualBoxMessageType.GumpResponse,
                GumpSequence = ++_gumpSequence,
                GumpServerSerial = serverSerial,
                GumpButton = button,
                GumpSwitches = (switches ?? []).Take(MaxGumpSwitches).ToArray(),
                GumpEntries = (entries ?? [])
                    .Where(entry => entry != null)
                    .Take(MaxGumpEntries)
                    .Select(
                        entry => new DualBoxGumpEntry
                        {
                            Index = entry.Item1,
                            Text = TruncateGumpEntry(entry.Item2)
                        }
                    )
                    .ToArray()
            };
        }

        foreach (Peer client in clients)
            Send(client, message);

        return clients.Count;
    }

    internal static string TruncateGumpEntry(string text)
    {
        text ??= string.Empty;
        return text.Length <= MaxGumpEntryTextLength ? text : text[..MaxGumpEntryTextLength];
    }

    internal void OnTargetCursorActivated(CursorTarget targeting)
    {
        if (!IsSynchronizableTargetCursor(targeting))
            return;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master || _activeMasterMacroSequence == 0)
                return;

            if (HasDeadlinePassed(Time.Ticks, _masterTargetCursorDeadline))
            {
                ClearMasterTargetSyncLocked();
                return;
            }

            _masterTargetCursorActive = true;
            _masterTargetCursorDeadline = 0;
        }
    }

    internal void OnTargetCursorCancelled()
    {
        lock (_gate)
        {
            if (_role == DualBoxRole.Master && _masterTargetCursorActive)
                ClearMasterTargetSyncLocked();
        }
    }

    internal void OnEntityTargetSelected(uint serial)
    {
        if (serial == 0)
            return;

        BroadcastTargetSelection(
            new DualBoxMessage
            {
                Type = DualBoxMessageType.Target,
                TargetKind = DualBoxTargetKind.Entity,
                TargetSerial = serial
            }
        );
    }

    internal void OnLocationTargetSelected(ushort graphic, ushort x, ushort y, sbyte z)
    {
        BroadcastTargetSelection(
            new DualBoxMessage
            {
                Type = DualBoxMessageType.Target,
                TargetKind = DualBoxTargetKind.Location,
                TargetGraphic = graphic,
                TargetX = x,
                TargetY = y,
                TargetZ = z
            }
        );
    }

    private void BroadcastTargetSelection(DualBoxMessage message)
    {
        List<Peer> clients;
        bool resolvedByMacroAction = World.Instance?.Macros?.IsProcessingAction == true;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master
                || !_masterTargetCursorActive
                || _activeMasterMacroSequence == 0)
            {
                return;
            }

            // TargetSelf/LastTarget and similar actions are already replayed independently by
            // each client. Only a target chosen by the player after the macro yields is shared.
            if (resolvedByMacroAction)
            {
                ClearMasterTargetSyncLocked();
                return;
            }

            clients = _peers.Where(p => p.Selected).ToList();
            message.MacroSequence = _activeMasterMacroSequence;
            ClearMasterTargetSyncLocked();
        }

        foreach (Peer client in clients)
            Send(client, message);
    }

    private void ClearMasterTargetSyncLocked()
    {
        _activeMasterMacroSequence = 0;
        _masterTargetCursorDeadline = 0;
        _masterTargetCursorActive = false;
    }

    /// <summary>
    /// Prevents the master from outrunning followers and prevents manual movement on an active
    /// follower. The master is released once every selected follower confirms the current step.
    /// </summary>
    public bool AllowLocalWalk()
    {
        World world = World.Instance;

        if (world?.InGame == true && world.Player != null)
            UpdateMasterMountState(world);

        lock (_gate)
        {
            if (_role == DualBoxRole.Master)
            {
                foreach (Peer peer in _peers)
                {
                    if (!peer.Selected)
                        continue;

                    if (!peer.Ready)
                        return false;

                    if (!peer.MountReady)
                        return false;
                }

                return true;
            }

            if (_role == DualBoxRole.Client && _clientFollowing)
                return _executingRemoteStep;

            return true;
        }
    }

    public void OnLocalStepRequested(int startX, int startY, sbyte startZ, in StepInfo step)
    {
        List<Peer> selected;
        DualBoxMessage message;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return;

            selected = _peers.Where(p => p.Selected).ToList();

            if (selected.Count == 0)
                return;

            message = new DualBoxMessage
            {
                Type = DualBoxMessageType.Step,
                Sequence = ++_stepSequence,
                StartX = (ushort)startX,
                StartY = (ushort)startY,
                StartZ = startZ,
                StartDirection = step.OldDirection,
                X = step.X,
                Y = step.Y,
                Z = step.Z,
                Direction = (byte)((Direction)step.Direction & Direction.Mask),
                Run = step.Running
            };

            foreach (Peer peer in selected)
            {
                peer.Ready = false;
                peer.ExpectedSequence = message.Sequence;
            }

            _status = $"Step {message.Sequence}: waiting for {selected.Count} client(s).";
        }

        foreach (Peer peer in selected)
            Send(peer, message);
    }

    public void OnWalkConfirmed(byte sequence, bool accepted)
    {
        if (!_clientFollowing || !_awaitingWalkSequence.HasValue)
            return;

        if (_awaitingWalkSequence.Value != sequence)
        {
            byte expectedSequence = _awaitingWalkSequence.Value;
            _awaitingWalkSequence = null;
            _pendingSteps.Clear();
            FailClientStep($"Expected game-server confirmation {expectedSequence}, received {sequence}.");
            return;
        }

        _awaitingWalkSequence = null;

        if (!accepted)
        {
            FailClientStep($"The game server could not confirm follower step {_clientSequence}.");
            return;
        }

        _clientReady = true;
        SetStatus("Synchronized with master.");
        SendClientState(DualBoxMessageType.State, string.Empty);
    }

    public void OnWalkDenied()
    {
        DualBoxRole role;

        lock (_gate)
            role = _role;

        if (role == DualBoxRole.Master)
        {
            ResyncSelectedClients("Master movement was rejected; realigning.");
        }
        else if (role == DualBoxRole.Client && _clientFollowing)
        {
            _pendingSteps.Clear();
            _awaitingWalkSequence = null;
            _clientReady = false;
            _aligning = false;
            _alignmentPathStarted = false;
            World.Instance?.Player?.Pathfinder.StopAutoWalk();
            SendClientState(DualBoxMessageType.Nack, "The game server rejected the follower's movement.");
        }
    }

    public bool ShouldIgnoreMobile(uint serial)
        => _clientFollowing && _groupSerials.Contains(serial);

    public void ProcessAutoWalk(Pathfinder pathfinder)
    {
        bool isAlignmentStep = _clientFollowing && _aligning;

        if (isAlignmentStep)
            _executingRemoteStep = true;

        try
        {
            pathfinder.ProcessAutoWalk();
        }
        finally
        {
            if (isAlignmentStep)
                _executingRemoteStep = false;
        }
    }

    public void Update()
    {
        World world = World.Instance;

        if (world?.InGame != true || world.Player == null)
            return;

        DualBoxRole role;

        lock (_gate)
            role = _role;

        if (role == DualBoxRole.Master)
        {
            UpdateMasterMountState(world);
            return;
        }

        if (role != DualBoxRole.Client)
            return;

        if (Time.Ticks >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.Ticks + 500;
            SendClientState(DualBoxMessageType.State, _clientReady ? string.Empty : "Not ready");
        }

        ProcessPendingTarget(world);
        ProcessPendingGumpResponses(world);

        if (_clientFollowing && !ProcessClientMountState(world))
            return;

        if (_aligning)
        {
            ProcessAlignment(world);
            return;
        }

        if (_clientFollowing && !_awaitingWalkSequence.HasValue && _pendingSteps.Count != 0)
            ProcessPendingStep(world);
    }

    internal static bool IsWithinSyncRange(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2)) <= SyncRange;

    internal static bool IsSynchronizableTargetCursor(CursorTarget targeting)
        => targeting is CursorTarget.Object or CursorTarget.Position or CursorTarget.MultiPlacement;

    internal static bool HasDeadlinePassed(uint now, uint deadline)
        => deadline != 0 && unchecked((int)(now - deadline)) >= 0;

    internal static DualBoxMountAction GetMountAction(bool desiredMounted, bool actualMounted)
        => desiredMounted == actualMounted
            ? DualBoxMountAction.None
            : desiredMounted
                ? DualBoxMountAction.Mount
                : DualBoxMountAction.Dismount;

    internal static bool CanAcknowledgeMountState(
        bool actionInFlight,
        bool actionTargetMounted,
        bool desiredMounted,
        bool actualMounted
    ) => actualMounted == desiredMounted
        && (!actionInFlight || actualMounted == actionTargetMounted);

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                client.NoDelay = true;
                var peer = new Peer(client);

                lock (_gate)
                {
                    if (_role != DualBoxRole.Master || token.IsCancellationRequested)
                    {
                        client.Close();
                        continue;
                    }

                    _peers.Add(peer);
                    _status = $"{_peers.Count} client(s) connected.";
                }

                _ = ReadPeerAsync(peer, true, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Master listener stopped: {ex.Message}");
            Log.Error($"Dual box listener failed: {ex}");
        }
    }

    private async Task ConnectClientAsync(DualBoxMessage hello, CancellationToken token)
    {
        Peer peer = null;

        try
        {
            var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            await client.ConnectAsync(IPAddress.Loopback, DefaultPort, token).ConfigureAwait(false);
            peer = new Peer(client);

            lock (_gate)
            {
                if (_role != DualBoxRole.Client || token.IsCancellationRequested)
                {
                    client.Close();
                    return;
                }

                _masterPeer = peer;
                _status = "Connected; waiting for the master to sync.";
            }

            await SendAsync(peer, hello, token).ConfigureAwait(false);
            await ReadPeerAsync(peer, false, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Client connection failed: {ex.Message}");
            Log.Warn($"Dual box client connection failed: {ex.Message}");
        }
        finally
        {
            bool isCurrentSession;

            lock (_gate)
            {
                isCurrentSession = _role == DualBoxRole.Client
                    && _cancellation != null
                    && _cancellation.Token == token
                    && _masterPeer == peer;

                if (isCurrentSession)
                {
                    _masterPeer = null;
                    _status = "Disconnected from master.";
                }
            }

            peer?.Client.Close();

            if (isCurrentSession)
            {
                MainThreadQueue.EnqueueAction(
                    () =>
                    {
                        lock (_gate)
                        {
                            if (_role != DualBoxRole.Client
                                || _cancellation == null
                                || _cancellation.Token != token)
                            {
                                return;
                            }
                        }

                        ResetClientState();
                    },
                    token
                );
            }
        }
    }

    private async Task ReadPeerAsync(Peer peer, bool masterSide, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && peer.Client.Connected)
            {
                DualBoxMessage message = await DualBoxProtocol.ReadAsync(peer.Stream, token).ConfigureAwait(false);

                if (message.Protocol != ProtocolVersion)
                    throw new InvalidDataException($"Unsupported dual-box protocol {message.Protocol}.");

                if (masterSide)
                    HandleMasterMessage(peer, message, token);
                else
                    HandleClientMessage(message, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        catch (Exception ex)
        {
            Log.Warn($"Dual box peer disconnected: {ex.Message}");
        }
        finally
        {
            if (masterSide)
            {
                lock (_gate)
                {
                    if (_role == DualBoxRole.Master
                        && _cancellation != null
                        && _cancellation.Token == token)
                    {
                        _peers.Remove(peer);
                        _status = $"{_peers.Count} client(s) connected.";
                    }
                }

                peer.Client.Close();
            }
        }
    }

    private void HandleMasterMessage(Peer peer, DualBoxMessage message, CancellationToken token)
    {
        bool needsResync = false;
        DualBoxMessage mountCorrection = null;
        bool stopMountFailedPeer = false;

        lock (_gate)
        {
            if (!_peers.Contains(peer))
                return;

            if (message.Type == DualBoxMessageType.MountState)
            {
                if (peer.Selected && message.MountSequence == peer.ExpectedMountSequence)
                {
                    peer.MountReady = message.Ready && message.Mounted == peer.ExpectedMounted;

                    if (peer.State != null)
                        peer.State.Mounted = message.Mounted;

                    if (!peer.MountReady && !string.IsNullOrEmpty(message.Error))
                    {
                        peer.Selected = false;
                        peer.Ready = false;
                        stopMountFailedPeer = true;
                        _status = $"Client removed from sync: {message.Error}";
                    }
                    else
                        UpdateMasterReadyStatusLocked();
                }
            }
            else if (message.Type is DualBoxMessageType.Hello or DualBoxMessageType.State or DualBoxMessageType.Nack)
            {
                peer.State = message;

                if (peer.Selected && message.Sequence == peer.ExpectedSequence)
                    peer.Ready = message.Type != DualBoxMessageType.Nack && message.Ready;

                if (message.Type == DualBoxMessageType.Nack
                    && peer.Selected
                    && message.Sequence == peer.ExpectedSequence)
                {
                    needsResync = true;
                }

                if (message.Type == DualBoxMessageType.State
                    && peer.Selected
                    && peer.MountReady
                    && _lastMasterMounted.HasValue
                    && message.Mounted != _lastMasterMounted.Value)
                {
                    mountCorrection = new DualBoxMessage
                    {
                        Type = DualBoxMessageType.MountState,
                        Mounted = _lastMasterMounted.Value,
                        MountSequence = ++_mountSequence
                    };
                    peer.MountReady = false;
                    peer.ExpectedMounted = mountCorrection.Mounted;
                    peer.ExpectedMountSequence = mountCorrection.MountSequence;
                }

                UpdateMasterReadyStatusLocked();
            }
        }

        if (mountCorrection != null)
            Send(peer, mountCorrection);

        if (stopMountFailedPeer)
            Send(peer, new DualBoxMessage { Type = DualBoxMessageType.Stop });

        if (needsResync)
        {
            MainThreadQueue.EnqueueAction(
                () => ResyncSelectedClients(string.IsNullOrEmpty(message.Error) ? "Follower desynchronized; realigning." : message.Error),
                token
            );
        }
    }

    private void HandleClientMessage(DualBoxMessage message, CancellationToken token)
    {
        switch (message.Type)
        {
            case DualBoxMessageType.Sync:
                MainThreadQueue.EnqueueAction(() => BeginAlignment(message), token);
                break;
            case DualBoxMessageType.Step:
                MainThreadQueue.EnqueueAction(() => QueueStep(message), token);
                break;
            case DualBoxMessageType.MountState:
                MainThreadQueue.EnqueueAction(() => QueueMountState(message), token);
                break;
            case DualBoxMessageType.Macro:
                MainThreadQueue.EnqueueAction(() => ExecuteSynchronizedMacro(message), token);
                break;
            case DualBoxMessageType.MacroStop:
                MainThreadQueue.EnqueueAction(() => StopSynchronizedMacro(message), token);
                break;
            case DualBoxMessageType.Target:
                MainThreadQueue.EnqueueAction(() => QueueTarget(message), token);
                break;
            case DualBoxMessageType.WarMode:
            case DualBoxMessageType.Attack:
                MainThreadQueue.EnqueueAction(() => ExecuteSynchronizedAction(message), token);
                break;
            case DualBoxMessageType.GumpResponse:
                MainThreadQueue.EnqueueAction(() => QueueGumpResponse(message), token);
                break;
            case DualBoxMessageType.ScriptCommand:
                MainThreadQueue.EnqueueAction(() => DispatchScriptCommand(message.Command), token);
                break;
            case DualBoxMessageType.Stop:
                MainThreadQueue.EnqueueAction(ResetClientState, token);
                break;
        }
    }

    internal void DispatchScriptCommand(string command)
    {
        if (!string.IsNullOrWhiteSpace(command) && command.Length <= MaxScriptCommandLength)
            ScriptCommandReceived?.Invoke(command);
    }

    private void ExecuteSynchronizedMacro(DualBoxMessage message)
    {
        if (!_clientFollowing
            || message.MacroSequence <= _clientMacroSequence
            || string.IsNullOrWhiteSpace(message.MacroDefinition)
            || message.MacroDefinition.Length > MaxMacroDefinitionLength)
        {
            return;
        }

        _clientMacroSequence = message.MacroSequence;
        _pendingTarget = null;
        _pendingTargetDeadline = 0;

        World world = World.Instance;

        if (world?.InGame != true
            || world.Player == null
            || !world.Macros.TryExecuteSynchronizedMacro(message.MacroDefinition))
        {
            SetStatus("Could not execute the master's macro.");
        }
    }

    private void StopSynchronizedMacro(DualBoxMessage message)
    {
        if (!_clientFollowing || message.MacroSequence <= _clientMacroSequence)
            return;

        _clientMacroSequence = message.MacroSequence;
        _pendingTarget = null;
        _pendingTargetDeadline = 0;
        World.Instance?.Macros?.StopExecution();
    }

    private void ExecuteSynchronizedAction(DualBoxMessage message)
    {
        if (!_clientFollowing || message.ActionSequence <= _clientActionSequence)
            return;

        _clientActionSequence = message.ActionSequence;
        World world = World.Instance;

        if (world?.InGame != true || world.Player == null)
            return;

        switch (message.Type)
        {
            case DualBoxMessageType.WarMode:
                GameActions.RequestWarMode(world.Player, message.WarMode);
                break;
            case DualBoxMessageType.Attack when SerialHelper.IsMobile(message.AttackSerial):
                GameActions.Attack(world, message.AttackSerial, true);
                break;
        }
    }

    private void QueueGumpResponse(DualBoxMessage message)
    {
        if (!_clientFollowing
            || message.GumpSequence <= _clientGumpSequence
            || message.GumpServerSerial == 0)
        {
            return;
        }

        _clientGumpSequence = message.GumpSequence;
        message.GumpSwitches = (message.GumpSwitches ?? []).Take(MaxGumpSwitches).ToArray();
        message.GumpEntries = (message.GumpEntries ?? [])
            .Where(entry => entry != null)
            .Take(MaxGumpEntries)
            .Select(
                entry => new DualBoxGumpEntry
                {
                    Index = entry.Index,
                    Text = TruncateGumpEntry(entry.Text)
                }
            )
            .ToArray();
        _pendingGumpResponses.Enqueue(new PendingGumpResponse(message));
        ProcessPendingGumpResponses(World.Instance);
    }

    private void ProcessPendingGumpResponses(World world)
    {
        while (_pendingGumpResponses.TryPeek(out PendingGumpResponse pending))
        {
            if (!_clientFollowing
                || world?.InGame != true
                || world.Player == null
                || HasDeadlinePassed(Time.Ticks, pending.Deadline))
            {
                _pendingGumpResponses.Dequeue();
                continue;
            }

            DualBoxMessage message = pending.Message;
            Gump gump = UIManager.GetGumpServer(message.GumpServerSerial);

            if (gump is not { IsDisposed: false, IsFromServer: true })
                return;

            Tuple<ushort, string>[] entries = (message.GumpEntries ?? [])
                .Select(entry => Tuple.Create(entry.Index, TruncateGumpEntry(entry.Text)))
                .ToArray();

            GameActions.ReplyGump(
                world,
                gump.LocalSerial,
                gump.ServerSerial,
                message.GumpButton,
                message.GumpSwitches ?? [],
                entries
            );

            if (gump.CanMove)
                UIManager.SavePosition(gump.ServerSerial, gump.Location);
            else
                UIManager.RemovePosition(gump.ServerSerial);

            gump.Dispose();
            _pendingGumpResponses.Dequeue();
        }
    }

    private void QueueTarget(DualBoxMessage message)
    {
        if (!_clientFollowing
            || message.MacroSequence != _clientMacroSequence
            || message.TargetKind == DualBoxTargetKind.None)
        {
            return;
        }

        _pendingTarget = message;
        _pendingTargetDeadline = Time.Ticks + PendingTargetTimeout;
        ProcessPendingTarget(World.Instance);
    }

    private void ProcessPendingTarget(World world)
    {
        if (_pendingTarget == null)
            return;

        if (!_clientFollowing
            || world?.InGame != true
            || world.Player == null
            || HasDeadlinePassed(Time.Ticks, _pendingTargetDeadline))
        {
            _pendingTarget = null;
            _pendingTargetDeadline = 0;
            return;
        }

        TargetManager targetManager = world.TargetManager;

        if (!targetManager.IsTargeting
            || !IsSynchronizableTargetCursor(targetManager.TargetingState))
        {
            return;
        }

        switch (_pendingTarget.TargetKind)
        {
            case DualBoxTargetKind.Entity:
                if (world.Get(_pendingTarget.TargetSerial) == null)
                    return;

                targetManager.Target(_pendingTarget.TargetSerial);
                break;
            case DualBoxTargetKind.Location:
                targetManager.Target(
                    _pendingTarget.TargetGraphic,
                    _pendingTarget.TargetX,
                    _pendingTarget.TargetY,
                    _pendingTarget.TargetZ
                );
                break;
            default:
                return;
        }

        _pendingTarget = null;
        _pendingTargetDeadline = 0;
    }

    private void BeginAlignment(DualBoxMessage message)
    {
        DualBoxMessage local = CreateStateMessage(DualBoxMessageType.State, false);

        if (!IsCompatible(message, local))
        {
            SendClientState(
                DualBoxMessageType.Nack,
                "Client is on a different server or facet.",
                message.Sequence
            );
            return;
        }

        if (!IsWithinSyncRange(message.X, message.Y, local.X, local.Y))
        {
            SendClientState(
                DualBoxMessageType.Nack,
                $"Client is more than {SyncRange} tiles from the master.",
                message.Sequence
            );
            return;
        }

        _groupSerials.Clear();

        foreach (uint serial in message.GroupSerials ?? [])
            _groupSerials.Add(serial);

        _pendingSteps.Clear();
        _pendingTarget = null;
        _pendingTargetDeadline = 0;
        _pendingGumpResponses.Clear();
        _clientSequence = message.Sequence;
        _clientFollowing = true;
        _clientReady = false;
        _aligning = true;
        _alignmentPathStarted = false;
        _awaitingWalkSequence = null;
        _targetX = message.X;
        _targetY = message.Y;
        _targetZ = message.Z;
        _targetDirection = (Direction)message.Direction & Direction.Mask;
        SetStatus("Aligning with master...");
        QueueMountState(message);
        SendClientState(DualBoxMessageType.State, "Aligning");
    }

    private void QueueMountState(DualBoxMessage message)
    {
        if (!_clientFollowing
            || message.MountSequence <= _clientMountSequence
            || message.MountSequence <= _pendingMountSequence)
        {
            return;
        }

        _expectedClientMounted = message.Mounted;
        _pendingMountSequence = message.MountSequence;

        if (!_mountActionRequested)
        {
            _mountAttemptTargetMounted = null;
            _mountActionAttempts = 0;
            _mountActionDeadline = 0;
        }

        SetStatus(message.Mounted
            ? "Matching the master's mounted state..."
            : "Matching the master's dismounted state...");
    }

    private void QueueStep(DualBoxMessage message)
    {
        if (!_clientFollowing || message.Sequence <= _clientSequence)
            return;

        if (message.Sequence != _clientSequence + 1
            || _pendingSteps.Count != 0
            || _awaitingWalkSequence.HasValue)
        {
            _pendingSteps.Clear();
            FailClientStep(
                $"Received out-of-order master step {message.Sequence}; expected {_clientSequence + 1}.",
                message.Sequence
            );
            return;
        }

        _clientReady = false;
        _pendingSteps.Enqueue(message);
        SetStatus($"Applying master step {message.Sequence}...");
    }

    private void ProcessAlignment(World world)
    {
        PlayerMobile player = world.Player;

        if (!_alignmentPathStarted && player.Walker.UnacceptedPacketsCount != 0)
            return;

        player.GetEndPosition(out int x, out int y, out sbyte z, out Direction direction);

        if (x != _targetX || y != _targetY || z != _targetZ)
        {
            if (_alignmentPathStarted)
            {
                if (!player.Pathfinder.AutoWalking && player.Walker.UnacceptedPacketsCount == 0)
                {
                    FailClientStep("Could not reach the master's tile.");
                    _aligning = false;
                }

                return;
            }

            _executingRemoteStep = true;

            try
            {
                _alignmentPathStarted = player.Pathfinder.WalkTo(_targetX, _targetY, _targetZ, 0, true);
            }
            finally
            {
                _executingRemoteStep = false;
            }

            if (!_alignmentPathStarted)
            {
                FailClientStep("Could not pathfind onto the master.");
                _aligning = false;
            }

            return;
        }

        if (player.Pathfinder.AutoWalking)
            player.Pathfinder.StopAutoWalk();

        if ((direction & Direction.Mask) != _targetDirection)
        {
            if (player.Walker.LastStepRequestTime <= Time.Ticks)
            {
                _executingRemoteStep = true;

                try
                {
                    player.Walk(_targetDirection, false);
                }
                finally
                {
                    _executingRemoteStep = false;
                }
            }

            return;
        }

        if (player.Walker.UnacceptedPacketsCount != 0)
            return;

        _aligning = false;
        _clientReady = true;
        SetStatus("Synchronized with master.");
        SendClientState(DualBoxMessageType.State, string.Empty);
    }

    private void ProcessPendingStep(World world)
    {
        PlayerMobile player = world.Player;

        if (player.Walker.LastStepRequestTime > Time.Ticks || player.Walker.StepsCount >= Constants.MAX_STEP_COUNT)
            return;

        DualBoxMessage message = _pendingSteps.Peek();
        player.GetEndPosition(out int x, out int y, out sbyte z, out Direction direction);

        if (x != message.StartX || y != message.StartY || z != message.StartZ
            || (direction & Direction.Mask) != ((Direction)message.StartDirection & Direction.Mask))
        {
            _pendingSteps.Clear();
            FailClientStep($"Follower position did not match step {message.Sequence}.", message.Sequence);
            return;
        }

        byte walkSequence = player.Walker.WalkSequence;
        _executingRemoteStep = true;
        bool walked;

        try
        {
            walked = player.WalkNotAvoid(
                (Direction)message.Direction & Direction.Mask,
                message.Run,
                honorAlwaysRun: false
            );
        }
        finally
        {
            _executingRemoteStep = false;
        }

        if (!walked)
        {
            _pendingSteps.Clear();
            FailClientStep($"Follower rejected step {message.Sequence}.", message.Sequence);
            return;
        }

        player.GetEndPosition(out x, out y, out z, out direction);

        ref StepInfo actualStep = ref player.Walker.StepInfos[player.Walker.StepsCount - 1];

        if (x != message.X || y != message.Y || z != message.Z
            || (direction & Direction.Mask) != ((Direction)message.Direction & Direction.Mask)
            || actualStep.Running != message.Run)
        {
            _pendingSteps.Clear();
            FailClientStep($"Follower ended step {message.Sequence} on a different tile.", message.Sequence);
            return;
        }

        _pendingSteps.Dequeue();
        _clientSequence = message.Sequence;
        _awaitingWalkSequence = walkSequence;
        SetStatus($"Waiting for server confirmation of step {message.Sequence}...");
    }

    private void FailClientStep(string error, long? sequence = null)
    {
        _clientReady = false;
        SetStatus(error);
        SendClientState(DualBoxMessageType.Nack, error, sequence);
    }

    private void UpdateMasterMountState(World world)
    {
        bool mounted = IsMounted(world.Player);
        List<Peer> selected;
        DualBoxMessage message;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return;

            if (!_lastMasterMounted.HasValue)
            {
                _lastMasterMounted = mounted;
                return;
            }

            if (_lastMasterMounted.Value == mounted)
                return;

            _lastMasterMounted = mounted;
            selected = _peers.Where(p => p.Selected).ToList();

            if (selected.Count == 0)
                return;

            message = CreateStateMessage(DualBoxMessageType.MountState, false);
            message.MountSequence = ++_mountSequence;

            foreach (Peer peer in selected)
            {
                peer.MountReady = false;
                peer.ExpectedMounted = mounted;
                peer.ExpectedMountSequence = message.MountSequence;
            }

            _status = $"Waiting for {selected.Count} client(s) to {(mounted ? "mount" : "dismount")}.";
        }

        foreach (Peer peer in selected)
            Send(peer, message);
    }

    private bool ProcessClientMountState(World world)
    {
        if (!_expectedClientMounted.HasValue)
            return true;

        bool actualMounted = IsMounted(world.Player);
        bool desiredMounted = _expectedClientMounted.Value;

        if (CanAcknowledgeMountState(
            _mountActionRequested,
            _mountActionTargetMounted,
            desiredMounted,
            actualMounted
        ))
        {
            CompleteClientMountState();
            return true;
        }

        if (_mountActionRequested)
        {
            if (actualMounted == _mountActionTargetMounted)
            {
                _mountActionRequested = false;
                _mountActionDeadline = 0;
            }
            else if (Time.Ticks < _mountActionDeadline)
            {
                return false;
            }
            else if (_mountActionAttempts >= MaxMountActionAttempts)
            {
                return FailClientMountState(
                    $"Timed out while trying to {(_mountActionTargetMounted ? "mount" : "dismount")}."
                );
            }
            else
            {
                return RequestClientMountState(world, _mountActionTargetMounted);
            }
        }

        DualBoxMountAction action = GetMountAction(desiredMounted, actualMounted);

        if (action == DualBoxMountAction.None)
        {
            CompleteClientMountState();
            return true;
        }

        return RequestClientMountState(world, action == DualBoxMountAction.Mount);
    }

    private bool RequestClientMountState(World world, bool mounted)
    {
        if (_mountAttemptTargetMounted != mounted)
        {
            _mountAttemptTargetMounted = mounted;
            _mountActionAttempts = 0;
        }

        _mountActionRequested = true;
        _mountActionTargetMounted = mounted;
        _mountActionAttempts++;
        _mountActionDeadline = Time.Ticks + MountActionTimeout;

        if (!mounted)
        {
            GameActions.DoubleClick(world, world.Player, true, true);
            SetStatus("Dismounting with master...");
            return false;
        }

        GameActions.MountResult result = GameActions.Mount(false);

        if (result == GameActions.MountResult.Success)
        {
            SetStatus("Mounting with master...");
            return false;
        }

        string error = result switch
        {
            GameActions.MountResult.NoDesignatedMount => "No saved mount is configured on this client.",
            GameActions.MountResult.MountNotFound => "This client's saved mount was not found.",
            GameActions.MountResult.MountTooFar => "This client's saved mount is too far away.",
            _ => "This client could not mount."
        };

        return FailClientMountState(error);
    }

    private void CompleteClientMountState()
    {
        long completedSequence = _pendingMountSequence;
        ResetClientMountAction();
        _pendingMountSequence = 0;
        _expectedClientMounted = null;

        if (completedSequence == 0)
            return;

        _clientMountSequence = completedSequence;
        SendClientMountState(true, string.Empty, completedSequence);
    }

    private bool FailClientMountState(string error)
    {
        long failedSequence = _pendingMountSequence;

        if (failedSequence != 0)
            SendClientMountState(false, error, failedSequence);

        World.Instance?.Player?.Pathfinder.StopAutoWalk();
        ResetClientState();
        SetStatus(error);

        return false;
    }

    private void ResetClientMountAction()
    {
        _mountActionRequested = false;
        _mountActionTargetMounted = false;
        _mountAttemptTargetMounted = null;
        _mountActionAttempts = 0;
        _mountActionDeadline = 0;
    }

    private void SendClientMountState(bool ready, string error, long mountSequence)
    {
        Peer peer;

        lock (_gate)
            peer = _masterPeer;

        if (peer == null)
            return;

        DualBoxMessage state = CreateStateMessage(DualBoxMessageType.MountState, ready);
        state.MountSequence = mountSequence;
        state.Error = error;
        Send(peer, state);
    }

    private void ResyncSelectedClients(string reason)
    {
        DualBoxMessage state = CreateStateMessage(DualBoxMessageType.Sync, false);
        List<Peer> selected;
        List<Peer> deselected;

        lock (_gate)
        {
            if (_role != DualBoxRole.Master)
                return;

            ClearMasterTargetSyncLocked();
            state.Sequence = ++_stepSequence;
            state.MountSequence = ++_mountSequence;
            _lastMasterMounted = state.Mounted;
            selected = [];
            deselected = [];

            foreach (Peer peer in _peers.Where(p => p.Selected))
            {
                if (!IsCompatible(state, peer.State)
                    || !IsWithinSyncRange(state.X, state.Y, peer.State.X, peer.State.Y))
                {
                    peer.Selected = false;
                    peer.Ready = false;
                    deselected.Add(peer);
                    continue;
                }

                peer.Ready = false;
                peer.ExpectedSequence = state.Sequence;
                peer.MountReady = false;
                peer.ExpectedMounted = state.Mounted;
                peer.ExpectedMountSequence = state.MountSequence;
                selected.Add(peer);
            }

            state.GroupSerials = selected
                .Where(p => p.State != null)
                .Select(p => p.State.Serial)
                .Append(state.Serial)
                .Distinct()
                .ToArray();

            _status = selected.Count == 0
                ? $"No compatible client within {SyncRange} tiles."
                : reason;
        }

        foreach (Peer peer in deselected)
            Send(peer, new DualBoxMessage { Type = DualBoxMessageType.Stop });

        foreach (Peer peer in selected)
            Send(peer, state);
    }

    private DualBoxMessage CreateStateMessage(DualBoxMessageType type, bool ready)
    {
        World world = World.Instance;
        var result = new DualBoxMessage { Type = type, Ready = ready, Sequence = _clientSequence };

        if (world?.InGame != true || world.Player == null)
            return result;

        world.Player.GetEndPosition(out int x, out int y, out sbyte z, out Direction direction);
        result.ServerName = world.ServerName ?? string.Empty;
        result.MapIndex = world.MapIndex;
        result.Serial = world.Player.Serial;
        result.X = (ushort)x;
        result.Y = (ushort)y;
        result.Z = z;
        result.Direction = (byte)(direction & Direction.Mask);
        result.Mounted = IsMounted(world.Player);
        result.MountSequence = _clientMountSequence;
        return result;
    }

    private static bool IsMounted(PlayerMobile player)
        => player?.IsMounted == true;

    private void UpdateMasterReadyStatusLocked()
    {
        int selected = _peers.Count(p => p.Selected);
        int ready = _peers.Count(p => p.Selected && p.Ready && p.MountReady);
        _status = selected == 0
            ? $"{_peers.Count} client(s) connected."
            : ready == selected
                ? $"Synchronized: {ready}/{selected} client(s)."
                : $"Waiting for clients: {ready}/{selected} ready.";
    }

    private void SendClientState(DualBoxMessageType type, string error, long? sequence = null)
    {
        Peer peer;

        lock (_gate)
            peer = _masterPeer;

        if (peer == null)
            return;

        DualBoxMessage state = CreateStateMessage(type, _clientReady);
        state.Sequence = sequence ?? state.Sequence;
        state.Error = error;
        Send(peer, state);
    }

    private void Send(Peer peer, DualBoxMessage message)
    {
        CancellationToken token;

        lock (_gate)
            token = _cancellation?.Token ?? CancellationToken.None;

        _ = SendAsync(peer, message, token);
    }

    private static async Task SendAsync(Peer peer, DualBoxMessage message, CancellationToken token)
    {
        bool lockHeld = false;

        try
        {
            byte[] frame = DualBoxProtocol.Encode(message);
            await peer.SendLock.WaitAsync(token).ConfigureAwait(false);
            lockHeld = true;
            await peer.Stream.WriteAsync(frame, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not send dual-box message: {ex.Message}");
            peer.Client.Close();
        }
        finally
        {
            if (lockHeld)
                peer.SendLock.Release();
        }
    }

    private static bool IsUsableState(DualBoxMessage state)
        => state != null && state.MapIndex >= 0 && state.Serial != 0 && !string.IsNullOrEmpty(state.ServerName);

    private static bool IsCompatible(DualBoxMessage master, DualBoxMessage client)
        => IsUsableState(master)
            && IsUsableState(client)
            && master.MapIndex == client.MapIndex
            && string.Equals(master.ServerName, client.ServerName, StringComparison.OrdinalIgnoreCase);

    private void ResetClientState()
    {
        _pendingSteps.Clear();
        _groupSerials.Clear();
        _pendingTarget = null;
        _pendingTargetDeadline = 0;
        _pendingGumpResponses.Clear();
        _clientFollowing = false;
        _clientReady = false;
        _aligning = false;
        _alignmentPathStarted = false;
        _executingRemoteStep = false;
        _awaitingWalkSequence = null;
        _clientSequence = 0;
        _mountSequence = 0;
        _clientMountSequence = 0;
        _clientMacroSequence = 0;
        _clientActionSequence = 0;
        _clientGumpSequence = 0;
        _pendingMountSequence = 0;
        _lastMasterMounted = null;
        _expectedClientMounted = null;
        ResetClientMountAction();
    }

    private void SetStatus(string status)
    {
        lock (_gate)
            _status = status;
    }
}
