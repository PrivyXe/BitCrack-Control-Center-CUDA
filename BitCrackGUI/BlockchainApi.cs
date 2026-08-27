using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BitCrackGUI
{
    public class AddressCheckResult
    {
        public string Address { get; set; } = "";
        public bool IsValid { get; set; }
        public bool HasTransactions { get; set; }
        public bool HasSpentTx { get; set; }
        public long TotalReceivedSats { get; set; }
        public long TotalSpentSats { get; set; }
        public string PublicKeyHex { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class BlockchainApi
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static async Task<AddressCheckResult> CheckAddressAsync(string address)
        {
            var result = new AddressCheckResult { Address = address };

            if (string.IsNullOrWhiteSpace(address))
            {
                result.Message = "Lütfen geçerli bir Bitcoin adresi veya Public Key girin.";
                return result;
            }

            string cleanStr = address.Trim();

            // 1. Direct Public Key Check (66 hex chars starting 02/03, or 130 hex chars starting 04)
            if ((cleanStr.Length == 66 && (cleanStr.StartsWith("02", StringComparison.OrdinalIgnoreCase) || cleanStr.StartsWith("03", StringComparison.OrdinalIgnoreCase))) ||
                (cleanStr.Length == 130 && cleanStr.StartsWith("04", StringComparison.OrdinalIgnoreCase)))
            {
                result.IsValid = true;
                result.HasSpentTx = true;
                result.PublicKeyHex = cleanStr;
                result.Message = $"✅ Doğrudan Public Key Girildi: {cleanStr.Substring(0, 16)}... (Kanguru Modu Kullanılabilir!)";
                return result;
            }

            try
            {
                // 2. Query Mempool.space Address Info
                string url = $"https://mempool.space/api/address/{cleanStr}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    result.Message = $"API Hatası ({response.StatusCode}): Adres bulunamadı veya ağ yanıt vermedi.";
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("chain_stats", out var chainStats))
                {
                    result.IsValid = true;
                    int txCount = chainStats.GetProperty("tx_count").GetInt32();
                    long fundedSats = chainStats.GetProperty("funded_txo_sum").GetInt64();
                    long spentSats = chainStats.GetProperty("spent_txo_sum").GetInt64();

                    result.HasTransactions = txCount > 0;
                    result.TotalReceivedSats = fundedSats;
                    result.TotalSpentSats = spentSats;

                    if (spentSats > 0 || txCount > 1)
                    {
                        result.HasSpentTx = true;
                        result.Message = $"✅ Harcama Bulundu! (İşlem: {txCount}, Harcanan: {spentSats / 100000000.0:F8} BTC).";

                        // Try fetching outgoing TXs across all pages to extract Public Key
                        string pubKey = await FetchPublicKeyFromTxsAsync(cleanStr);
                        if (!string.IsNullOrEmpty(pubKey))
                        {
                            result.PublicKeyHex = pubKey;
                            result.Message += $" Public Key elde edildi: {pubKey.Substring(0, 16)}... (Kanguru Modu Hazır!)";
                        }
                        else
                        {
                            result.Message += " UYARI: Harcama yapılmış ancak Public Key API sayfalarında bulunamadı. Lütfen Public Key'i elle girin.";
                        }
                    }
                    else if (txCount > 0)
                    {
                        result.Message = $"ℹ️ Sadece Gelen İşlem Var ({fundedSats / 100000000.0:F8} BTC). Harcama yapılmadığı için Public Key henüz ağa yazılmamış. BitCrack Lineer Modu önerilir!";
                    }
                    else
                    {
                        result.Message = "ℹ️ Bu adreste henüz hiçbir blokzincir işlemi (0 tx) bulunmuyor.";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Bağlantı Hatası: {ex.Message}";
            }

            return result;
        }

        private static async Task<string> FetchPublicKeyFromTxsAsync(string address)
        {
            string[] apiBases = new string[]
            {
                "https://mempool.space/api",
                "https://blockstream.info/api"
            };

            foreach (var apiBase in apiBases)
            {
                try
                {
                    string lastTxId = "";
                    for (int page = 0; page < 10; page++) // Paginate up to 10 pages (500 transactions)
                    {
                        string url = $"{apiBase}/address/{address}/txs" + (!string.IsNullOrEmpty(lastTxId) ? $"/chain/{lastTxId}" : "");
                        HttpResponseMessage response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode) break;

                        string json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.ValueKind != JsonValueKind.Array) break;

                        int count = 0;
                        foreach (var tx in doc.RootElement.EnumerateArray())
                        {
                            count++;
                            if (tx.TryGetProperty("txid", out var tId))
                            {
                                lastTxId = tId.GetString() ?? "";
                            }

                            if (!tx.TryGetProperty("vin", out var vins) || vins.ValueKind != JsonValueKind.Array) continue;

                            foreach (var vin in vins.EnumerateArray())
                            {
                                // If prevout exists, ensure it matches target address (case-insensitive)
                                if (vin.TryGetProperty("prevout", out var prevout) && prevout.ValueKind == JsonValueKind.Object)
                                {
                                    string prevAddr = prevout.TryGetProperty("scriptpubkey_address", out var a) ? a.GetString() ?? "" : "";
                                    if (!string.IsNullOrEmpty(prevAddr) && !string.Equals(prevAddr, address, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue; // Input spent from a different address in a multi-input transaction
                                    }
                                }

                                // 1. ScriptSig ASM parsing
                                if (vin.TryGetProperty("scriptsig_asm", out var scriptSigAsm))
                                {
                                    string asm = scriptSigAsm.GetString() ?? "";
                                    var parts = asm.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var part in parts)
                                    {
                                        string cleaned = part.Trim();
                                        if ((cleaned.Length == 66 && (cleaned.StartsWith("02", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("03", StringComparison.OrdinalIgnoreCase))) ||
                                            (cleaned.Length == 130 && cleaned.StartsWith("04", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            return cleaned;
                                        }
                                    }
                                }

                                // 2. ScriptSig Raw Hex parsing
                                if (vin.TryGetProperty("scriptsig", out var scriptSigHex))
                                {
                                    string hex = scriptSigHex.GetString() ?? "";
                                    if (hex.Length >= 66)
                                    {
                                        string last66 = hex.Substring(hex.Length - 66);
                                        if (last66.StartsWith("02", StringComparison.OrdinalIgnoreCase) || last66.StartsWith("03", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return last66;
                                        }
                                    }
                                    if (hex.Length >= 130)
                                    {
                                        string last130 = hex.Substring(hex.Length - 130);
                                        if (last130.StartsWith("04", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return last130;
                                        }
                                    }
                                }

                                // 3. Witness Array parsing (SegWit)
                                if (vin.TryGetProperty("witness", out var witness) && witness.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in witness.EnumerateArray())
                                    {
                                        string part = item.GetString() ?? "";
                                        if ((part.Length == 66 && (part.StartsWith("02", StringComparison.OrdinalIgnoreCase) || part.StartsWith("03", StringComparison.OrdinalIgnoreCase))) ||
                                            (part.Length == 130 && part.StartsWith("04", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            return part;
                                        }
                                    }
                                }
                            }
                        }

                        if (count < 25) break; // Reached end of transactions history
                    }
                }
                catch
                {
                    // Fallback to next endpoint
                }
            }

            return "";
        }
    }
}
