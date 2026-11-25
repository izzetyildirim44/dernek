using DernekTakip.Servisler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DernekTakip.Pages;

[AllowAnonymous]
public class KurulumModel : PageModel
{
    readonly Depo _depo;
    public KurulumModel(Depo depo) => _depo = depo;

    [BindProperty] public string DernekAd { get; set; } = "";
    [BindProperty] public string YoneticiAd { get; set; } = "";
    [BindProperty] public string KullaniciAdi { get; set; } = "";
    [BindProperty] public string Sifre { get; set; } = "";
    public string? Uyari { get; set; }

    public IActionResult OnGet()
    {
        if (_depo.KuruluMu()) return RedirectToPage("/Giris");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_depo.KuruluMu()) return RedirectToPage("/Giris");
        try
        {
            _depo.Kur(DernekAd, YoneticiAd, KullaniciAdi, Sifre);
            var k = _depo.GirisDogrula(KullaniciAdi, Sifre);
            if (k is null) return RedirectToPage("/Giris");
            var did = k.DernekId ?? _depo.DernekListe().First().Id;
            await GirisAraci.Ac(HttpContext, k, did);
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            return Page();
        }
    }
}
