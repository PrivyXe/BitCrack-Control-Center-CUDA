using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BitCrackGUI
{
    public class ScanStatusEventArgs : EventArgs
    {
        public string SpeedText { get; set; } = "0 MKey/s";
        public double SpeedMKey { get; set; }
        public string TotalKeysText { get; set; } = "0";
        public string ElapsedTimeText { get; set; } = "00:00:00";
        public string RawLine { get; set; } = "";
    }

    public class KeyFoundEventArgs : EventArgs
    {
        public string Address { get; set; } = "";
        public string PrivateKey { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public bool Compressed { get; set; }
    }

    public enum EngineType
    {
        cuBitCrack,
        RCKangaroo
    }

    public class BitCrackConfig
    {
        public EngineType Engine { get; set; } = EngineType.cuBitCrack;
        public string ExecutablePath { get; set; } = "";
        public string RCKangarooPath { get; set; } = "";
        public int DeviceId { get; set; } = 0;
        public int Blocks { get; set; } = 128;
        public int Threads { get; set; } = 256;
        public int PointsPerThread { get; set; } = 32;
        public string CompressionMode { get; set; } = "compressed"; // compressed, uncompressed, both
        public string TargetAddress { get; set; } = "";
        public string TargetsFile { get; set; } = "";
        public string ResultsFile { get; set; } = "";
        public string KeyspaceStart { get; set; } = "";
        public string KeyspaceEnd { get; set; } = "";
        public string Stride { get; set; } = "";
        public bool EnableCheckpoint { get; set; } = false;
        public string CheckpointFile { get; set; } = "";
        public bool IsKangarooMode { get; set; } = false;
        public int DpBits { get; set; } = 16;
        public int RangeBits { get; set; } = 0;
        public string PublicKeyHex { get; set; } = "";
        public string TamesFile { get; set; } = "";
    }

    public class BitCrackRunner
    {
        private Process? _process;
        private bool _isScanning;

        public event EventHandler<ScanStatusEventArgs>? StatusUpdated;
        public event EventHandler<KeyFoundEventArgs>? KeyFound;
        public event EventHandler<string>? LogOutput;
        public event EventHandler<int>? ScanFinished;

        public bool IsScanning => _isScanning;

        public Task StartAsync(BitCrackConfig config)
        {
            if (_isScanning) return Task.CompletedTask;

            string exePath = config.ExecutablePath;

            if (config.Engine == EngineType.RCKangaroo)
            {
                exePath = FindRCKangarooExecutable(config.RCKangarooPath);
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    LogOutput?.Invoke(this, $"[Hata] RCKangaroo.exe bulunamadı. Lütfen RCKangaroo_v31 klasöründeki derlenmiş exe dosyasını kontrol edin.");
                    return Task.CompletedTask;
                }
            }
            else
            {
                if (!File.Exists(exePath))
                {
                    LogOutput?.Invoke(this, $"[Hata] Executable bulunamadı: {exePath}");
                    return Task.CompletedTask;
                }

                if (config.IsKangarooMode)
                {
                    if (!SupportsKangarooMode(exePath, out string validationError))
                    {
                        LogOutput?.Invoke(this, $"[Kangaroo Uyarısı] Backend doğrulanamadı ({validationError}). Auto-repair deneniyor...");
                        string activePath = exePath;
                        if (TryAutoRepairExecutable(ref activePath, msg => LogOutput?.Invoke(this, msg)) && SupportsKangarooMode(activePath, out _))
                        {
                            exePath = activePath;
                            config.ExecutablePath = activePath;
                            LogOutput?.Invoke(this, $"[Kangaroo Başarılı] Güncellenmiş backend doğrulandı: {exePath}");
                        }
                        else
                        {
                            LogOutput?.Invoke(this, $"[Kangaroo Hatası] {validationError}");
                            LogOutput?.Invoke(this, $"[Kangaroo Hatası] Seçilen backend: {exePath}");
                            LogOutput?.Invoke(this, $"[Kangaroo Hatası] İşlem iptal edildi. Lütfen x64\\Release\\cuBitCrack.exe projesini yeniden derleyin.");
                            return Task.CompletedTask;
                        }
                    }
                }
            }

            var args = new StringBuilder();

            if (config.Engine == EngineType.RCKangaroo)
            {
                args.Append($"-gpu {config.DeviceId} ");
                args.Append($"-dp {config.DpBits} ");

                int range = config.RangeBits;
                if (range <= 0) range = CalculateRangeFromStartEnd(config.KeyspaceStart, config.KeyspaceEnd);
                if (range <= 0) range = 66; // Default fallback

                if (range > 170)
                {
                    LogOutput?.Invoke(this, $"[Hata] RCKangaroo motoru maksimum 170-bit aralık destekler (Seçilen: {range}-bit). Lütfen 256-bit aramalar için 'Standard BitCrack Engine' (Lineer Mod) kullanın.");
                    return Task.CompletedTask;
                }

                args.Append($"-range {range} ");

                if (!string.IsNullOrWhiteSpace(config.KeyspaceStart))
                {
                    args.Append($"-start {config.KeyspaceStart} ");
                }

                if (!string.IsNullOrWhiteSpace(config.PublicKeyHex))
                {
                    args.Append($"-pubkey {config.PublicKeyHex} ");
                }

                if (!string.IsNullOrWhiteSpace(config.TamesFile))
                {
                    args.Append($"-tames \"{config.TamesFile}\" ");
                }
            }
            else
            {
                // Always add -f (--follow) so status updates emit newlines (\n) and stream live to GUI pipe
                args.Append("-f ");

                // Device
                args.Append($"-d {config.DeviceId} ");

                // Kangaroo Mode & DP Bits
                if (config.IsKangarooMode)
                {
                    args.Append($"-k -dp {config.DpBits} ");
                    if (!string.IsNullOrWhiteSpace(config.PublicKeyHex))
                    {
                        args.Append($"--pubkey {config.PublicKeyHex} ");
                    }
                }

                // Geometry
                if (config.Blocks > 0) args.Append($"-b {config.Blocks} ");
                if (config.Threads > 0) args.Append($"-t {config.Threads} ");
                if (config.PointsPerThread > 0) args.Append($"-p {config.PointsPerThread} ");

                // Compression
                if (config.CompressionMode == "compressed") args.Append("-c ");
                else if (config.CompressionMode == "uncompressed") args.Append("-u ");
                else args.Append("--compression both ");

                // Keyspace
                if (!string.IsNullOrWhiteSpace(config.KeyspaceStart))
                {
                    if (!string.IsNullOrWhiteSpace(config.KeyspaceEnd))
                    {
                        args.Append($"--keyspace {config.KeyspaceStart}:{config.KeyspaceEnd} ");
                    }
                    else
                    {
                        args.Append($"--keyspace {config.KeyspaceStart} ");
                    }
                }

                // Stride
                if (!string.IsNullOrWhiteSpace(config.Stride))
                {
                    args.Append($"--stride {config.Stride} ");
                }

                // Checkpoint
                if (config.EnableCheckpoint && !string.IsNullOrWhiteSpace(config.CheckpointFile))
                {
                    args.Append($"--continue \"{config.CheckpointFile}\" ");
                }

                // Output file
                if (!string.IsNullOrWhiteSpace(config.ResultsFile))
                {
                    args.Append($"-o \"{config.ResultsFile}\" ");
                }

                // Targets file or single target
                if (!string.IsNullOrWhiteSpace(config.TargetsFile) && File.Exists(config.TargetsFile))
                {
                    args.Append($"-i \"{config.TargetsFile}\" ");
                }
                else if (!string.IsNullOrWhiteSpace(config.TargetAddress))
                {
                    args.Append($"\"{config.TargetAddress}\" ");
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args.ToString().Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            LogOutput?.Invoke(this, $"[Süreç Başlatılıyor - {config.Engine}] {psi.FileName} {psi.Arguments}");

            try
            {
                _process = new Process { StartInfo = psi };
                _process.EnableRaisingEvents = true;

                _process.OutputDataReceived += OnDataReceived;
                _process.ErrorDataReceived += OnDataReceived;

                _process.Exited += (s, e) =>
                {
                    int exitCode = _process?.ExitCode ?? 0;
                    _isScanning = false;
                    LogOutput?.Invoke(this, $"[Süreç Bitti] Exit Code: {exitCode}");
                    ScanFinished?.Invoke(this, exitCode);
                };

                _isScanning = _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _isScanning = false;
                LogOutput?.Invoke(this, $"[Başlatma Hatası] {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public static string FindRCKangarooExecutable(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "RCKangaroo.exe"),
                Path.Combine(baseDir, "RCKangaroo_v31", "RCKangaroo.exe"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "RCKangaroo_v31", "RCKangaroo.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "RCKangaroo_v31", "RCKangaroo.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "RCKangaroo_v31", "src", "x64", "Release", "RCKangaroo.exe"))
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return "";
        }

        public static int CalculateRangeFromStartEnd(string startHex, string endHex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(endHex))
                {
                    var endVal = System.Numerics.BigInteger.Parse("0" + endHex.Trim(), System.Globalization.NumberStyles.HexNumber);
                    if (endVal > System.Numerics.BigInteger.Zero)
                    {
                        return (int)endVal.GetBitLength();
                    }
                }
                if (!string.IsNullOrWhiteSpace(startHex))
                {
                    var startVal = System.Numerics.BigInteger.Parse("0" + startHex.Trim(), System.Globalization.NumberStyles.HexNumber);
                    if (startVal > System.Numerics.BigInteger.Zero)
                    {
                        return (int)startVal.GetBitLength() + 1;
                    }
                }
            }
            catch { }
            return 0;
        }

        public static bool SupportsKangarooMode(string executablePath, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                error = "Backend dosyası bulunamadı.";
                return false;
            }

            try
            {
                using var probe = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = "--help",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                if (!probe.Start())
                {
                    error = "Backend yetenek kontrolü başlatılamadı.";
                    return false;
                }

                string output = probe.StandardOutput.ReadToEnd();
                string stderr = probe.StandardError.ReadToEnd();

                if (!probe.WaitForExit(5000))
                {
                    probe.Kill(true);
                    error = "Backend --help kontrolüne 5 saniye içinde yanıt vermedi.";
                    return false;
                }

                string helpText = output + stderr;
                bool hasKangaroo = helpText.Contains("--kangaroo", StringComparison.OrdinalIgnoreCase) || helpText.Contains("-k,", StringComparison.OrdinalIgnoreCase) || helpText.Contains("-k ", StringComparison.Ordinal);
                bool hasPubkey = helpText.Contains("--pubkey", StringComparison.OrdinalIgnoreCase);

                if (!hasKangaroo || !hasPubkey)
                {
                    error = "Seçilen cuBitCrack.exe Kangaroo desteği içermiyor. Native CUDA projesini yeniden derleyin ve GUI çıktı klasöründeki eski kopyayı güncelleyin.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Backend yetenek kontrolü başarısız: {ex.Message}";
                return false;
            }
        }

        public static bool TryAutoRepairExecutable(ref string activePath, Action<string>? logCallback = null)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = Path.Combine(baseDir, "cuBitCrack.exe");

            string[] candidatePaths = new string[]
            {
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "x64", "Release", "cuBitCrack.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "x64", "Release", "cuBitCrack.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "x64", "Debug", "cuBitCrack.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "x64", "Debug", "cuBitCrack.exe"))
            };

            foreach (var cand in candidatePaths)
            {
                if (File.Exists(cand) && SupportsKangarooMode(cand, out _))
                {
                    try
                    {
                        if (!string.Equals(Path.GetFullPath(cand), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(cand, targetPath, overwrite: true);
                            logCallback?.Invoke($"[Kangaroo Auto-Repair] Güncel backend kopyalandı: {cand} -> {targetPath}");
                        }
                        activePath = targetPath;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logCallback?.Invoke($"[Kangaroo Auto-Repair Uyarısı] {ex.Message}");
                        activePath = cand;
                        return true;
                    }
                }
            }

            return false;
        }

        public void Stop()
        {
            if (!_isScanning || _process == null) return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                LogOutput?.Invoke(this, $"[Durdurma Hatası] {ex.Message}");
            }
            finally
            {
                _isScanning = false;
            }
        }

        private KeyFoundEventArgs? _currentFoundKey;

        private void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            string line = e.Data.Trim();
            LogOutput?.Invoke(this, line);

            // Parse cuBitCrack status update (e.g., "NVIDIA GeForce RTX... | 1 target 450.25 MKey/s (1,048,576 total) [00:00:05]")
            var statusMatch = Regex.Match(line, @"([<>\d\.\s]+[GMK]?Key/s)\s*\(([\d,]+)\s*total\)\s*\[([\d:]+)\]");
            if (statusMatch.Success)
            {
                string speedStr = statusMatch.Groups[1].Value.Trim();
                string totalStr = statusMatch.Groups[2].Value;
                string timeStr = statusMatch.Groups[3].Value;

                double speedMKey = 0;
                var speedNumMatch = Regex.Match(speedStr, @"([\d\.]+)");
                if (speedNumMatch.Success && double.TryParse(speedNumMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    if (speedStr.Contains("GKey/s", StringComparison.OrdinalIgnoreCase)) speedMKey = val * 1000.0;
                    else if (speedStr.Contains("KKey/s", StringComparison.OrdinalIgnoreCase)) speedMKey = val / 1000.0;
                    else speedMKey = val;
                }

                StatusUpdated?.Invoke(this, new ScanStatusEventArgs
                {
                    SpeedText = speedStr,
                    SpeedMKey = speedMKey,
                    TotalKeysText = totalStr,
                    ElapsedTimeText = timeStr,
                    RawLine = line
                });
                return;
            }

            // Parse RCKangaroo status update (e.g., "MAIN: Speed: 1050 MKeys/s, Err: 0, DPs: 120K/500K, Time: 0d:00h:01m/0d:00h:15m")
            var rcMatch = Regex.Match(line, @"Speed:\s*([\d\.]+)\s*([GMK]?Keys?/s)", RegexOptions.IgnoreCase);
            if (rcMatch.Success)
            {
                double val = double.Parse(rcMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                string unit = rcMatch.Groups[2].Value;
                double speedMKey = val;
                if (unit.StartsWith("G", StringComparison.OrdinalIgnoreCase)) speedMKey = val * 1000.0;
                else if (unit.StartsWith("K", StringComparison.OrdinalIgnoreCase)) speedMKey = val / 1000.0;

                string speedStr = speedMKey >= 1000.0 ? $"{(speedMKey / 1000.0):F2} GKey/s" : $"{speedMKey:F2} MKey/s";
                string dpsStr = "0 DPs";
                var dpsMatch = Regex.Match(line, @"DPs:\s*([^\s,]+)");
                if (dpsMatch.Success)
                {
                    string rawDps = dpsMatch.Groups[1].Value;
                    var parts = rawDps.Split('/');
                    if (parts.Length == 2)
                    {
                        string current = parts[0].Trim();
                        string targetStr = parts[1].Trim();
                        if (targetStr.EndsWith("K", StringComparison.OrdinalIgnoreCase) && double.TryParse(targetStr.Substring(0, targetStr.Length - 1), System.Globalization.CultureInfo.InvariantCulture, out double targetK))
                        {
                            if (targetK >= 1000000.0) targetStr = $"{(targetK / 1000000.0):F1}B";
                            else if (targetK >= 1000.0) targetStr = $"{(targetK / 1000.0):F1}M";
                            else targetStr = $"{targetK:F0}K";
                        }
                        dpsStr = $"{current} / {targetStr} DPs";
                    }
                    else
                    {
                        dpsStr = rawDps;
                    }
                }

                string timeStr = "00:00:00";
                var timeMatch = Regex.Match(line, @"Time:\s*([^\s,\r\n]+)");
                if (timeMatch.Success)
                {
                    string rawTime = timeMatch.Groups[1].Value;
                    var timeParts = rawTime.Split('/');
                    if (timeParts.Length >= 1)
                    {
                        timeStr = timeParts[0].Replace("d:", "d ").Replace("h:", ":").Replace("m", "");
                    }
                    else
                    {
                        timeStr = rawTime;
                    }
                }

                StatusUpdated?.Invoke(this, new ScanStatusEventArgs
                {
                    SpeedText = speedStr,
                    SpeedMKey = speedMKey,
                    TotalKeysText = dpsStr,
                    ElapsedTimeText = timeStr,
                    RawLine = line
                });
                return;
            }

            // Parse RCKangaroo private key result (e.g., "PRIVATE KEY: 4000...1234")
            var rcKeyMatch = Regex.Match(line, @"PRIVATE KEY:\s*([0-9A-Fa-f]+)", RegexOptions.IgnoreCase);
            if (rcKeyMatch.Success)
            {
                string privHex = rcKeyMatch.Groups[1].Value.Trim();
                KeyFound?.Invoke(this, new KeyFoundEventArgs
                {
                    Address = "RCKangaroo Solved Target",
                    PrivateKey = privHex,
                    PublicKey = "Solana / Bitcoin EC Point",
                    Compressed = true
                });
                return;
            }

            // Strip timestamp and log level prefix (e.g., "[2026-07-20.01:48:15] [Info] ")
            string cleanLine = Regex.Replace(line, @"^\[.*?\]\s*(\[Info\]|\[Error\]|\[Warning\])?\s*", "").Trim();

            // Parse Address: ... Private key: ...
            if (cleanLine.StartsWith("Address:"))
            {
                _currentFoundKey = new KeyFoundEventArgs();
                _currentFoundKey.Address = cleanLine.Replace("Address:", "").Trim();
            }
            else if (cleanLine.StartsWith("Private key:") && _currentFoundKey != null)
            {
                _currentFoundKey.PrivateKey = cleanLine.Replace("Private key:", "").Trim();
            }
            else if (cleanLine.StartsWith("Compressed:") && _currentFoundKey != null)
            {
                _currentFoundKey.Compressed = cleanLine.Contains("yes");
            }
            else if (cleanLine.StartsWith("Public key:") && _currentFoundKey != null)
            {
                // Public key label
            }
            else if (_currentFoundKey != null && !string.IsNullOrWhiteSpace(_currentFoundKey.PrivateKey))
            {
                if (string.IsNullOrEmpty(_currentFoundKey.PublicKey))
                {
                    _currentFoundKey.PublicKey = cleanLine.Trim();
                    KeyFound?.Invoke(this, _currentFoundKey);
                    _currentFoundKey = null;
                }
            }
        }
    }
}
