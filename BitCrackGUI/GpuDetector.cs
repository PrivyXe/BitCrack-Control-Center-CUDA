using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BitCrackGUI
{
    public class GpuDeviceInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Bilinmeyen GPU";
        public string Memory { get; set; } = "0 MB";
        public int ComputeUnits { get; set; }
        public string ComputeCapability { get; set; } = "Auto CUDA";

        public override string ToString() => $"[ID: {Id}] {Name} ({Memory}, {ComputeUnits} Compute Units)";
    }

    public static class GpuDetector
    {
        public static List<GpuDeviceInfo> DetectDevices(string bitCrackExePath)
        {
            var devices = new List<GpuDeviceInfo>();

            // Method 1: Query backend (cuBitCrack.exe --list-devices)
            if (File.Exists(bitCrackExePath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = bitCrackExePath,
                        Arguments = "--list-devices",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);

                        GpuDeviceInfo? current = null;
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (trimmed.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (current != null) devices.Add(current);
                                current = new GpuDeviceInfo();
                                if (int.TryParse(trimmed.Substring(3).Trim(), out int id))
                                {
                                    current.Id = id;
                                }
                            }
                            else if (trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase) && current != null)
                            {
                                current.Name = trimmed.Substring(5).Trim();
                            }
                            else if (trimmed.StartsWith("Memory:", StringComparison.OrdinalIgnoreCase) && current != null)
                            {
                                current.Memory = trimmed.Substring(7).Trim();
                            }
                            else if (trimmed.StartsWith("Compute units:", StringComparison.OrdinalIgnoreCase) && current != null)
                            {
                                if (int.TryParse(trimmed.Substring(14).Trim(), out int cu))
                                {
                                    current.ComputeUnits = cu;
                                }
                            }
                        }

                        if (current != null) devices.Add(current);
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            // Method 2: System WMI Query (Win32_VideoController via PowerShell)
            if (devices.Count == 0)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-Command \"Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);

                        var gpuNames = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        int id = 0;
                        foreach (var name in gpuNames)
                        {
                            string cleanName = name.Trim();
                            if (!string.IsNullOrEmpty(cleanName) && !cleanName.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                            {
                                devices.Add(new GpuDeviceInfo
                                {
                                    Id = id++,
                                    Name = cleanName,
                                    Memory = "Auto Memory",
                                    ComputeUnits = 0
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            // Method 3: Generic Fallback
            if (devices.Count == 0)
            {
                devices.Add(new GpuDeviceInfo
                {
                    Id = 0,
                    Name = "NVIDIA CUDA GPU",
                    Memory = "Auto Memory",
                    ComputeUnits = 0
                });
            }

            return devices;
        }
    }
}
