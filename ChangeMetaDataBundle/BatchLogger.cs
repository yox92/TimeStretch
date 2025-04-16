using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChangeMetaDataBundle
{
    public enum EnumLoggerMode
    {
        DirectWrite,
        MemoryBuffer
    }

    public static class BatchLogger
    {
        private static EnumLoggerMode _mode = EnumLoggerMode.DirectWrite;
        private static readonly object WriteLock = new();
        private static string _logFilePath = PathsFile.LogFilePath;
        private static readonly List<string> PackedLogs = new();
        private static readonly Dictionary<string, (string Block, int Count)> DeduplicationMap = new();
        private static bool DebugOnly { get; set; } = false;
        private static CancellationTokenSource? _flushCts;

        private static bool LoadDebugMode()
        {
            var configPath = PathsFile.DebugPath;

            try
            {
                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, "false");
                    return false;
                }

                var content = File.ReadAllText(configPath).Trim();
                return content.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void Init(EnumLoggerMode mode = EnumLoggerMode.DirectWrite)
        {
            _mode = mode;
            DebugOnly = LoadDebugMode();

            lock (WriteLock)
            {
                File.WriteAllText(_logFilePath, "");
            }

            if (_mode == EnumLoggerMode.MemoryBuffer)
            {
                _flushCts = new CancellationTokenSource();
                _ = StartAutoFlushAsync(_flushCts.Token);
            }
        }

        public static void Info(string message) => Write("ℹ️", message);
        public static void Warn(string message) => Write("⚠️", message);
        public static void Error(string message) => Write("❌", message);
        public static void Log(string message) => Write("", message);

        public static void LogException(Exception ex, string? context = null)
        {
            if (!DebugOnly) return;

            if (!string.IsNullOrWhiteSpace(context))
                Write("💥", $"Exception dans {context}");

            Write("🤮", $"Exception : {ex.Message}");
            Write("🩵", $"StackTrace : {ex.StackTrace}");
        }

        public static void Section(string title)
        {
            if (!DebugOnly) return;
            Write("🔷", $"========== {title} ==========");
        }

        private static void Write(string prefix, string message)
        {
            if (!DebugOnly) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var line = $"[{timestamp}] {prefix} {message}".Trim();

            lock (WriteLock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }

        public static void Block(IEnumerable<string> lines)
        {
            if (!DebugOnly) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var finalLines = lines.Select(line => $"[{timestamp}] {line}");

            lock (WriteLock)
            {
                File.AppendAllLines(_logFilePath, finalLines);
            }
        }

        public static void FlushClipLog(IEnumerable<string> lines, string clipName) =>
            FlushLabeledBlock(lines, $"AudioClip: {clipName}");

        public static void FlushReplacementLog(IEnumerable<string> lines, string label) =>
            FlushLabeledBlock(lines, label);

        private static void FlushLabeledBlock(IEnumerable<string> lines, string label)
        {
            if (!DebugOnly) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var blockHeader = $"🔊🟩 START {label} ({timestamp})";
            var blockFooter = $"🔊🔴 END {label}";

            var sb = new StringBuilder();
            var sbForHash = new StringBuilder();

            sb.AppendLine(blockHeader);
            sbForHash.AppendLine($"START {label}");

            foreach (var line in lines)
            {
                var ts = DateTime.Now.ToString("HH:mm:ss.fff");
                sb.AppendLine($"[{ts}] {line}");
                sbForHash.AppendLine(line);
            }

            sb.AppendLine(blockFooter);
            sbForHash.AppendLine($"END {label}");

            var blockString = sb.ToString();
            var hashString = sbForHash.ToString();
            var hash = hashString.GetHashCode().ToString();

            lock (WriteLock)
            {
                if (_mode == EnumLoggerMode.MemoryBuffer)
                {
                    if (DeduplicationMap.TryGetValue(hash, out var existing))
                        DeduplicationMap[hash] = (existing.Block, existing.Count + 1);
                    else
                        DeduplicationMap[hash] = (blockString, 1);
                }
                else
                {
                    File.AppendAllText(_logFilePath, blockString);
                }
            }
        }

        private static async Task StartAutoFlushAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);

                lock (WriteLock)
                {
                    if (PackedLogs.Count > 0)
                    {
                        File.AppendAllText(_logFilePath, string.Join(Environment.NewLine, PackedLogs));
                        PackedLogs.Clear();
                    }

                    if (DeduplicationMap.Count > 0)
                    {
                        FlushDeduplicatedLogsToDisk();
                    }
                }
            }
        }

        private static void FlushDeduplicatedLogsToDisk()
        {
            if (!DebugOnly) return;

            var sb = new StringBuilder();
            foreach (var (_, (block, count)) in DeduplicationMap)
            {
                if (count > 1)
                {
                    var insertPos = block.LastIndexOf("🔊🔴", StringComparison.Ordinal);
                    var mergedBlock = block.Insert(insertPos, $"  🔁 Répété {count} fois\n");
                    sb.AppendLine(mergedBlock);
                }
                else
                {
                    sb.AppendLine(block);
                }
            }

            File.AppendAllText(_logFilePath, sb.ToString());
            DeduplicationMap.Clear();
        }

        public static void StopAutoFlush()
        {
            _flushCts?.Cancel();
        }

        public static void OnApplicationQuit()
        {
            if (!DebugOnly) return;
            FlushPackedLogsToDisk();
            FlushDeduplicatedLogsToDisk();
        }

        private static void FlushPackedLogsToDisk()
        {
            if (!DebugOnly) return;

            lock (WriteLock)
            {
                if (PackedLogs.Count == 0) return;

                File.AppendAllText(_logFilePath, string.Join(Environment.NewLine, PackedLogs));
                PackedLogs.Clear();
            }
        }
    }
}
