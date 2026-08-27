#ifndef _DP_CALCULATOR_H
#define _DP_CALCULATOR_H

#include <cmath>
#include <cstdint>
#include <algorithm>

struct DPResult {
    int dp_bits;
    double expected_dp_count;   // Beklenen kaydedilecek kapan sayısı
    double expected_ram_mb;     // Beklenen RAM kullanımı (MB)
    double total_steps;         // T = Toplam beklenen adım sayısı
};

// range_bits      : aralık genişliği bit cinsinden (örn: puzzle 66 -> 65)
// num_kangaroos   : paralel koşan toplam kanguru sayısı (tame+wild)
// available_ram_mb: ayırabileceğin RAM (MB)
// entry_size_bytes: her DP kaydının boyutu (xKey + distLow + xFull + distance + type + id) ~84 byte
// use_negation_map: negation map kullanıyor musun (true ise T daha küçük çıkar)
//                   NOT: şu anki implementasyonda negation map uygulanmıyor, varsayılan false
inline DPResult computeOptimalDPBits(
    int range_bits,
    int num_kangaroos,
    double available_ram_mb = 4096.0,
    int entry_size_bytes = 84,
    bool use_negation_map = false)
{
    // 1) Toplam beklenen adım sayısı T (van Oorschot-Wiener)
    double N = std::pow(2.0, range_bits);
    double sqrtN = std::sqrt(N);
    double constant = use_negation_map ? std::sqrt(2.0) : 2.0;
    double T = constant * sqrtN;

    // Çok sayıda kanguru kullanmak T'yi birthday paradox etkisiyle düşürür
    if (num_kangaroos > 2) {
        T = T / std::sqrt(static_cast<double>(num_kangaroos) / 2.0);
    }

    // 2) RAM'e göre maksimum kaç DP kaydı tutabiliriz
    double available_ram_bytes = available_ram_mb * 1024.0 * 1024.0;
    double max_dp_entries = available_ram_bytes / entry_size_bytes;

    // 3) RAM kısıtından gelen alt sınır: k >= log2(T / max_entries)
    int k_ram_bound = static_cast<int>(std::ceil(std::log2(T / max_dp_entries)));

    // 4) Overshoot'u boğmamak için üst sınır: k <= range_bits/2 - 4
    int k_overshoot_bound = static_cast<int>(range_bits / 2.0) - 4;

    // 5) Nihai k
    int k = std::max(k_ram_bound, 0);
    k = std::min(k, k_overshoot_bound);

    // Sonuçları hesapla
    double expected_dp_count = T / std::pow(2.0, k);
    double expected_ram_mb = (expected_dp_count * entry_size_bytes) / (1024.0 * 1024.0);

    return { k, expected_dp_count, expected_ram_mb, T };
}

#endif
