// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace ClassicUO.Network;

/// <summary>
/// Measures the round-trip time to the connected game server.
/// </summary>
/// <remarks>
/// The UO ping packet (0x73) is preferred because it travels through the game protocol. Some
/// shards, including official shards, do not always echo that packet, so an ICMP measurement to
/// the connected endpoint is kept as a fallback.
/// </remarks>
public sealed class PingManager
{
    private const int SAMPLE_COUNT = 5;
    private const int PROTOCOL_SEQUENCE_COUNT = byte.MaxValue + 1;
    private const uint PROTOCOL_PING_FRESHNESS_MS = 5_000;
    private const uint NETWORK_PING_INTERVAL_MS = 1_000;
    private const int NETWORK_PING_TIMEOUT_MS = 1_000;
    private const uint MAX_ACCEPTED_PING_MS = 10_000;

    private readonly object _sync = new();
    private readonly Func<bool> _isConnected;
    private readonly Action<byte> _sendProtocolPing;
    private readonly Func<IPAddress> _getRemoteAddress;
    private readonly Func<uint> _getTicks;
    private readonly Func<IPAddress, Task<uint?>> _sendNetworkPing;

    private readonly uint[] _protocolPings = new uint[SAMPLE_COUNT];
    private readonly uint[] _networkPings = new uint[SAMPLE_COUNT];
    private readonly uint[] _protocolPingSentAt = new uint[PROTOCOL_SEQUENCE_COUNT];
    private readonly bool[] _protocolPingPending = new bool[PROTOCOL_SEQUENCE_COUNT];

    private int _protocolPingCount;
    private int _protocolPingWriteIndex;
    private int _networkPingCount;
    private int _networkPingWriteIndex;
    private int _generation;
    private byte _sequence;
    private uint _lastProtocolPingReceived;
    private uint _nextNetworkPing;
    private bool _networkPingInFlight;

    public PingManager(AsyncNetClient socket)
        : this(
            () => socket?.IsConnected ?? false,
            sequence => socket?.Send_Ping(sequence),
            () => (socket?.RemoteEndPoint as IPEndPoint)?.Address,
            () => Time.Ticks,
            SendNetworkPingAsync)
    {
    }

    internal PingManager(
        Func<bool> isConnected,
        Action<byte> sendProtocolPing,
        Func<IPAddress> getRemoteAddress,
        Func<uint> getTicks,
        Func<IPAddress, Task<uint?>> sendNetworkPing)
    {
        _isConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
        _sendProtocolPing = sendProtocolPing ?? throw new ArgumentNullException(nameof(sendProtocolPing));
        _getRemoteAddress = getRemoteAddress ?? throw new ArgumentNullException(nameof(getRemoteAddress));
        _getTicks = getTicks ?? throw new ArgumentNullException(nameof(getTicks));
        _sendNetworkPing = sendNetworkPing ?? throw new ArgumentNullException(nameof(sendNetworkPing));
        _lastProtocolPingReceived = _getTicks();
    }

    /// <summary>
    /// Gets the rolling average RTT in milliseconds. A recent UO protocol measurement takes
    /// precedence over the ICMP fallback.
    /// </summary>
    public uint Ping
    {
        get
        {
            lock (_sync)
            {
                uint protocolPing = Average(_protocolPings, _protocolPingCount);
                uint networkPing = Average(_networkPings, _networkPingCount);

                if (
                    protocolPing != 0
                    && (networkPing == 0 || Elapsed(_getTicks(), _lastProtocolPingReceived) <= PROTOCOL_PING_FRESHNESS_MS)
                )
                {
                    return protocolPing;
                }

                return networkPing;
            }
        }
    }

    /// <summary>
    /// Gets the time at which the last UO protocol ping was received. ICMP replies intentionally
    /// do not update this value because it is also used to detect a hung game connection.
    /// </summary>
    public uint LastProtocolPingReceived
    {
        get
        {
            lock (_sync)
            {
                return _lastProtocolPingReceived;
            }
        }
    }

