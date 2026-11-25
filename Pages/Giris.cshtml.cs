using DernekTakip.Servisler;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DernekTakip.Pages;

[AllowAnonymous]
public class GirisModel : PageModel
{
    readonly Depo _depo;
    public GirisModel(Depo depo) => _depo = depo;

    [BindProperty] public string KullaniciAdi { get; set; } = "";
    [BindProperty] public string Sifre { get; set; } = "";
    public string? Uyari { get; set; }

    public IActionResult OnGet()
    {
        if (!_depo.KuruluMu()) return RedirectToPage("/Kurulum");
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var k = _depo.GirisDogrula(KullaniciAdi, Sifre);
        if (k is null)
        {
            Uyari = "Kullanıcı adı veya şifre yanlış.";
            return Page();
        }
        int did;
        if (k.Rol == Oturum.Sistem)
            did = k.DernekId ?? _depo.DernekListe().FirstOrDefault()?.Id ?? 0;
        else
            did = k.DernekId ?? 0;
        if (did == 0 || _depo.DernekGetir(did) is null)
        {
            Uyari = "Tanımlı dernek yok.";
            return Page();
        }
        await GirisAraci.Ac(HttpContext, k, did);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostCikisAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Giris");
    }
}
