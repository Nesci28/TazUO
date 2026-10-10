// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using NetSocket = System.Net.Sockets.Socket;

namespace ClassicUO.Network;

/// <summary>
/// Reads the RTT maintained by the operating system for an established TCP socket.
/// </summary>
internal static class TcpRoundTripTime
{
    // Darwin: IPPROTO_TCP / TCP_CONNECTION_INFO / tcp_connection_info.tcpi_srtt (milliseconds).
    private const int MACOS_TCP_CONNECTION_INFO = 0x106;
    private const int MACOS_TCP_CONNECTION_INFO_SIZE = 112;
    private const int MACOS_TCP_SMOOTHED_RTT_OFFSET = 44;

    // Linux: IPPROTO_TCP / TCP_INFO / tcp_info.tcpi_rtt (microseconds).
    private const int LINUX_TCP_INFO = 11;
    private const int LINUX_TCP_INFO_SIZE = 256;
    private const int LINUX_TCP_RTT_OFFSET = 68;

    // Windows: SIO_TCP_INFO with a DWORD version 0 / TCP_INFO_v0.RttUs (microseconds).
    private const int WINDOWS_SIO_TCP_INFO = unchecked((int)0xD8000027);
    private const int WINDOWS_TCP_INFO_V0_SIZE = 88;
    private const int WINDOWS_TCP_RTT_OFFSET = 20;

    public static uint? Get(NetSocket socket)
    {
        if (socket == null || !socket.Connected)
        {
            return null;
        }

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                return GetMacOs(socket);
            }

            if (OperatingSystem.IsLinux())
            {
                return GetLinux(socket);
            }

            if (OperatingSystem.IsWindows())
            {
                return GetWindows(socket);
            }
        }
        catch
        {
            // TCP statistics are optional; PingManager will continue with UO and ICMP probes.
        }

        return null;
    }

    private static uint? GetMacOs(NetSocket socket)
    {
        Span<byte> connectionInfo = stackalloc byte[MACOS_TCP_CONNECTION_INFO_SIZE];
        int bytesWritten = socket.GetRawSocketOption(
            (int)SocketOptionLevel.Tcp,
            MACOS_TCP_CONNECTION_INFO,
            connectionInfo
        );

        if (bytesWritten < MACOS_TCP_SMOOTHED_RTT_OFFSET + sizeof(uint))
        {
            return null;
        }

        uint milliseconds = ReadNativeUInt32(
            connectionInfo.Slice(MACOS_TCP_SMOOTHED_RTT_OFFSET, sizeof(uint))
        );

        return milliseconds == 0 ? null : milliseconds;
    }

    private static uint? GetLinux(NetSocket socket)
    {
        Span<byte> tcpInfo = stackalloc byte[LINUX_TCP_INFO_SIZE];
        int bytesWritten = socket.GetRawSocketOption(
            (int)SocketOptionLevel.Tcp,
            LINUX_TCP_INFO,
            tcpInfo
        );

        if (bytesWritten < LINUX_TCP_RTT_OFFSET + sizeof(uint))
        {
            return null;
        }

        uint microseconds = ReadNativeUInt32(tcpInfo.Slice(LINUX_TCP_RTT_OFFSET, sizeof(uint)));
        return MicrosecondsToMilliseconds(microseconds);
    }

    private static uint? GetWindows(NetSocket socket)
    {
        byte[] version = new byte[sizeof(uint)];
        byte[] tcpInfo = new byte[WINDOWS_TCP_INFO_V0_SIZE];
        int bytesWritten = socket.IOControl(WINDOWS_SIO_TCP_INFO, version, tcpInfo);

        if (bytesWritten < WINDOWS_TCP_RTT_OFFSET + sizeof(uint))
        {
            return null;
        }

        uint microseconds = ReadNativeUInt32(
            tcpInfo.AsSpan(WINDOWS_TCP_RTT_OFFSET, sizeof(uint))
        );

        return MicrosecondsToMilliseconds(microseconds);
    }

    private static uint? MicrosecondsToMilliseconds(uint microseconds)
    {
        if (microseconds == 0)
        {
            return null;
        }

        return (uint)Math.Max(1, (microseconds + 500L) / 1_000L);
    }

    private static uint ReadNativeUInt32(ReadOnlySpan<byte> value) =>
        BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(value)
            : BinaryPrimitives.ReadUInt32BigEndian(value);
}
