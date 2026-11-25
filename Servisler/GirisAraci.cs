using System.Security.Claims;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DernekTakip.Servisler;

public static class GirisAraci
{
    public static async Task Ac(HttpContext ctx, KullaniciKayit k, int dernekId)
    {
        var kim = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, k.Ad),
            new Claim("kid", k.Id.ToString()),
            new Claim("did", dernekId.ToString()),
            new Claim("rol", k.Rol)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(kim),
            new AuthenticationProperties { IsPersistent = true });
    }
}
