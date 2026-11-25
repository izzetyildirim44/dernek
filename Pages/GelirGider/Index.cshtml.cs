using System.Text.Json;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.GelirGider;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<KasaKayit> Liste { get; private set; } = [];
    public List<int> Yillar { get; private set; } = [];
    public int Yil { get; private set; }
    public decimal Gelir { get; private set; }
    public decimal Gider { get; private set; }
    public string AraJson { get; private set; } = "[]";
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/GelirGider");

    public void OnGet(int yil = 0, int sayfa = 1)
    {
        Yillar = Depo.KayitYillari(DernekId);
        Yil = yil == 0 ? DateTime.Now.Year : yil;
        if (!Yillar.Contains(Yil))
            Yillar.Insert(0, Yil);
        var ozet = Depo.KasaOzet(DernekId, Yil);
        Gelir = ozet.Gelir;
        Gider = ozet.Gider;
        Sayfalar = SayfaCubugu.Olustur(ozet.Say, sayfa, Yol("/GelirGider?yil=" + Yil));
        Liste = Depo.KasaListe(DernekId, Yil, Sayfalar.Atla, SayfaCubugu.Boyut);
        AraJson = JsonSerializer.Serialize(Depo.UyeListe(DernekId, Yil).Select(u => new
        {
            id = u.Id,
            ad = u.AdSoyad,
            tc = u.Tc,
            kalan = u.Kalan > 0 ? Depo.Tl(u.Kalan) : ""
        }));
    }

    public IActionResult OnPostEkle(string tur, decimal tutar, DateTime tarih, string aciklama, string? makbuz, int uyeId, int yil)
    {
        try
        {
            var t = tarih == default ? DateTime.Today : tarih;
            Depo.KasaEkle(DernekId, t, tur, tutar, aciklama, makbuz ?? "", uyeId);
            Tamam = "Kasa hareketi kaydedildi.";
            return RedirectToPage(new { yil = t.Year });
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(yil);
            return Page();
        }
    }

    public IActionResult OnPostSil(int id, int yil)
    {
        if (!Yonetici) return Forbid();
        try { Depo.KasaSil(DernekId, id); }
        catch (Exception ex) { Uyari = ex.Message; OnGet(yil); return Page(); }
        return RedirectToPage(new { yil });
    }
}
