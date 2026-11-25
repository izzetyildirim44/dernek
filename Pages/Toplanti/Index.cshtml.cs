using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Toplanti;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<ToplantiKayit> Liste { get; private set; } = [];
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Toplanti");
    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Tarih { get; set; } = "";
    [BindProperty] public string Baslik { get; set; } = "";
    [BindProperty] public string Notlar { get; set; } = "";

    public void OnGet(int? id, int sayfa = 1)
    {
        var say = Depo.ToplantiSay(DernekId);
        Sayfalar = SayfaCubugu.Olustur(say, sayfa, Yol("/Toplanti"));
        Liste = Depo.ToplantiListe(DernekId, Sayfalar.Atla, SayfaCubugu.Boyut);
        if (id is null) return;
        var t = Depo.ToplantiGetir(DernekId, id.Value);
        if (t is null) return;
        Id = t.Id;
        Tarih = t.Tarih;
        Baslik = t.Baslik;
        Notlar = t.Notlar;
    }

    public IActionResult OnPostKaydet()
    {
        try
        {
            var dt = DateTime.TryParse(Tarih, out var t) ? t : DateTime.Today;
            Depo.ToplantiKaydet(DernekId, Id, dt, Baslik, Notlar);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(Id == 0 ? null : Id);
            return Page();
        }
    }

    public IActionResult OnPostSil(int id)
    {
        if (!Yonetici) return Forbid();
        Depo.ToplantiSil(DernekId, id);
        return RedirectToPage();
    }
}
