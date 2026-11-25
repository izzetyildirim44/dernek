using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public PanelOzet Ozet { get; private set; } = new();
    public List<int> Yillar { get; private set; } = [];
    public int Yil { get; private set; }

    public IActionResult OnGet(int yil = 0)
    {
        if (DernekId == 0) return RedirectToPage("/Dernekler/Index");
        Yillar = Depo.KayitYillari(DernekId);
        Yil = yil == 0 ? DateTime.Now.Year : yil;
        if (!Yillar.Contains(Yil))
            Yillar.Insert(0, Yil);
        Ozet = Depo.Ozet(DernekId, Yil);
        return Page();
    }

    public async Task<IActionResult> OnPostDernekSecAsync(int dernekId)
    {
        if (!Sistem) return Forbid();
        var d = Depo.DernekGetir(dernekId);
        if (d is null) return RedirectToPage("/Dernekler/Index");
        var k = new KullaniciKayit
        {
            Id = User.KullaniciId(),
            Ad = User.Ad(),
            Rol = User.Rol()
        };
        await GirisAraci.Ac(HttpContext, k, dernekId);
        return RedirectToPage("/Index");
    }
}
