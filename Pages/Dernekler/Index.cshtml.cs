using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Dernekler;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<DernekKayit> Liste { get; private set; } = [];
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Dernekler");
    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Ad { get; set; } = "";
    [BindProperty] public string Telefon { get; set; } = "";
    [BindProperty] public string Adres { get; set; } = "";

    public void OnGet(int? id, int sayfa = 1, int yeni = 0)
    {
        var hepsi = Sistem ? Depo.DernekListe() : [];
        Sayfalar = SayfaCubugu.Olustur(hepsi.Count, sayfa, Yol("/Dernekler"));
        Liste = Sayfalar.Dilim(hepsi);
        if (yeni == 1 && Sistem)
        {
            Id = 0;
            Ad = "";
            Telefon = "";
            Adres = "";
            return;
        }
        var hedef = id ?? DernekId;
        var d = Depo.DernekGetir(hedef);
        if (d is null) return;
        Id = d.Id;
        Ad = d.Ad;
        Telefon = d.Telefon;
        Adres = d.Adres;
    }

    public IActionResult OnPostKaydet()
    {
        try
        {
            if (!Yonetici) return Forbid();
            if (Id == 0)
            {
                if (!Sistem) return Forbid();
                var yeni = Depo.DernekEkle(Ad, Telefon, Adres);
                Tamam = "Dernek eklendi.";
                return RedirectToPage(new { id = yeni });
            }
            if (!Sistem && Id != DernekId) return Forbid();
            Depo.DernekGuncelle(Id, Ad, Telefon, Adres);
            Tamam = "Kaydedildi.";
        }
        catch (Exception ex) { Uyari = ex.Message; }
        OnGet(Id == 0 ? null : Id);
        return Page();
    }

    public IActionResult OnPostSil()
    {
        if (!Sistem) return Forbid();
        try
        {
            Depo.DernekSil(Id, DernekId);
            Tamam = "Dernek silindi.";
            return LocalRedirect(Yol("/Dernekler"));
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(null);
            return Page();
        }
    }
}
