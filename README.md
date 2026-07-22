# BitCrack Control Center — CUDA GPU Edition ⚡

![CUDA](https://img.shields.io/badge/CUDA-v12.0%2B-green.svg)
![.NET](https://img.shields.io/badge/.NET-9.0--windows-blue.svg)
![Architecture](https://img.shields.io/badge/Architecture-x64%20Single--File%20Executable-orange.svg)
![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)

> **Language / Diller:** [English](README.md) | [Türkçe](README_TR.md)

**BitCrack Control Center** is an ultra-fast, portable, single-file GUI suite and GPU key search engine for Windows powered by NVIDIA CUDA hardware acceleration. It features low-level PTX assembly math, warp-level Montgomery batch inversion, and dual search engines (**Linear Multi-Hash** and **Pollard's Kangaroo**) for solving Bitcoin Puzzles (#1 to #256) and recovering private keys across custom ranges.

---

## 🌟 Features & Technical Highlights

- **Single-File Portable Executable**: `BitCrackGUI.exe` is bundled with the complete .NET 9 Desktop Runtime inside a single 62.8 MB executable. **No .NET runtime or CUDA Toolkit SDK installation required for end-users!** (Only NVIDIA Display Driver needed).
- **PTX Assembly Math**: Branchless 256-bit modular addition and subtraction (`addModP`, `subModP`) compiled to GPU SASS instructions.
- **Warp-Level Batch Inversion**: 32-thread cooperative warp Montgomery batch inversion (`invModP`), removing serial Euclidean bottlenecks.
- **LDS Stride Önbellekleme**: Zero-latency shared memory (`__shared__`) caching for precomputed elliptic curve stride points (`_INC_X`, `_INC_Y`).
- **Hardware-Accelerated Cryptography**: SHA-256 and RIPEMD-160 execution pipelines using `lop3.b32` hardware lookup tables and `__funnelshift_r` bitwise shifts.
- **Multi-Engine Support**:
  - **Standard BitCrack Engine ($O(N)$)**: Linear multi-hash engine for unspent addresses and full 256-bit keyspace.
  - **RCKangaroo v3.1 Engine ($O(\sqrt{N})$)**: SOTA Pollard's Kangaroo algorithm for bounded bit ranges (32–170 bits).

---

## 📖 Comprehensive Step-by-Step Usage Guide

### 1. Launching the Application
Simply double-click **`BitCrackGUI.exe`**. The application auto-detects your active NVIDIA GPU (VRAM capacity, CUDA Cores, and Compute Capability).

---

### 2. Setting Target Addresses & Public Keys

#### Option A: Single Address Mode (with Automatic Blockchain API Resolution)
1. Select **Single Address / Public Key Mode**.
2. Type or paste your target Bitcoin Address (e.g. `15Z5YJaaNSxeynvr6uW6jQZLwq3n1Hu6RX`) in the target box.
3. Click **`🔍 Check API`**.
   - The application queries `mempool.space` and `blockstream.info` blockchain APIs using multi-page pagination.
   - If the address has spent Bitcoin, its spending **Public Key** is automatically extracted and populated into the **`Target Public Key (Hex)`** field.
   - If a raw Public Key (`02...`/`03...` 66 chars or `04...` 130 chars) is entered directly, it is validated instantly.

#### Option B: Bulk File List Mode
1. Select **Bulk File List Mode (.txt)**.
2. Click **Browse...** to load a text file containing multiple P2PKH Bitcoin addresses (one address per line).

---

### 3. Choosing the Search Engine Algorithm

- **⚡ RCKangaroo v3.1 Engine (SOTA+ ~1.5 GKey/s to 8+ GKey/s)**:
  - **Use Case**: Best for spent addresses with a revealed Public Key across bounded bit ranges (32 to 170 bits, e.g. Bitcoin Puzzles #1 to #160).
  - *Note*: Kangaroo mode requires a target Public Key and supports bit ranges up to 170 bits.

- **cuBitCrack Kangaroo Engine**:
  - Native CUDA Pollard's Kangaroo engine ($O(\sqrt{N})$) using Distinguished Point (DP) collision detection.

- **Standard BitCrack Engine (Linear Multi-Hash $O(N)$)**:
  - **Use Case**: Best for unspent addresses, bulk target address lists, or full 256-bit keyspace searches.

---

### 4. Keyspace Range & Puzzle Presets

- **Quick Puzzle Presets**: Click any preset button (**Puzzle #20**, **#40**, **#66**, **#80**, **#100**, **#120**, **#140**, **#160**, **#256**) to automatically populate the exact start (`-start`) and end range (`-keyspace`) hex values.
- **Puzzle Dropdown**: Select any puzzle from **#1 through #256** from the dropdown menu.
- **Custom Keyspace**: Manually type starting and ending private key hex values in **Start Key (Hex)** and **End Key (Hex)**.
- **Distinguished Points (-dp)**: Set DP bits manually or let the app automatically calculate mathematically optimal DP bits for Pollard's Kangaroo.

---

### 5. Performance Tuning Parameters

- **Compression Mode**: Choose `Compressed (-c)`, `Uncompressed (-u)`, or `Both` depending on the key format.
- **GPU Blocks (-b)**: Number of CUDA thread blocks (Default: `1024` for Kangaroo, `128` for Linear).
- **Threads per Block (-t)**: CUDA threads per block (Default: `256`).
- **Points per Thread (-p)**: Elliptic curve points processed per thread (Default: `32`).
- **Precalculated Tames Guide File (-tames)**: Enable to load precalculated Kangaroo tame guide files (`.tames` / `.work`) to speed up Kangaroo solving.

---

### 6. Starting & Monitoring the Search

1. Click **`🚀 START SCANNING`**.
2. **Live Gauge Cards**: Monitor real-time speed (**MKey/s / GKey/s**), total checked keys / DP counters, and elapsed time.
3. **Console Log**: View live GPU log streams, CUDA device initialization status, and execution progress.
4. **Key Discovery**: When a matching private key is found:
   - A green discovery banner appears displaying the **Address**, **Private Key (Hex/WIF)**, and **Public Key**.
   - An audio alert plays and a popup dialog notifies you.
   - The result is automatically saved to `found_keys.txt`.

---

## 📊 Performance Benchmarks

| GPU Model | Linear Engine (Multi-Hash) | RCKangaroo Engine (SOTA+) |
| :--- | :---: | :---: |
| **NVIDIA RTX 2060 Super** | ~477.5 MKey/s | ~1.50 GKey/s |
| **NVIDIA RTX 3080** | ~1.10 GKey/s | ~3.80 GKey/s |
| **NVIDIA RTX 4090** | ~2.50 GKey/s | ~8.00 GKey/s |

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE.MIT](LICENSE.MIT) file for details.

---

> ⚠️ **Disclaimer**: This software is intended for educational, research, and recovery purposes on addresses you legally own or for participation in public cryptographic challenge puzzles (e.g. Bitcoin Puzzles).
