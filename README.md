# BitCrack Control Center & CUDA Engine — High-Speed GPU Key Search Suite ⚡

![CUDA](https://img.shields.io/badge/CUDA-v12.0%2B-green.svg)
![.NET](https://img.shields.io/badge/.NET-9.0--windows-blue.svg)
![Download](https://img.shields.io/badge/Download-Google%20Drive-red.svg)
![License](https://img.shields.io/badge/License-MIT-blue.svg)

> **Language / Diller:** [English](README.md) | [Türkçe](README_TR.md)  
> 📥 **Direct Download (Google Drive):** [Download Full Package (GUI + CUDA Engines)](https://drive.google.com/drive/folders/1b37LVwxgE3P0IdzXM95SGTGWBUp4k-C9?usp=sharing)

**BitCrack Control Center & CUDA Engine** is a high-performance GPU key search suite for Windows powered by NVIDIA CUDA hardware acceleration. It features low-level PTX assembly optimizations, 32-thread warp-level Montgomery batch inversion, LDS shared memory stride caching, an intuitive WPF GUI Control Center, and dual native CUDA engines: **`cuBitCrack.exe`** ($O(N)$ Linear Multi-Hash Engine) and **`RCKangaroo.exe`** ($O(\sqrt{N})$ Pollard's Kangaroo SOTA+ Engine).

---

## 📥 Download Full Executable Package (Google Drive)

You can download the pre-compiled standalone executables (`BitCrackGUI.exe`, `cuBitCrack.exe`, `RCKangaroo.exe`) and complete runtime package directly from the Google Drive folder:

👉 **[Click Here to Open Google Drive Folder & Download Files](https://drive.google.com/drive/folders/1b37LVwxgE3P0IdzXM95SGTGWBUp4k-C9?usp=sharing)**

---

## 🌟 Key Performance & Architecture Highlights

- **Single-File GUI Executable**: `BitCrackGUI.exe` is bundled with all required runtime dependencies into a standalone single-file binary. **No .NET runtime or CUDA SDK installation required for end-users!** (Only standard NVIDIA Display Driver needed).
- **PTX Assembly Math**: Branchless 256-bit modular addition and subtraction (`addModP`, `subModP`) targeting GPU SASS execution directly.
- **Warp-Level Batch Inversion**: 32-thread cooperative warp Montgomery batch modular inversion (`invModP`), eliminating serial Euclidean bottlenecks.
- **LDS Stride Caching**: Zero-latency shared memory (`__shared__`) caching for precomputed elliptic curve stride points (`_INC_X`, `_INC_Y`).
- **Hardware-Accelerated Cryptography**: Optimized SHA-256 and RIPEMD-160 execution pipelines using `lop3.b32` hardware lookup tables and `__funnelshift_r` bitwise shifts.

---

## 📖 GUI & Command-Line Usage Guide

### 1. WPF GUI Control Center (`BitCrackGUI.exe`)
- **Automatic GPU Detection**: Detects installed NVIDIA GPU, VRAM capacity, CUDA Cores, and Compute Capability.
- **Blockchain API Integration**: Queries `mempool.space` and `blockstream.info` APIs with multi-page pagination to auto-extract spending Public Keys for target Bitcoin addresses.
- **Bitcoin Puzzle Presets**: 1-click configuration for official Bitcoin Puzzles (**#1 through #256**).
- **Live Real-time Monitoring**: Real-time speed gauge (MKey/s / GKey/s), checked key counter, elapsed time, log console, and audio alerts upon finding keys.

### 2. `cuBitCrack.exe` — Linear Multi-Hash Engine ($O(N)$)
Designed for unspent Bitcoin addresses, bulk target address lists, and searching full 256-bit keyspaces.
```cmd
cuBitCrack.exe -d 0 -b 1024 -t 256 -p 32 -c --keyspace 4000000000:7FFFFFFFFF -o found_keys.txt 1EeAxcprB2PpCnr34VfZdFrkUWuxyiNEFv
```

### 3. `RCKangaroo.exe` — SOTA+ Pollard's Kangaroo Engine ($O(\sqrt{N})$)
Designed for solving spent Bitcoin addresses with a revealed Public Key across bounded bit ranges (32 to 170 bits, e.g. Bitcoin Puzzles #1 to #160).
```cmd
RCKangaroo.exe -gpu 0 -dp 16 -range 66 -start 2000000000000000 -pubkey 03a20917...
```

---

## 📊 Performance Benchmarks

| GPU Model | `cuBitCrack.exe` (Linear Engine) | `RCKangaroo.exe` (SOTA+ Engine) |
| :--- | :---: | :---: |
| **NVIDIA RTX 2060 Super** | ~477.5 MKey/s | ~1.50 GKey/s |
| **NVIDIA RTX 3080** | ~1.10 GKey/s | ~3.80 GKey/s |
| **NVIDIA RTX 4090** | ~2.50 GKey/s | ~8.00 GKey/s |

---

## 📄 License

This project is licensed under the **MIT License** - see [LICENSE.MIT](LICENSE.MIT) for details.

---

> ⚠️ **Disclaimer**: This software is intended for educational, research, and recovery purposes on addresses you legally own or for participation in public cryptographic challenge puzzles (e.g. Bitcoin Puzzles).
