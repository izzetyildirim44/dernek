using System.Globalization;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Uye;

public class KabulModel : DernekSayfa
{
    public KabulModel(Depo depo) : base(depo) { }

    public DernekKayit Dernek { get; private set; } = new();
    public DernekProfil Profil { get; private set; } = new();
    public UyeKayit? Uye { get; private set; }

    public IActionResult OnGet(int? id)
    {
        var d = Depo.DernekGetir(DernekId);
        if (d is null) return NotFound();
        Dernek = d;
        Profil = Depo.DernekProfilGetir(DernekId) ?? new DernekProfil { Id = d.Id, Ad = d.Ad };
        if (id is int uyeId && uyeId > 0)
        {
            Uye = Depo.UyeGetir(DernekId, uyeId);
            if (Uye is null) return NotFound();
        }
        return Page();
    }

    public static string TarihYazi(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length >= 10 && DateTime.TryParse(s[..10], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return t.ToString("dd.MM.yyyy");
        return s;
    }
}
