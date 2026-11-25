using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;

namespace DernekTakip.Pages.Kullanicilar;

public class IndexModel : DernekSayfa
{
    public IndexModel(Depo depo) : base(depo) { }

    public List<KullaniciKayit> Liste { get; private set; } = [];
    public int KullaniciSayisi { get; private set; }
    public SayfaCubugu Sayfalar { get; private set; } = SayfaCubugu.Olustur(0, 1, "/Kullanicilar");
    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Ad { get; set; } = "";
    [BindProperty] public string KullaniciAdi { get; set; } = "";
    [BindProperty] public string Sifre { get; set; } = "";
    [BindProperty] public string Rol { get; set; } = Oturum.Kullanici;

    public void OnGet(int? id, int sayfa = 1)
    {
        KullaniciSayisi = Depo.KullaniciSay(DernekId, Sistem);
        Sayfalar = SayfaCubugu.Olustur(KullaniciSayisi, sayfa, Yol("/Kullanicilar"));
        Liste = Depo.KullaniciListe(DernekId, Sistem, Sayfalar.Atla, SayfaCubugu.Boyut);
        if (id is null) return;
        var k = Depo.KullaniciGetir(id.Value, DernekId, Sistem);
        if (k is null) return;
        Id = k.Id;
        Ad = k.Ad;
        KullaniciAdi = k.KullaniciAdi;
        Rol = k.Rol;
        Sifre = "";
    }

    public IActionResult OnPostKaydet()
    {
        if (!Yonetici) return Forbid();
        try
        {
            if (!Sistem && Rol == Oturum.Sistem) Rol = Oturum.Yonetici;
            if (Id == 0)
            {
                var did = Rol == Oturum.Sistem ? (int?)null : DernekId;
                Depo.KullaniciEkle(did, Ad, KullaniciAdi, Sifre, Rol);
                Tamam = "Kullanıcı eklendi.";
            }
            else
            {
                if (Depo.KullaniciGetir(Id, DernekId, Sistem) is null)
                    return Forbid();
                Depo.KullaniciGuncelle(Id, Ad, KullaniciAdi, Sifre, Rol);
                Tamam = "Kullanıcı güncellendi.";
            }
            return LocalRedirect(Yol("/Kullanicilar"));
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(Id == 0 ? null : Id);
            return Page();
        }
    }

    public IActionResult OnPostSil()
    {
        if (!Yonetici) return Forbid();
        try
        {
            if (Depo.KullaniciGetir(Id, DernekId, Sistem) is null)
                return Forbid();
            if (Depo.KullaniciSay(DernekId, Sistem) <= 1)
                throw new InvalidOperationException("Son kullanıcı silinemez.");
            Depo.KullaniciSil(Id, User.KullaniciId());
            return LocalRedirect(Yol("/Kullanicilar"));
        }
        catch (Exception ex)
        {
            Uyari = ex.Message;
            OnGet(null);
            return Page();
        }
    }
}
