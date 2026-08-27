# 135 bit için α = 0.90 – 0.92 aralığını 30 bitlik parçalara böler
# Her parça doğrudan --keyspace olarak kullanılabilir.

n = 135
start = 1 << (n - 1)          # 2^134
end = (1 << n) - 1            # 2^135 - 1
width = end - start + 1

alpha_start = 0.90
alpha_end = 0.92

sub_start = start + int(width * alpha_start)
sub_end = start + int(width * alpha_end)
sub_width = sub_end - sub_start + 1

# 30 bit = 2^30 ≈ 1.073.741.824 adım
step = 1 << 30

num_parts = (sub_width + step - 1) // step

print(f"135 bit α=0.90-0.92 aralığı toplam {num_parts} parça.\n")
print("İlk 20 parça (keyspace olarak kullanmak için):\n")

for i in range(min(20, num_parts)):
    part_start = sub_start + i * step
    part_end = min(sub_start + (i + 1) * step - 1, sub_end)
    print(f"Parça {i+1:2d}: {part_start:040x}:{part_end:040x}")

print("\nTüm parçaları listelemek istersen range(num_parts) kullan.")