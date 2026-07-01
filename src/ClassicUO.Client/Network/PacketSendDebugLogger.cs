using ClassicUO.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClassicUO.Network
{
    internal static class PacketSendDebugLogger
    {
        private const int RecentLimit = 256;
        private static readonly object _lock = new object();
        private static readonly Queue<PacketRecord> _recentPackets = new Queue<PacketRecord>();
        private static readonly Queue<PacketStamp> _packetStamps = new Queue<PacketStamp>();
        private static string _logPath;

        public static void LogQueuedPacket(Span<byte> message, bool ignorePlugin, bool skipEncryption, int queuedBytes)
        {
            if (message.IsEmpty)
            {
                return;
            }

            PacketRecord record = new PacketRecord
            {
                Time = DateTimeOffset.Now,
                PacketId = message[0],
                Length = message.Length,
                IgnorePlugin = ignorePlugin,
                SkipEncryption = skipEncryption,
                QueuedBytesBeforeEnqueue = queuedBytes,
                Head = ToHex(message, Math.Min(message.Length, 32))
            };

            lock (_lock)
            {
                Trim(record.Time);
                _recentPackets.Enqueue(record);
                _packetStamps.Enqueue(new PacketStamp { Time = record.Time, PacketId = record.PacketId });

                while (_recentPackets.Count > RecentLimit)
                {
                    _recentPackets.Dequeue();
                }

                AppendLine(
                    $"{Stamp(record.Time)} QUEUE id=0x{record.PacketId:X2} len={record.Length} " +
                    $"queuedBefore={record.QueuedBytesBeforeEnqueue} last1s={CountSince(record.Time, 1)} " +
                    $"last10s={CountSince(record.Time, 10)} last60s={CountSince(record.Time, 60)} " +
                    $"top60={TopIds(record.Time, 60)} ignorePlugin={record.IgnorePlugin} " +
                    $"skipEncryption={record.SkipEncryption} head={record.Head}"
                );
            }
        }

        public static void LogSocketWrite(int bytesToSend, int queuedBytesRemaining)
        {
            lock (_lock)
            {
                AppendLine(
                    $"{Stamp(DateTimeOffset.Now)} WRITE bytes={bytesToSend} queuedRemaining={queuedBytesRemaining}"
                );
            }
        }

        public static void LogDisconnect(string reason, SocketError? socketError = null, Exception exception = null)
        {
            lock (_lock)
            {
                DateTimeOffset now = DateTimeOffset.Now;
                Trim(now);

                AppendLine("");
                AppendLine(
                    $"{Stamp(now)} DISCONNECT reason={reason} socketError={socketError?.ToString() ?? "none"} " +
                    $"last1s={CountSince(now, 1)} last10s={CountSince(now, 10)} last60s={CountSince(now, 60)} " +
                    $"top60={TopIds(now, 60)}"
                );

                if (exception != null)
                {
                    AppendLine($"{Stamp(now)} DISCONNECT_EXCEPTION {exception}");
                }

                AppendLine($"{Stamp(now)} RECENT_PACKETS_BEGIN count={_recentPackets.Count}");

                foreach (PacketRecord record in _recentPackets)
                {
                    AppendLine(
                        $"{Stamp(record.Time)} RECENT id=0x{record.PacketId:X2} len={record.Length} " +
                        $"queuedBefore={record.QueuedBytesBeforeEnqueue} ignorePlugin={record.IgnorePlugin} " +
                        $"skipEncryption={record.SkipEncryption} head={record.Head}"
                    );
                }

                AppendLine($"{Stamp(now)} RECENT_PACKETS_END");
                AppendLine("");
            }
        }

        private static void Trim(DateTimeOffset now)
        {
            while (_packetStamps.Count != 0 && (now - _packetStamps.Peek().Time).TotalSeconds > 60)
            {
                _packetStamps.Dequeue();
            }
        }

        private static int CountSince(DateTimeOffset now, int seconds)
        {
            return _packetStamps.Count(stamp => (now - stamp.Time).TotalSeconds <= seconds);
        }

        private static string TopIds(DateTimeOffset now, int seconds)
        {
            return string.Join(
                ",",
                _packetStamps
                    .Where(stamp => (now - stamp.Time).TotalSeconds <= seconds)
                    .GroupBy(stamp => stamp.PacketId)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Take(8)
                    .Select(group => $"0x{group.Key:X2}:{group.Count()}")
            );
        }

        private static string ToHex(Span<byte> bytes, int length)
        {
            StringBuilder builder = new StringBuilder(length * 3);

            for (int i = 0; i < length; i++)
            {
                if (i != 0)
                {
                    builder.Append(' ');
                }

                builder.Append(bytes[i].ToString("X2"));
            }

            return builder.ToString();
        }

        private static string Stamp(DateTimeOffset time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        }

        private static void AppendLine(string line)
        {
            try
            {
                string path = GetLogPath();
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch
            {
                // This logger is diagnostic only. It must never affect gameplay/network flow.
            }
        }

        private static string GetLogPath()
        {
            if (_logPath == null)
            {
                string directory = FileSystemHelper.CreateFolderIfNotExists(
                    CUOEnviroment.ExecutablePath,
                    "Logs",
                    "Network"
                );
                _logPath = Path.Combine(directory, "packet-send-debug.log");
            }

            return _logPath;
        }

        private struct PacketStamp
        {
            public DateTimeOffset Time;
            public byte PacketId;
        }

        private struct PacketRecord
        {
            public DateTimeOffset Time;
            public byte PacketId;
            public int Length;
            public bool IgnorePlugin;
            public bool SkipEncryption;
            public int QueuedBytesBeforeEnqueue;
            public string Head;
        }
    }
}
