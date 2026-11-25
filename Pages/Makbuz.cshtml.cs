using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages;

public class MakbuzModel : DernekSayfa
{
    public MakbuzModel(Depo depo) : base(depo) { }

    public MakbuzKayit? Makbuz { get; private set; }

    public IActionResult OnGet(int id)
    {
        Makbuz = Depo.MakbuzGetir(DernekId, id);
        if (Makbuz is null) return NotFound();
        return Page();
    }
}
