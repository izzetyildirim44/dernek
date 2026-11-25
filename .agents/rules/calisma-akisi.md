---
trigger: always_on
description: Dernek Takip (.NET 10) zorunlu çalışma, Git ve Git'ten canlıya aktarım sırası kuralı
---

# Zorunlu Çalışma ve Dağıtım Sırası Kuralı

Tüm geliştirmelerde ve düzeltmelerde şu 3 adım **her zaman bu sırayla** uygulanmalıdır:

1. **Önce Yerel Klasör (`D:\Web Tasarım\Dernek Takip`):**
   - Tüm kod ve tasarım değişiklikleri önce yerel klasörde hazırlanır ve `dotnet build` ile derlenerek doğrulanır.
2. **Sonra Git (`https://github.com/izzetyildirim44/dernek.git` · `origin/main`):**
   - Yerelde tamamlanan ve doğrulanan değişiklikler her işlem bitiminde `git add -A`, `git commit` ve `git push origin main` ile GitHub `/dernek` deposuna gönderilir (`App_Data` verileri asla Git'e eklenmez).
3. **Ardından Git'ten Canlı Sunucuya (`root@94.138.209.104` · `/var/www/dernek`) Aktarım:**
   - Doğrudan yerelden dosya kopyalamak yerine sunucuda `ssh -n -i "C:\Users\izzet\.ssh\id_ed25519" -o BatchMode=yes root@94.138.209.104 "/usr/local/bin/deploy-dernek"` çalıştırılır (veya `git push live main` yapılır).
   - `deploy-dernek` betiği değişiklikleri GitHub (`origin/main`) üzerinden `/opt/dernek-src` dizinine çeker, `.NET 10` ile `Release` derlemesini alır, `/var/www/dernek/App_Data` (`program.db` ve `evrak` klasörü) verilerini koruyarak `/var/www/dernek` dizinine aktarır ve `dernek-takip.service` servisini yeniden başlatır (`https://www.evrakhane.com.tr/dernek`).
