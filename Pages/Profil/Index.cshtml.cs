using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Profil;

[RequestSizeLimit(10_000_000)]
public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public DernekProfil P { get; private set; } = new();
    public string LogoAdres { get; private set; } = "";

    void LogoYenile()
    {
        var yol = Depo.DernekLogoGoreliYol(DernekId);
        LogoAdres = yol.Length > 0 ? Yol(yol) : "";
        ViewData["DernekLogo"] = LogoAdres;
    }

    public IActionResult OnGet()
    {
        var p = Depo.DernekProfilGetir(DernekId);
        if (p is null) return RedirectToPage("/Dernekler/Index");
        P = p;
        LogoYenile();
        return Page();
    }

    public IActionResult OnPost(
        IFormFile? LogoDosya,
        [FromForm] int LogoKaldir,
        [FromForm] string? Ad,
        [FromForm] string? KisaAd,
        [FromForm] string? KutukNo,
        [FromForm] string? KurulusTarihi,
        [FromForm] string? Statu,
        [FromForm] string? FaaliyetAlani,
        [FromForm] string? VergiDairesi,
        [FromForm] string? VergiNo,
        [FromForm] string? Amac,
        [FromForm] string? GorevDonemi,
        [FromForm] string? SonGenelKurulTarihi,
        [FromForm] string? KararDefterNo,
        [FromForm] string? Baskan,
        [FromForm] string? BaskanTelefon,
        [FromForm] string[]? ByAd,
        [FromForm] string[]? ByUnvan,
        [FromForm] string[]? ByTel,
        [FromForm] string? GenelSekreter,
        [FromForm] string? GenelSekreterTelefon,
        [FromForm] string? Sayman,
        [FromForm] string? SaymanTelefon,
        [FromForm] string? Veznedar,
        [FromForm] string? VeznedarTelefon,
        [FromForm] string[]? YkAd,
        [FromForm] string[]? YkUnvan,
        [FromForm] string[]? YkTel,
        [FromForm] string? DenetimBaskani,
        [FromForm] string? DenetimBaskaniTelefon,
        [FromForm] string[]? DkAd,
        [FromForm] string[]? DkUnvan,
        [FromForm] string[]? DkTel,
        [FromForm] string? Telefon,
        [FromForm] string? Telefon2,
        [FromForm] string? Eposta,
        [FromForm] string? KepAdresi,
        [FromForm] string? WebSitesi,
        [FromForm] string? SosyalMedya,
        [FromForm] string? Il,
        [FromForm] string? Ilce,
        [FromForm] string? Adres,
        [FromForm] string[]? BkBanka,
        [FromForm] string[]? BkHesap,
        [FromForm] string[]? BkIban,
        [FromForm] string? Notlar)
    {
        var p = new DernekProfil
        {
            Id = DernekId,
            Ad = Temiz(Ad),
            KisaAd = Temiz(KisaAd),
            KutukNo = Temiz(KutukNo),
            KurulusTarihi = Temiz(KurulusTarihi),
            Statu = string.IsNullOrWhiteSpace(Statu) ? "Dernek" : Temiz(Statu),
            FaaliyetAlani = Temiz(FaaliyetAlani),
            VergiDairesi = Temiz(VergiDairesi),
            VergiNo = Temiz(VergiNo),
            Amac = Temiz(Amac),
            GorevDonemi = Temiz(GorevDonemi),
            SonGenelKurulTarihi = Temiz(SonGenelKurulTarihi),
            KararDefterNo = Temiz(KararDefterNo),
            Baskan = Temiz(Baskan),
            BaskanTelefon = Temiz(BaskanTelefon),
            BaskanYardimcilari = GorevliTopla(ByAd, ByUnvan, ByTel),
            GenelSekreter = Temiz(GenelSekreter),
            GenelSekreterTelefon = Temiz(GenelSekreterTelefon),
            Sayman = Temiz(Sayman),
            SaymanTelefon = Temiz(SaymanTelefon),
            Veznedar = Temiz(Veznedar),
            VeznedarTelefon = Temiz(VeznedarTelefon),
            YonetimUyeleri = GorevliTopla(YkAd, YkUnvan, YkTel),
            DenetimBaskani = Temiz(DenetimBaskani),
            DenetimBaskaniTelefon = Temiz(DenetimBaskaniTelefon),
            DenetimUyeleri = GorevliTopla(DkAd, DkUnvan, DkTel),
            Telefon = Temiz(Telefon),
            Telefon2 = Temiz(Telefon2),
            Eposta = Temiz(Eposta),
            KepAdresi = Temiz(KepAdresi),
            WebSitesi = Temiz(WebSitesi),
            SosyalMedya = Temiz(SosyalMedya),
            Il = Temiz(Il),
            Ilce = Temiz(Ilce),
            Adres = Temiz(Adres),
            BankaHesaplari = BankaTopla(BkBanka, BkHesap, BkIban),
            Notlar = Temiz(Notlar)
        };

        try
        {
            if (!Yonetici)
                throw new InvalidOperationException("Profil bilgilerini güncellemek için yönetici yetkisi gerekir.");

            if (LogoDosya is not null && LogoDosya.Length > 0)
            {
                if (LogoDosya.Length > 5_000_000)
                    throw new InvalidOperationException("Logo dosyası en fazla 5 MB olabilir.");
                using var akis = LogoDosya.OpenReadStream();
                Depo.DernekLogoKaydet(DernekId, LogoDosya.FileName, akis);
            }
            else if (LogoKaldir == 1)
            {
                Depo.DernekLogoSil(DernekId);
            }

            Depo.DernekProfilKaydet(DernekId, p);
            P = Depo.DernekProfilGetir(DernekId) ?? p;
            ViewData["DernekAd"] = P.Ad;
            LogoYenile();
            if (Sistem)
                ViewData["DernekListe"] = Depo.DernekListe();
            Tamam = "Dernek profili, logo ve kurul bilgileri kaydedildi.";
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            P = p;
            LogoYenile();
        }
        return Page();
    }

    static string Temiz(string? s) => (s ?? "").Trim();

    static List<GorevliKayit> GorevliTopla(string[]? adlar, string[]? unvanlar, string[]? teller)
    {
        var liste = new List<GorevliKayit>();
        var n = Math.Max(adlar?.Length ?? 0, Math.Max(unvanlar?.Length ?? 0, teller?.Length ?? 0));
        for (var i = 0; i < n; i++)
        {
            var ad = i < (adlar?.Length ?? 0) ? Temiz(adlar![i]) : "";
            var unvan = i < (unvanlar?.Length ?? 0) ? Temiz(unvanlar![i]) : "";
            var tel = i < (teller?.Length ?? 0) ? Temiz(teller![i]) : "";
            if (ad.Length == 0 && unvan.Length == 0 && tel.Length == 0) continue;
            liste.Add(new GorevliKayit { AdSoyad = ad, Unvan = unvan, Telefon = tel });
        }
        return liste;
    }

    static List<BankaKayit> BankaTopla(string[]? bankalar, string[]? hesaplar, string[]? ibanlar)
    {
        var liste = new List<BankaKayit>();
        var n = Math.Max(bankalar?.Length ?? 0, Math.Max(hesaplar?.Length ?? 0, ibanlar?.Length ?? 0));
        for (var i = 0; i < n; i++)
        {
            var b = i < (bankalar?.Length ?? 0) ? Temiz(bankalar![i]) : "";
            var h = i < (hesaplar?.Length ?? 0) ? Temiz(hesaplar![i]) : "";
            var ib = i < (ibanlar?.Length ?? 0) ? Temiz(ibanlar![i]) : "";
            if (b.Length == 0 && h.Length == 0 && ib.Length == 0) continue;
            liste.Add(new BankaKayit { BankaAdi = b, HesapAdi = h, Iban = ib });
        }
        return liste;
    }
}