    /// <summary>
    /// Starts a UO protocol measurement and, when due, a non-blocking network fallback measurement.
    /// </summary>
    public void SendPing()
    {
        if (!_isConnected())
        {
            return;
        }

        byte sequence;
        IPAddress remoteAddress = null;
        int generation = 0;

        lock (_sync)
        {
            uint now = _getTicks();
            sequence = _sequence;
            _sequence = unchecked((byte)(_sequence + 1));
            _protocolPingSentAt[sequence] = now;
            _protocolPingPending[sequence] = true;

            bool protocolPingIsFresh =
                _protocolPingCount > 0
                && Elapsed(now, _lastProtocolPingReceived) <= PROTOCOL_PING_FRESHNESS_MS;

            if (
                !protocolPingIsFresh
                && !_networkPingInFlight
                && (_nextNetworkPing == 0 || IsDue(now, _nextNetworkPing))
            )
            {
                IPAddress candidateAddress = _getRemoteAddress();

                if (candidateAddress != null && !IPAddress.Any.Equals(candidateAddress) && !IPAddress.IPv6Any.Equals(candidateAddress))
                {
                    remoteAddress = candidateAddress;
                    _networkPingInFlight = true;
                    _nextNetworkPing = now + NETWORK_PING_INTERVAL_MS;
                    generation = _generation;
                }
            }
        }

        try
        {
            _sendProtocolPing(sequence);
        }
        catch
        {
            lock (_sync)
            {
                _protocolPingPending[sequence] = false;
            }
        }

        if (remoteAddress != null)
        {
            _ = MeasureNetworkPingAsync(remoteAddress, generation);
        }
    }

    /// <summary>
    /// Completes a UO protocol ping using the echoed sequence byte.
    /// </summary>
    /// <returns><see langword="true"/> when the sequence matched an outstanding ping.</returns>
    public bool ProtocolPingReceived(byte sequence)
    {
        int index = sequence;

        lock (_sync)
        {
            if (!_protocolPingPending[index])
            {
                return false;
            }

            _protocolPingPending[index] = false;
            uint now = _getTicks();
            uint roundTripTime = Elapsed(now, _protocolPingSentAt[index]);

            if (roundTripTime > MAX_ACCEPTED_PING_MS)
            {
                return false;
            }

            AddSample(_protocolPings, ref _protocolPingCount, ref _protocolPingWriteIndex, Math.Max(1, roundTripTime));
            _lastProtocolPingReceived = now;
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_protocolPings);
            Array.Clear(_networkPings);
            Array.Clear(_protocolPingSentAt);
            Array.Clear(_protocolPingPending);
            _protocolPingCount = 0;
            _protocolPingWriteIndex = 0;
            _networkPingCount = 0;
            _networkPingWriteIndex = 0;
            _sequence = 0;
            _lastProtocolPingReceived = _getTicks();
            _nextNetworkPing = 0;
            _generation++;
        }
    }

    private async Task MeasureNetworkPingAsync(IPAddress address, int generation)
    {
        uint? roundTripTime = null;

        try
        {
            roundTripTime = await _sendNetworkPing(address).ConfigureAwait(false);
        }
        catch
        {
            // A server or platform may reject ICMP. The UO protocol measurement remains active.
        }

        lock (_sync)
        {
            if (
                generation == _generation
                && roundTripTime.HasValue
                && roundTripTime.Value <= MAX_ACCEPTED_PING_MS
            )
            {
                AddSample(
                    _networkPings,
                    ref _networkPingCount,
                    ref _networkPingWriteIndex,
                    Math.Max(1, roundTripTime.Value)
                );
            }

            _networkPingInFlight = false;
        }
    }

    private static async Task<uint?> SendNetworkPingAsync(IPAddress address)
    {
        using var ping = new System.Net.NetworkInformation.Ping();
        PingReply reply = await ping.SendPingAsync(address, NETWORK_PING_TIMEOUT_MS).ConfigureAwait(false);

        if (reply.Status != IPStatus.Success)
        {
            return null;
        }

        return (uint)Math.Min(reply.RoundtripTime, uint.MaxValue);
    }

    private static void AddSample(uint[] samples, ref int count, ref int writeIndex, uint value)
    {
        samples[writeIndex] = value;
        writeIndex = (writeIndex + 1) % samples.Length;

        if (count < samples.Length)
        {
            count++;
        }
    }

    private static uint Average(uint[] samples, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        ulong total = 0;

        for (int i = 0; i < count; i++)
        {
            total += samples[i];
        }

        return (uint)(total / (uint)count);
    }

    private static uint Elapsed(uint now, uint then) => unchecked(now - then);

    private static bool IsDue(uint now, uint dueAt) => unchecked((int)(now - dueAt)) >= 0;
}
