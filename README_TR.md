# BitCrack Control Center & CUDA Engine — Yüksek Hızlı GPU Anahtar Arama Paketi ⚡

![CUDA](https://img.shields.io/badge/CUDA-v12.0%2B-green.svg)
![.NET](https://img.shields.io/badge/.NET-9.0--windows-blue.svg)
![İndir](https://img.shields.io/badge/İndir-Google%20Drive-red.svg)
![License](https://img.shields.io/badge/License-MIT-blue.svg)

> **Language / Diller:** [English](README.md) | [Türkçe](README_TR.md)  
> 📥 **Doğrudan İndirme Klasörü (Google Drive):** [Tüm Paketi İndir (GUI + CUDA Motorları)](https://drive.google.com/drive/folders/1b37LVwxgE3P0IdzXM95SGTGWBUp4k-C9?usp=sharing)

**BitCrack Control Center & CUDA Engine**, NVIDIA ekran kartlarının donanım hızlandırma gücünden yararlanan, kuruluma ve derlemeye ihtiyaç duymayan taşınabilir (portable) Secp256k1 Bitcoin özel anahtar arama paketidir. Alt seviye PTX assembly optimizasyonları, 32-iş parçacıklı warp-seviyesi inversiyon, modern WPF grafik arayüzü (`BitCrackGUI.exe`) ve iki farklı yerel CUDA motoru içerir: **`cuBitCrack.exe`** ($O(N)$ Lineer Multi-Hash Motoru) ve **`RCKangaroo.exe`** ($O(\sqrt{N})$ Pollard's Kangaroo SOTA+ Motoru).

---

## 📥 Hazır Çalıştırılabilir Paketi İndir (Google Drive)

Derlenmiş hazır çalıştırılabilir dosyaları (`BitCrackGUI.exe`, `cuBitCrack.exe`, `RCKangaroo.exe`) ve tam çalışma paketini doğrudan Google Drive klasöründen indirebilirsiniz:

👉 **[Google Drive Klasörünü Açmak ve İndirmek İçin Buraya Tıklayın](https://drive.google.com/drive/folders/1b37LVwxgE3P0IdzXM95SGTGWBUp4k-C9?usp=sharing)**

---

## 🌟 Öne Çıkan Teknik Özellikler

- **Tek Dosyada Grafik Arayüz (Single-File Binary)**: Tüm .NET 9 çalışma motoru `BitCrackGUI.exe` dosyasının içine gömülmüştür. **Kullanıcıların .NET Runtime veya CUDA SDK kurmasına KESİNLİKLE GEREK YOKTUR!** (Sadece güncel NVIDIA ekran kartı sürücüsü yeterlidir).
- **PTX Assembly Matematiği**: Doğrudan GPU SASS komutlarını hedefleyen dallanmasız (branchless) 256-bit modüler toplama ve çıkarma (`addModP`, `subModP`).
- **Warp-Seviyesi Toplu İnversiyon**: Seri Öklid darboğazını ortadan kaldıran 32-iş parçacıklı paralel Montgomery modüler inversiyonu (`invModP`).
- **LDS Stride Önbellekleme**: Elliptic curve adım noktalarının (`_INC_X`, `_INC_Y`) GPU paylaşımlı belleğinde (`__shared__`) sıfır gecikmeyle önbelleklenmesi.
- **Donanım Hızlandırmalı Kriptografi**: `lop3.b32` donanım doğruluk tabloları ve `__funnelshift_r` komutları ile optimize edilmiş SHA-256 ve RIPEMD-160 hesaplama hattı.

---

## 📖 Grafik Arayüz & Komut Satırı Kullanım Rehberi

### 1. WPF Grafik Kontrol Paneli (`BitCrackGUI.exe`)
- **Otomatik GPU Tespiti**: Sistemdeki NVIDIA GPU modelini, VRAM miktarını ve CUDA çekirdeklerini otomatik tespit eder.
- **Blokzincir API Entegrasyonu**: `mempool.space` ve `blockstream.info` üzerinden geçmiş sayfaları tarayarak harcanmış adreslerin **Public Key** verisini otomatik çeker.
- **Bulmaca Kısayolları**: Resmi Bitcoin Bulmacaları (**#1 - #256**) için tek tıkla otomatik aralık ayarı.
- **Canlı İzleme ve Sesli Uyarı**: Anlık tarama hızı (MKey/s / GKey/s), sayaçlar, geçen süre, canlı konsol ve sesli bildirim.

### 2. `cuBitCrack.exe` — Lineer Multi-Hash Motoru ($O(N)$)
Harcama yapılmamış Bitcoin adresleri, toplu adres listeleri ve tam 256-bit anahtar uzayı taramaları için geliştirilmiştir.
```cmd
cuBitCrack.exe -d 0 -b 1024 -t 256 -p 32 -c --keyspace 4000000000:7FFFFFFFFF -o found_keys.txt 1EeAxcprB2PpCnr34VfZdFrkUWuxyiNEFv
```

### 3. `RCKangaroo.exe` — SOTA+ Pollard's Kangaroo Motoru ($O(\sqrt{N})$)
Public Key'i bilinen harcanmış Bitcoin adresleri ve belirli bit aralıkları (32 ile 170 bit arası, örn: Bitcoin Bulmacaları #1 - #160) için geliştirilmiştir.
```cmd
RCKangaroo.exe -gpu 0 -dp 16 -range 66 -start 2000000000000000 -pubkey 03a20917...
```

---

## 📊 Performans Değerleri

| GPU Modeli | `cuBitCrack.exe` (Lineer Motor) | `RCKangaroo.exe` (SOTA+ Motoru) |
| :--- | :---: | :---: |
| **NVIDIA RTX 2060 Super** | ~477.5 MKey/s | ~1.50 GKey/s |
| **NVIDIA RTX 3080** | ~1.10 GKey/s | ~3.80 GKey/s |
| **NVIDIA RTX 4090** | ~2.50 GKey/s | ~8.00 GKey/s |

---

## 📄 Lisans

Bu proje **MIT Lisansı** altında yayınlanmıştır - detaylar için [LICENSE.MIT](LICENSE.MIT) dosyasına bakabilirsiniz.

---

> ⚠️ **Sorumluluk Reddi**: Bu yazılım yalnızca eğitim, araştırma ve kendi sahip olduğunuz cüzdanların kurtarılması veya açık kriptografik bulmaca yarışmalarına (Bitcoin Bulmacaları) katılım amacıyla geliştirilmiştir.
