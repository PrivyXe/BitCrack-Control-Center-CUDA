# BitCrack Control Center — CUDA GPU Edition ⚡

![CUDA](https://img.shields.io/badge/CUDA-v12.0%2B-green.svg)
![.NET](https://img.shields.io/badge/.NET-9.0--windows-blue.svg)
![Architecture](https://img.shields.io/badge/Architecture-x64%20Tek%20Dosya%20Executable-orange.svg)
![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)

> **Language / Diller:** [English](README.md) | [Türkçe](README_TR.md)

**BitCrack Control Center**, NVIDIA ekran kartlarının donanım hızlandırma gücünden yararlanan, kuruluma ve derlemeye ihtiyaç duymayan taşınabilir (portable), tek dosyada paketlenmiş ultra hızlı Bitcoin özel anahtar arama motoru ve WPF kontrol panelidir. Alt seviye PTX assembly optimizasyonları ve çift arama motoru (**Lineer Multi-Hash** ve **Pollard's Kangaroo**) ile resmi Bitcoin Bulmacalarını (#1 - #256) ve özel anahtar aralıklarını hızlıca taramak için tasarlanmıştır.

---

## 🌟 Öne Çıkan Teknik Özellikler

- **Tek Dosyada Paketlendi (Single-File Binary)**: Tüm .NET 9 çalışma motoru `BitCrackGUI.exe` (62.8 MB) dosyasının içine gömülmüştür. **Kullanıcıların .NET Runtime veya CUDA Toolkit SDK kurmasına KESİNLİKLE GEREK YOKTUR!** (Sadece güncel NVIDIA ekran kartı sürücüsü yeterlidir).
- **PTX Assembly Matematiği**: Doğrudan GPU SASS komutlarını hedefleyen dallanmasız (branchless) 256-bit modüler toplama ve çıkarma (`addModP`, `subModP`).
- **Warp-Seviyesi Toplu İnversiyon**: Seri Öklid darboğazını ortadan kaldıran 32-iş parçacıklı paralel Montgomery modüler inversiyonu (`invModP`).
- **LDS Stride Önbellekleme**: Elliptic curve adım noktalarının (`_INC_X`, `_INC_Y`) GPU paylaşımlı belleğinde (`__shared__`) sıfır gecikmeyle önbelleklenmesi.
- **Donanım Hızlandırmalı Kriptografi**: `lop3.b32` donanım doğruluk tabloları ve `__funnelshift_r` komutları ile optimize edilmiş SHA-256 ve RIPEMD-160 hesaplama hattı.
- **Çift Arama Motoru**:
  - **Standard BitCrack Engine ($O(N)$)**: Harcama yapılmamış adresler ve tam 256-bit uzay için lineer multi-hash motoru.
  - **RCKangaroo v3.1 Engine ($O(\sqrt{N})$)**: Belirli bit aralıklarında (32–170 bit) SOTA Pollard's Kangaroo çözücü.

---

## 📖 Detaylı Kullanım Rehberi (Adım Adım)

### 1. Uygulamayı Başlatma
**`BitCrackGUI.exe`** dosyasına çift tıklayarak uygulamayı başlatın. Uygulama açılırken sistemdeki aktif NVIDIA ekran kartınızı (VRAM miktarı, CUDA Çekirdekleri ve Mimarisi) otomatik olarak tespit eder.

---

### 2. Hedef Adres veya Public Key Tanımlama

#### Seçenek A: Tek Adres Modu (Otomatik Blokzincir API Sorgusu İle)
1. **Single Address / Public Key Mode** seçeneğini işaretleyin.
2. Hedef Bitcoin adresinizi (Örn: `15Z5YJaaNSxeynvr6uW6jQZLwq3n1Hu6RX`) kutucuğa yapıştırın.
3. **`🔍 Check API`** butonuna tıklayın.
   - Uygulama `mempool.space` ve `blockstream.info` blokzincir API'lerini geçmiş sayfaları tarayarak sorgular.
   - Eğer adresten daha önce harcama yapılmışsa, adrese ait giden işlemlerdeki **Public Key** otomatik olarak çekilir ve **`Target Public Key (Hex)`** alanına yazılır.
   - Doğrudan bir Public Key (`02...`/`03...` 66 karakter veya `04...` 130 karakter) girdiyseniz uygulama anında doğrular.

#### Seçenek B: Toplu Adres Listesi Modu
1. **Bulk File List Mode (.txt)** seçeneğini işaretleyin.
2. **Browse...** butonuna tıklayarak içinde her satırda bir P2PKH Bitcoin adresi bulunan `.txt` listenizi seçin.

---

### 3. Arama Motoru Algoritmasını Seçme

- **⚡ RCKangaroo v3.1 Engine (SOTA+ ~1.5 GKey/s - 8+ GKey/s)**:
  - **Kullanım Alanı**: Public Key'i bilinen harcanmış adresler ve belirli bit aralıkları (32 ile 170 bit arası, örn: Bitcoin Bulmacaları #1 - #160) için en hızlı motordur.
  - *Not*: Kanguru modu hedef Public Key gerektirir ve en fazla 170-bit aralık destekler.

- **cuBitCrack Kangaroo Engine**:
  - Native CUDA Pollard's Kangaroo motoru ($O(\sqrt{N})$). Distinguished Point (DP) çakışma tespiti kullanır.

- **Standard BitCrack Engine (Linear Multi-Hash $O(N)$)**:
  - **Kullanım Alanı**: Harcama yapılmamış (unspent) adresler, toplu adres listeleri veya tam 256-bit anahtar uzayı taramaları için kullanılır.

---

### 4. Anahtar Uzayı & Bulmaca (Puzzle) Ayarları

- **Hızlı Bulmaca Kısayolları**: Butonlara tıklayarak (**Puzzle #20**, **#40**, **#66**, **#80**, **#100**, **#120**, **#140**, **#160**, **#256**) başlangıç (`-start`) ve bitiş (`-keyspace`) hex değerlerini otomatik ayarlayabilirsiniz.
- **Bulmaca Menüsü**: Dropdown listeden **#1 ile #256** arasındaki herhangi bir bulmacayı seçebilirsiniz.
- **Özel Hex Aralığı**: **Start Key (Hex)** ve **End Key (Hex)** kutularına kendiniz özel başlangıç ve bitiş hex anahtarları girebilirsiniz.
- **Distinguished Points (-dp)**: DP bit değerini elle girebilir veya uygulamanın Kanguru algoritması için matematiksel olarak en optimal DP değerini otomatik hesaplamasına izin verebilirsiniz.

---

### 5. GPU Performans İnce Ayarları

- **Compression Mode**: Anahtar formatına göre `Compressed (-c)` (Sıkıştırılmış), `Uncompressed (-u)` (Sıkıştırılmamış) veya `Both` (Her ikisi) seçebilirsiniz.
- **GPU Blocks (-b)**: CUDA thread blok sayısı (Varsayılan: Kanguru için `1024`, Lineer için `128`).
- **Threads per Block (-t)**: Blok başına düşen iş parçacığı sayısı (Varsayılan: `256`).
- **Points per Thread (-p)**: İş parçacığı başına hesaplanan eğri noktası sayısı (Varsayılan: `32`).
- **Önceden Hesaplanmış Guide Dosyası (-tames)**: Kanguru aramasını hızlandırmak için önceden üretilmiş `.tames` / `.work` rehber dosyalarını yükleyebilirsiniz.

---

### 6. Taramayı Başlatma ve İzleme

1. **`🚀 START SCANNING`** butonuna tıklayın.
2. **Canlı Göstergeler**: Anlık tarama hızını (**MKey/s / GKey/s**), taranan toplam anahtar / DP sayacını ve geçen süreyi canlı takip edin.
3. **Konsol Günlüğü**: GPU sürücü başlatma durumunu ve tarama kayıtlarını canlı konsoldan izleyin.
4. **Anahtar Bulunduğunda**:
   - Yeşil bir başarı paneli açılarak bulunan **Adres**, **Private Key (Hex/WIF)** ve **Public Key** görüntülenir.
   - Sesli bildirim çalınır ve ekranda kutu açılır.
   - Bulunan anahtar otomatik olarak `found_keys.txt` dosyasına kaydedilir.

---

## 📊 Performans Değerleri

| GPU Modeli | Lineer Motor (Multi-Hash) | RCKangaroo Motoru (SOTA+) |
| :--- | :---: | :---: |
| **NVIDIA RTX 2060 Super** | ~477.5 MKey/s | ~1.50 GKey/s |
| **NVIDIA RTX 3080** | ~1.10 GKey/s | ~3.80 GKey/s |
| **NVIDIA RTX 4090** | ~2.50 GKey/s | ~8.00 GKey/s |

---

## 📄 Lisans

Bu proje **MIT Lisansı** altında yayınlanmıştır - detaylar için [LICENSE.MIT](LICENSE.MIT) dosyasına bakabilirsiniz.

---

> ⚠️ **Sorumluluk Reddi**: Bu yazılım yalnızca eğitim, araştırma ve kendi sahip olduğunuz cüzdanların kurtarılması veya açık kriptografik bulmaca yarışmalarına (Bitcoin Bulmacaları) katılım amacıyla geliştirilmiştir.
