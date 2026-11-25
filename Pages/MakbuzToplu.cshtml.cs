using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages;

public class MakbuzTopluModel : DernekSayfa
{
    public MakbuzTopluModel(Depo depo) : base(depo) { }

    public List<MakbuzKayit> Liste { get; private set; } = [];

    public IActionResult OnGet(int[] id)
    {
        Liste = Depo.MakbuzListe(DernekId, id ?? []);
        if (Liste.Count == 0) return RedirectToPage("/Aidat/Index");
        return Page();
    }
}
