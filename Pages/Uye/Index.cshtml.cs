using System.Text.Json;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Uye;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<UyeKayit> Liste { get; private set; } = [];
    public List<GrupKayit> Gruplar { get; private set; } = [];
    public List<int> Yillar { get; private set; } = [];
    [BindProperty] public int Id { get; set; }
    [BindProperty] public int GrupId { get; set; }
    [BindProperty] public string AdSoyad { get; set; } = "";
    [BindProperty] public string Tc { get; set; } = "";
    [BindProperty] public string Telefon { get; set; } = "";
    [BindProperty] public string Eposta { get; set; } = "";
    [BindProperty] public string Adres { get; set; } = "";
    [BindProperty] public string Durum { get; set; } = "Aktif";
    [BindProperty] public string GirisTarih { get; set; } = "";
    [BindProperty] public string AyrilisTarih { get; set; } = "";

    [BindProperty] public string DurumFiltre { get; set; } = "Aktif";
    [BindProperty] public int Yil { get; set; }
    public int UyeSayisi { get; private set; }
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Uye");
    public int OdemelerUyeId { get; private set; }
    public string OdemelerAd { get; private set; } = "";
    public List<OdemeKayit> UyeOdemeler { get; private set; } = [];
    public string AraJson { get; private set; } = "[]";
    public string AraSecilen { get; private set; } = "";
    public bool KayitPencere { get; private set; }
    public bool ExcelPencere { get; private set; }
    public bool Yazdir { get; private set; }

    public void OnGet(int? id, string? durum, int yil = 0, int odemeler = 0, int sayfa = 1, int sec = 0, int yeni = 0, int yazdir = 0, int excel = 0, int eklenen = 0, int atlanan = 0)
    {
        if (eklenen > 0 || atlanan > 0)
        {
            Tamam = eklenen > 0
                ? $"Excel dosyasından {eklenen} üye hızlıca eklendi." + (atlanan > 0 ? $" ({atlanan} mevcut kayıt atlandı.)" : "")
                : $"Seçilen dosyadaki {atlanan} üye zaten kayıtlı olduğu için yeni kayıt eklenmedi.";
        }
        Gruplar = Depo.GrupListe(DernekId);
        Yillar = Depo.KayitYillari(DernekId);
        Yil = yil == 0 ? DateTime.Now.Year : yil;
        if (!Yillar.Contains(Yil))
            Yillar.Insert(0, Yil);
        DurumFiltre = DurumNorm(durum);
        var durumFiltre = DurumFiltre == "Tümü" ? null : DurumFiltre;
        var adlar = Depo.UyeAdListe(DernekId, durumFiltre);
        UyeSayisi = adlar.Count;
        AraJson = JsonSerializer.Serialize(adlar.Select(u => new { id = u.Id, ad = u.Ad }));
        if (yazdir == 1)
        {
            Yazdir = true;
            Liste = Depo.UyeListe(DernekId, Yil, durumFiltre);
            Sayfalar = SayfaCubugu.Olustur(UyeSayisi, 1, ListeAdres());
            return;
        }
        var secilen = sec > 0 ? Depo.UyeGetir(DernekId, sec, Yil) : null;
        if (secilen is not null)
        {
            AraSecilen = secilen.AdSoyad;
            Liste = [secilen];
            Sayfalar = SayfaCubugu.Olustur(1, 1, ListeAdres());
        }
        else
        {
            if (sec > 0)
                sec = 0;
            Sayfalar = SayfaCubugu.Olustur(UyeSayisi, sayfa, ListeAdres());
            Liste = Depo.UyeListe(DernekId, Yil, durumFiltre, Sayfalar.Atla, SayfaCubugu.Boyut);
        }
        if (odemeler > 0)
        {
            var ou = Depo.UyeGetir(DernekId, odemeler, Yil);
            if (ou is null)
                return;
            OdemelerUyeId = odemeler;
            OdemelerAd = ou.AdSoyad;
            UyeOdemeler = Depo.UyeOdemeListe(DernekId, odemeler, Yil);
            return;
        }
        if (excel == 1)
        {
            ExcelPencere = true;
            return;
        }
        if (yeni == 1 && id is null)
        {
            KayitPencere = true;
            Id = 0;
            GirisTarih = DateTime.Today.ToString("yyyy-MM-dd");
            return;
        }
        if (id is null) return;
        var u = Depo.UyeGetir(DernekId, id.Value, Yil);
        if (u is null) return;
        Id = u.Id;
        GrupId = u.GrupId;
        AdSoyad = u.AdSoyad;
        Tc = u.Tc;
        Telefon = u.Telefon;
        Eposta = u.Eposta;
        Adres = u.Adres;
        Durum = u.Durum;
        GirisTarih = TarihKutu(u.GirisTarih);
        AyrilisTarih = TarihKutu(u.AyrilisTarih);
        KayitPencere = true;
    }

    static string TarihKutu(string s) => s.Length >= 10 ? s[..10] : s;

    static string DurumNorm(string? durum)
    {
        durum = (durum ?? "").Trim();
        return durum is "Tümü" or "Aktif" or "Ayrıldı" ? durum : "Aktif";
    }

    public string ListeAdres(string? durum = null, int? yil = null)
    {
        var d = DurumNorm(durum ?? DurumFiltre);
        var y = yil ?? Yil;
        if (y == 0) y = DateTime.Now.Year;
        return Yol("/Uye?durum=" + Uri.EscapeDataString(d) + "&yil=" + y);
    }

    public string YeniUyeAdres() => ListeAdres() + "&yeni=1";

    public string ExcelAdres() => ListeAdres() + "&excel=1";

    public string OrnekExcelAdres() => Yol("/Uye?handler=OrnekExcel");

    public string YazdirAdres() => ListeAdres() + "&yazdir=1";

    public string OdemelerAdres(int uyeId) => ListeAdres() + "&odemeler=" + uyeId;

    public IActionResult OnGetOrnekExcel()
    {
        var d = Depo.DernekGetir(DernekId);
        var gruplar = Depo.GrupListe(DernekId);
        var bytes = UyeExcelServisi.OrnekExcelOlustur(d?.Ad ?? "Dernek", gruplar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ornek-uye-listesi.xlsx");
    }

    public IActionResult OnPostExcelYukle(IFormFile? excelDosya, int varsayilanGrupId)
    {
        try
        {
            if (excelDosya is null || excelDosya.Length == 0)
                throw new InvalidOperationException("Lütfen yüklenecek Excel (.xlsx, .xls) veya CSV dosyasını seçin.");
            if (excelDosya.Length > 10_000_000)
                throw new InvalidOperationException("Excel dosyası en fazla 10 MB olabilir.");

            using var akis = excelDosya.OpenReadStream();
            var satirlar = UyeExcelServisi.Oku(akis, excelDosya.FileName);
            if (satirlar.Count == 0)
                throw new InvalidOperationException("Dosyada eklenecek geçerli üye satırı bulunamadı. Lütfen Örnek Excel şablonunu indirip sütunları kontrol edin.");

            var (eklenen, atlanan, _) = Depo.UyeTopluEkle(DernekId, satirlar, varsayilanGrupId);
            return LocalRedirect(ListeAdres() + $"&eklenen={eklenen}&atlanan={atlanan}");
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(null, DurumFiltre, Yil, excel: 1);
            ExcelPencere = true;
            return Page();
        }
    }

    public IActionResult OnPostKaydet()
    {
        try
        {
            Depo.UyeKaydet(DernekId, Id, GrupId, AdSoyad, Tc, Telefon, Eposta, Adres, Durum, GirisTarih, AyrilisTarih);
            Tamam = "Üye kaydedildi.";
            return LocalRedirect(ListeAdres());
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(Id == 0 ? null : Id, DurumFiltre, Yil, 0, 1, 0, Id == 0 ? 1 : 0);
            KayitPencere = true;
            return Page();
        }
    }

    public IActionResult OnPostSil(int id)
    {
        if (!Yonetici) return Forbid();
        try { Depo.UyeSil(DernekId, id); Tamam = "Üye silindi."; }
        catch (Exception ex) { Uyari = ex.Message; }
        return LocalRedirect(ListeAdres());
    }
}
