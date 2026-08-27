using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace BitCrackGUI
{
    public partial class MainWindow : Window
    {
        private readonly BitCrackRunner _runner = new();
        private string _executablePath = "";
        private string _recoveredPubKey = "";
        private string _detectedGpuName = "GPU";

        public MainWindow()
        {
            InitializeComponent();

            // Locate newest cuBitCrack.exe and ensure Kangaroo capability
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localExe = Path.Combine(baseDir, "cuBitCrack.exe");
            string resolvedPath = localExe;

            if (!BitCrackRunner.SupportsKangarooMode(localExe, out _))
            {
                BitCrackRunner.TryAutoRepairExecutable(ref resolvedPath);
            }

            _executablePath = File.Exists(resolvedPath) ? resolvedPath : localExe;

            // Hook runner events
            _runner.StatusUpdated += Runner_StatusUpdated;
            _runner.KeyFound += Runner_KeyFound;
            _runner.LogOutput += Runner_LogOutput;
            _runner.ScanFinished += Runner_ScanFinished;

            // Set default checkpoint and results output paths to app directory
            TxtCheckpointFile.Text = Path.Combine(baseDir, "progress.ci");
            TxtResultsFile.Text = Path.Combine(baseDir, "found_keys.txt");

            // Populate Puzzle Selector ComboBox (#1 to #256)
            if (CboPuzzleSelect != null)
            {
                for (int i = 1; i <= 256; i++)
                {
                    CboPuzzleSelect.Items.Add($"Puzzle #{i}");
                }
                CboPuzzleSelect.SelectedIndex = 39; // Default Puzzle #40
            }

            // Detect GPU Hardware
            DetectGpu();
        }

        private void DetectGpu()
        {
            var devices = GpuDetector.DetectDevices(_executablePath);
            if (devices.Count > 0)
            {
                var gpu = devices[0];
                _detectedGpuName = gpu.Name;
                TxtGpuName.Text = gpu.Name;
                TxtGpuMemory.Text = gpu.Memory;
                TxtGpuCores.Text = gpu.ComputeUnits > 0 ? $"{gpu.ComputeUnits} Compute Units ({gpu.ComputeUnits * 64} Cores)" : "Auto CUDA Cores";
                if (GbGpuParams != null) GbGpuParams.Header = $"⚡ GPU Performance Parameters ({gpu.Name})";
                if (TxtGpuArch != null) TxtGpuArch.Text = !string.IsNullOrEmpty(gpu.ComputeCapability) ? gpu.ComputeCapability : "NVIDIA CUDA Hardware";
                UpdateStartButtonState(isScanning: false);
            }
        }

        private void UpdateKangarooCountInfo()
        {
            if (SliderBlocks == null || SliderThreads == null || TxtKangarooCountVal == null) return;

            int blocks = (int)SliderBlocks.Value;
            int threads = (int)SliderThreads.Value;
            long totalPairs = (long)blocks * threads;
            long totalKangaroos = totalPairs * 2;

            TxtKangarooCountVal.Text = $"{totalPairs:N0} Çift ({totalKangaroos:N0} Kanguru)";
        }

        private bool _userManuallySetDp = false;

        private void AutoCalculateAndSetOptimalDpBits(int puzzleNum = 0)
        {
            if (SliderDpBits == null) return;
            if (_userManuallySetDp && puzzleNum <= 0) return; // Respect user's manual DP selection!

            int nBits = puzzleNum;
            if (nBits <= 0)
            {
                try
                {
                    string startHex = TxtKeyStart?.Text?.Trim() ?? "";
                    string endHex = TxtKeyEnd?.Text?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(startHex) && !string.IsNullOrEmpty(endHex))
                    {
                        var startVal = System.Numerics.BigInteger.Parse("0" + startHex, System.Globalization.NumberStyles.HexNumber);
                        var endVal = System.Numerics.BigInteger.Parse("0" + endHex, System.Globalization.NumberStyles.HexNumber);
                        var diff = endVal - startVal;

                        if (diff > System.Numerics.BigInteger.Zero)
                        {
                            nBits = (int)diff.GetBitLength();
                        }
                    }
                }
                catch
                {
                    // Fallback on format error
                }
            }

            if (nBits <= 0) nBits = 66;

            // Van Oorschot-Wiener theoretical DP bits
            double nVal = Math.Pow(2.0, nBits);
            double sqrtN = Math.Sqrt(nVal);
            double totalSteps = 2.0 * sqrtN;

            int blocks = SliderBlocks != null ? (int)SliderBlocks.Value : 1024;
            int threads = SliderThreads != null ? (int)SliderThreads.Value : 256;
            int numKangaroos = blocks * threads * 2;
            if (numKangaroos > 2)
            {
                totalSteps /= Math.Sqrt(numKangaroos / 2.0);
            }

            double availableRamBytes = 4096.0 * 1024.0 * 1024.0; // 4GB default RAM budget
            int entrySizeBytes = 84;
            double maxEntries = availableRamBytes / entrySizeBytes;

            int kRam = (int)Math.Ceiling(Math.Log2(totalSteps / maxEntries));
            int kOvershoot = (int)(nBits / 2.0) - 4;

            int optDp = Math.Max(kRam, 0);
            optDp = Math.Min(optDp, kOvershoot);
            optDp = Math.Max(optDp, 6);   // UI min bound
            optDp = Math.Min(optDp, 60);  // UI max bound (RCKangaroo supports DP 14..60)

            SliderDpBits.Value = optDp;
        }

        private void TxtKeyRange_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            AutoCalculateAndSetOptimalDpBits();
        }

        private void SliderBlocks_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBlocksVal != null) TxtBlocksVal.Text = ((int)e.NewValue).ToString();
            UpdateKangarooCountInfo();
            AutoCalculateAndSetOptimalDpBits();
        }

        private void SliderThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtThreadsVal != null) TxtThreadsVal.Text = ((int)e.NewValue).ToString();
            UpdateKangarooCountInfo();
            AutoCalculateAndSetOptimalDpBits();
        }

        private void SliderPoints_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPointsVal != null) TxtPointsVal.Text = ((int)e.NewValue).ToString();
        }

        private void BtnBrowseTargets_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Target Address List File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtTargetsFile.Text = dlg.FileName;
            }
        }

        private void BtnBrowseCheckpoint_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Checkpoint Files (*.ci)|*.ci|All Files (*.*)|*.*",
                Title = "Select Checkpoint File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtCheckpointFile.Text = dlg.FileName;
            }
        }

        private void BtnBrowseResults_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Results Output File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtResultsFile.Text = dlg.FileName;
            }
        }

        private void ChkUseTames_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkUseTames?.IsChecked == true;
            if (TxtTamesFile != null) TxtTamesFile.IsEnabled = isChecked;
            if (BtnBrowseTames != null) BtnBrowseTames.IsEnabled = isChecked;
            if (TxtTamesMax != null) TxtTamesMax.IsEnabled = isChecked;
        }

        private void BtnBrowseTames_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Tames Guide Files (*.tames;*.work)|*.tames;*.work|All Files (*.*)|*.*",
                Title = "Select Kangaroo Pre-calculated Tames Guide File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtTamesFile.Text = dlg.FileName;
            }
        }

        private void SliderDpBits_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDpBitsVal != null) TxtDpBitsVal.Text = ((int)e.NewValue).ToString();
            if (IsLoaded) _userManuallySetDp = true;
        }

        private void SetPuzzleRange(int puzzleNum)
        {
            if (puzzleNum < 1 || puzzleNum > 256 || TxtKeyStart == null || TxtKeyEnd == null) return;

            _userManuallySetDp = false; // Reset manual override on explicit puzzle change

            var startBig = System.Numerics.BigInteger.One << (puzzleNum - 1);
            var endBig = (System.Numerics.BigInteger.One << puzzleNum) - 1;

            var secp256k1N = System.Numerics.BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364140", System.Globalization.NumberStyles.HexNumber);
            if (endBig > secp256k1N) endBig = secp256k1N;

            TxtKeyStart.Text = startBig.ToString("X");
            TxtKeyEnd.Text = endBig.ToString("X");

            // Mathematically exact optimal DP bits for Pollard's Kangaroo
            AutoCalculateAndSetOptimalDpBits(puzzleNum);

            // Set known official Bitcoin puzzle addresses
            if (puzzleNum == 66) TxtTargetAddress.Text = "13zb1hQbWVsc2S7ZTGarKvvtzzyesvh5B2";
            else if (puzzleNum == 67) TxtTargetAddress.Text = "1BY8GQbnueYDoFWSuBWZ4Standard67";
            else if (puzzleNum == 68) TxtTargetAddress.Text = "1MVDYgVaSN6iKaWJfEccwbya55re42WFCg";
            else if (puzzleNum == 40) TxtTargetAddress.Text = "1EeAxcprB2PpCnr34VfZdFrkUWuxyiNEFv";
            else if (puzzleNum == 32) TxtTargetAddress.Text = "1C89mJxF2642j3iGzXzL9b5fR17n4c8Nq";
            else if (puzzleNum == 20) TxtTargetAddress.Text = "1HS9GcVyzirUyivtkGdTFhDeLBYyLWNXAp";
            else if (puzzleNum == 160) TxtTargetAddress.Text = "14o7vwE7q8g7F24tH2Xv6V7Z7x5";
            else if (puzzleNum == 256) TxtTargetAddress.Text = "15Z5YJaaNSxeynvr6uW6jQZLwq3n1Hu6RX";
        }

        private void CboPuzzleSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CboPuzzleSelect == null || CboPuzzleSelect.SelectedIndex < 0) return;
            SetPuzzleRange(CboPuzzleSelect.SelectedIndex + 1);
        }

        private void BtnPresetPuzzle20_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(20);
        private void BtnPresetPuzzle40_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(40);
        private void BtnPresetPuzzle66_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(66);
        private void BtnPresetPuzzle80_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(80);
        private void BtnPresetPuzzle100_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(100);
        private void BtnPresetPuzzle120_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(120);
        private void BtnPresetPuzzle140_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(140);
        private void BtnPresetPuzzle160_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(160);
        private void BtnPresetPuzzle256_Click(object sender, RoutedEventArgs e) => SetPuzzleRange(256);

        private void RbAlgo_Changed(object sender, RoutedEventArgs e)
        {
            if (SliderBlocks == null || SliderThreads == null || SliderPoints == null || TxtApiStatus == null) return;

            if (RbAlgoRCKangaroo?.IsChecked == true)
            {
                TxtApiStatus.Text = $"⚡ RCKangaroo v3.1 Motoru Seçildi ({_detectedGpuName}): SOTA+ Algoritması (~8 GKey/s)";
            }
            else if (RbAlgoKangaroo?.IsChecked == true)
            {
                // Optimal Kangaroo Preset for RTX 2060 Super (256 Threads/Block prevents CUDA register overflow)
                SliderBlocks.Value = 1024;
                SliderThreads.Value = 256;
                SliderPoints.Value = 32;
                TxtApiStatus.Text = $"⚡ Yerel Kanguru Otomatik Ayarlandı ({_detectedGpuName}): 262,144 Kanguru (1024x256)";
            }
            else
            {
                // Standard BitCrack Preset
                SliderBlocks.Value = 128;
                SliderThreads.Value = 256;
                SliderPoints.Value = 32;
            }
        }

        private void RbTargetMode_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtTargetAddress == null || TxtTargetsFile == null || BtnBrowseTargets == null || BtnCheckApi == null) return;

            bool isSingle = RbSingleTarget?.IsChecked == true;
            TxtTargetAddress.IsEnabled = isSingle;
            BtnCheckApi.IsEnabled = isSingle;
            TxtTargetsFile.IsEnabled = !isSingle;
            BtnBrowseTargets.IsEnabled = !isSingle;
        }

        private void TxtPublicKeyHex_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TxtPublicKeyHex != null)
            {
                _recoveredPubKey = TxtPublicKeyHex.Text.Trim();
            }
        }

        private async void BtnCheckApi_Click(object sender, RoutedEventArgs e)
        {
            string address = TxtTargetAddress.Text.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                MessageBox.Show("Lütfen kontrol edilecek bir Bitcoin adresi veya Public Key girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnCheckApi.IsEnabled = false;
            TxtApiStatus.Text = "⏳ Blokzincir API sorgulanıyor...";

            var res = await BlockchainApi.CheckAddressAsync(address);

            TxtApiStatus.Text = res.Message;
            BtnCheckApi.IsEnabled = true;

            if (!string.IsNullOrEmpty(res.PublicKeyHex))
            {
                _recoveredPubKey = res.PublicKeyHex;
                if (TxtPublicKeyHex != null) TxtPublicKeyHex.Text = res.PublicKeyHex;
            }

            if (res.HasSpentTx)
            {
                if (RbAlgoRCKangaroo != null) RbAlgoRCKangaroo.IsChecked = true;
                else if (RbAlgoKangaroo != null) RbAlgoKangaroo.IsChecked = true;
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_runner.IsScanning)
            {
                _runner.Stop();
                UpdateStartButtonState(isScanning: false);
                TxtStatus.Text = "Tarama kullanıcı tarafından durduruldu.";
                return;
            }

            bool isSingle = RbSingleTarget.IsChecked == true;

            if (isSingle && string.IsNullOrWhiteSpace(TxtTargetAddress.Text))
            {
                MessageBox.Show("Lütfen taranacak tek bir Bitcoin adresi veya Public Key girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!isSingle && (string.IsNullOrWhiteSpace(TxtTargetsFile.Text) || !File.Exists(TxtTargetsFile.Text)))
            {
                MessageBox.Show("Lütfen geçerli bir toplu adres dosyası (.txt) seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isRCKangaroo = RbAlgoRCKangaroo?.IsChecked == true;
            bool isKangarooMode = (RbAlgoKangaroo?.IsChecked == true) || isRCKangaroo;

            string kStart = string.IsNullOrWhiteSpace(TxtKeyStart.Text.Trim().TrimStart('0')) ? "1" : TxtKeyStart.Text.Trim().TrimStart('0');
            string kEnd = string.IsNullOrWhiteSpace(TxtKeyEnd.Text.Trim().TrimStart('0')) ? "1" : TxtKeyEnd.Text.Trim().TrimStart('0');
            int calcRange = BitCrackRunner.CalculateRangeFromStartEnd(kStart, kEnd);

            if (isRCKangaroo && calcRange > 170)
            {
                MessageBox.Show(
                    $"RCKangaroo motoru en fazla 170-bit aralıkları destekler (Seçilen aralık: {calcRange}-bit).\n\n" +
                    "256-bit tam anahtar arayışları için lütfen 'Standard BitCrack Engine (Linear Multi-Hash)' moduna geçin.",
                    "RCKangaroo Limit Uyarısı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Determine effective Public Key
            string effectivePubKey = TxtPublicKeyHex?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(effectivePubKey)) effectivePubKey = _recoveredPubKey;

            // Check if TxtTargetAddress is itself a Public Key (02/03 length 66 or 04 length 130)
            string targetInput = TxtTargetAddress?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(effectivePubKey) && !string.IsNullOrWhiteSpace(targetInput))
            {
                if ((targetInput.Length == 66 && (targetInput.StartsWith("02", StringComparison.OrdinalIgnoreCase) || targetInput.StartsWith("03", StringComparison.OrdinalIgnoreCase))) ||
                    (targetInput.Length == 130 && targetInput.StartsWith("04", StringComparison.OrdinalIgnoreCase)))
                {
                    effectivePubKey = targetInput;
                    if (TxtPublicKeyHex != null) TxtPublicKeyHex.Text = targetInput;
                }
            }

            if (isKangarooMode && string.IsNullOrWhiteSpace(effectivePubKey))
            {
                MessageBox.Show(
                    "Kanguru / SOTA+ modu için hedef Public Key gerekir.\n\n" +
                    "1) Adresi 'API Kontrol' ile sorgulayın (harcanmış adreslerden pubkey çekilir),\n" +
                    "2) 'Target Public Key (Hex)' kutusuna Public Key'i elle yapıştırın, veya\n" +
                    "3) Lineer moda geçin (pubkey bilinmeyen adresler için).",
                    "Kanguru: Public Key Yok",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Build config
            var config = new BitCrackConfig
            {
                Engine = isRCKangaroo ? EngineType.RCKangaroo : EngineType.cuBitCrack,
                ExecutablePath = _executablePath,
                DeviceId = 0,
                Blocks = (int)SliderBlocks.Value,
                Threads = (int)SliderThreads.Value,
                PointsPerThread = (int)SliderPoints.Value,
                CompressionMode = RbCompressed.IsChecked == true ? "compressed" : (RbUncompressed.IsChecked == true ? "uncompressed" : "both"),
                TargetAddress = isSingle ? (TxtTargetAddress?.Text.Trim() ?? "") : "",
                TargetsFile = !isSingle ? TxtTargetsFile.Text.Trim() : "",
                KeyspaceStart = string.IsNullOrWhiteSpace(TxtKeyStart.Text.Trim().TrimStart('0')) ? "1" : TxtKeyStart.Text.Trim().TrimStart('0'),
                KeyspaceEnd = string.IsNullOrWhiteSpace(TxtKeyEnd.Text.Trim().TrimStart('0')) ? "1" : TxtKeyEnd.Text.Trim().TrimStart('0'),
                EnableCheckpoint = !isKangarooMode && (ChkEnableCheckpoint.IsChecked == true),
                CheckpointFile = TxtCheckpointFile.Text.Trim(),
                ResultsFile = TxtResultsFile.Text.Trim(),
                IsKangarooMode = isKangarooMode,
                DpBits = SliderDpBits != null ? (int)SliderDpBits.Value : 16,
                PublicKeyHex = effectivePubKey,
                TamesFile = (ChkUseTames?.IsChecked == true && TxtTamesFile != null && !string.IsNullOrWhiteSpace(TxtTamesFile.Text)) ? TxtTamesFile.Text.Trim() : ""
            };

            // Reset UI
            BorderKeyFound.Visibility = Visibility.Collapsed;
            TxtConsoleLog.Clear();
            TxtLiveSpeed.Text = "0.00 MKey/s";
            TxtTotalKeys.Text = "0";
            TxtElapsedTime.Text = "00:00:00";

            UpdateStartButtonState(isScanning: true);
            TxtStatus.Text = $"Tarama çalışıyor ({_detectedGpuName})...";

            await _runner.StartAsync(config);
        }

        private void UpdateStartButtonState(bool isScanning)
        {
            if (isScanning)
            {
                BtnStart.Content = "⏹ STOP SCANNING";
                BtnStart.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
            }
            else
            {
                BtnStart.Content = $"🚀 START SCANNING ({_detectedGpuName})";
                BtnStart.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
            }
        }

        private void Runner_StatusUpdated(object? sender, ScanStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLiveSpeed.Text = e.SpeedText;
                TxtTotalKeys.Text = e.TotalKeysText;
                TxtElapsedTime.Text = e.ElapsedTimeText;
            });
        }

        private void Runner_KeyFound(object? sender, KeyFoundEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _runner.Stop();
                UpdateStartButtonState(isScanning: false);
                TxtStatus.Text = "🎉 ÖZEL ANAHTAR BULUNDU!";

                BorderKeyFound.Visibility = Visibility.Visible;
                TxtFoundAddress.Text = e.Address;
                TxtFoundPrivateKey.Text = e.PrivateKey;
                TxtFoundPublicKey.Text = e.PublicKey;

                System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show($"🎉 TEBRİKLER! ÖZEL ANAHTAR BULUNDU!\n\nAdres: {e.Address}\nPrivate Key: {e.PrivateKey}", "ANAHTAR BULUNDU!", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void Runner_LogOutput(object? sender, string line)
        {
            Dispatcher.Invoke(() =>
            {
                TxtConsoleLog.AppendText(line + Environment.NewLine);
                TxtConsoleLog.ScrollToEnd();
            });
        }

        private void Runner_ScanFinished(object? sender, int exitCode)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStartButtonState(isScanning: false);
                TxtStatus.Text = $"Tarama bitti (Exit Code: {exitCode}).";
            });
        }
    }
}
