using System.Security.Claims;

namespace DernekTakip.Servisler;

public static class Oturum
{
    public const string Sistem = "Sistem";
    public const string Yonetici = "Yonetici";
    public const string Kullanici = "Kullanici";

    public static int KullaniciId(this ClaimsPrincipal u) =>
        int.TryParse(u.FindFirstValue("kid"), out var n) ? n : 0;

    public static int DernekId(this ClaimsPrincipal u) =>
        int.TryParse(u.FindFirstValue("did"), out var n) ? n : 0;

    public static string Rol(this ClaimsPrincipal u) =>
        u.FindFirstValue("rol") ?? Kullanici;

    public static string Ad(this ClaimsPrincipal u) =>
        u.FindFirstValue(ClaimTypes.Name) ?? "";

    public static bool SistemMi(this ClaimsPrincipal u) => u.Rol() == Sistem;

    public static bool YoneticiMi(this ClaimsPrincipal u) =>
        u.SistemMi() || u.Rol() == Yonetici;

    public static string RolYazi(string rol) => rol switch
    {
        Sistem => "Sistem",
        Yonetici => "Yönetici",
        _ => "Kullanıcı"
    };
}
