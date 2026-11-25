using System.Text.Json;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Aidat;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<GrupKayit> Gruplar { get; private set; } = [];
    public string AraJson { get; private set; } = "[]";
    public List<UyeKayit> Borclular { get; private set; } = [];
    public List<OdemeKayit> Odemeler { get; private set; } = [];
    public List<int> Yillar { get; private set; } = [];
    public bool BorcluGoster { get; private set; }
    public bool TahsilatPencere { get; private set; }
    public int Sayfa { get; private set; }
    public SayfaCubugu OdemeSayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Aidat");
    public SayfaCubugu BorcSayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Aidat");
    [BindProperty] public int GrupId { get; set; }
    [BindProperty] public int Yil { get; set; }
    [BindProperty] public decimal Yillik { get; set; }
    [BindProperty] public decimal Giris { get; set; }

    public void OnGet(int grupId = 0, int yil = 0, int borclu = 0, int sayfa = 1, int bsayfa = 1, int tahsilat = 0)
    {
        Gruplar = Depo.AidatliGruplar(DernekId);
        Yillar = Depo.AidatYillari(DernekId);
        Yil = yil == 0 ? DateTime.Now.Year : yil;
        if (!Yillar.Contains(Yil))
            Yillar.Insert(0, Yil);
        if (grupId != 0 && Gruplar.All(g => g.Id != grupId))
            grupId = 0;
        GrupId = grupId == 0 && Gruplar.Count > 0 ? Gruplar[0].Id : grupId;
        var t = Depo.AidatTanimGetir(DernekId, GrupId, Yil);
        if (t is not null)
        {
            Yillik = t.Aylar[0] > 0 ? t.Aylar[0] : t.Aylar.Sum();
            Giris = t.Giris;
        }
        AraJson = JsonSerializer.Serialize(Depo.UyeAdListe(DernekId).Select(u => new { id = u.Id, ad = u.Ad, tc = u.Tc }));
        var taban = Yol("/Aidat?grupId=" + GrupId + "&yil=" + Yil);
        if (borclu == 1) taban += "&borclu=1";
        var oSay = Depo.OdemeSay(DernekId, Yil);
        OdemeSayfalar = SayfaCubugu.Olustur(oSay, sayfa, taban);
        Odemeler = Depo.OdemeListe(DernekId, Yil, OdemeSayfalar.Atla, SayfaCubugu.Boyut);
        Sayfa = OdemeSayfalar.Sayfa;
        TahsilatPencere = tahsilat == 1;
        BorcluGoster = borclu == 1;
        if (BorcluGoster)
        {
            var bSay = Depo.UyeBorcluSay(DernekId, Yil, GrupId);
            BorcSayfalar = SayfaCubugu.Olustur(bSay, bsayfa, taban, "bsayfa");
            Borclular = Depo.UyeBorcluListe(DernekId, Yil, GrupId, BorcSayfalar.Atla, SayfaCubugu.Boyut);
        }
    }

    public IActionResult OnPostBorclandir()
    {
        if (!Yonetici) return Forbid();
        try
        {
            var say = Depo.AidatKaydetVeBorclandir(DernekId, GrupId, Yil, Yillik, Giris);
            Tamam = $"Tarife kaydedildi. {say} borç satırı işlendi.";
        }
        catch (Exception ex) { Uyari = ex.Message; }
        OnGet(GrupId, Yil);
        return Page();
    }

    public IActionResult OnPostOdeme(int uyeId, decimal tutar, DateTime tarih, string? aciklama)
    {
        try
        {
            var o = Depo.OdemeEkle(DernekId, uyeId, tarih == default ? DateTime.Today : tarih, tutar, aciklama ?? "");
            return RedirectToPage("/Makbuz", new { id = o.Id });
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(GrupId, Yil, tahsilat: 1);
            return Page();
        }
    }

    public string ListeAdres()
    {
        var t = Yol("/Aidat?grupId=" + GrupId + "&yil=" + Yil);
        if (BorcluGoster) t += "&borclu=1";
        if (Sayfa > 1) t += "&sayfa=" + Sayfa;
        return t;
    }

    public string TahsilatAdres() => ListeAdres() + "&tahsilat=1";
}
