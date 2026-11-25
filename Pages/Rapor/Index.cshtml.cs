using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Rapor;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<YilRapor> Yillar { get; private set; } = [];

    public IActionResult OnGet()
    {
        if (DernekId == 0) return RedirectToPage("/Dernekler/Index");
        Yillar = Depo.AidatYilRapor(DernekId);
        return Page();
    }
}
