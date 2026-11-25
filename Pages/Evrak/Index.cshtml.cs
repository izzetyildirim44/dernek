using System.Text.Json;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Evrak;

[RequestSizeLimit(12_000_000)]
public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<EvrakKayit> Liste { get; private set; } = [];
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Evrak");
    public string AraJson { get; private set; } = "[]";

    public void OnGet(int sayfa = 1)
    {
        var say = Depo.EvrakSay(DernekId);
        Sayfalar = SayfaCubugu.Olustur(say, sayfa, Yol("/Evrak"));
        Liste = Depo.EvrakListe(DernekId, Sayfalar.Atla, SayfaCubugu.Boyut);
        AraJson = JsonSerializer.Serialize(Depo.UyeAdListe(DernekId).Select(u => new { id = u.Id, ad = u.Ad }));
    }

    public IActionResult OnPostYukle(string baslik, IFormFile? dosya)
    {
        if (dosya is null || dosya.Length == 0)
        {
            Uyari = "Dosya seçin.";
            OnGet();
            return Page();
        }
        if (dosya.Length > 10_000_000)
        {
            Uyari = "Dosya en fazla 10 MB olabilir.";
            OnGet();
            return Page();
        }
        try
        {
            using var akis = dosya.OpenReadStream();
            Depo.EvrakEkle(DernekId, baslik, dosya.FileName, akis);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet();
            return Page();
        }
    }

    public IActionResult OnGetIndir(int id)
    {
        var yol = Depo.EvrakYol(DernekId, id);
        if (yol is null) return NotFound();
        var ad = Path.GetFileName(yol);
        return PhysicalFile(yol, "application/octet-stream", ad);
    }

    public IActionResult OnPostSil(int id)
    {
        if (!Yonetici) return Forbid();
        Depo.EvrakSil(DernekId, id);
        return RedirectToPage();
    }
}
