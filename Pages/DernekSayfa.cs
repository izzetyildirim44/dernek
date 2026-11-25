using DernekTakip.Servisler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DernekTakip.Pages;

public abstract class DernekSayfa : PageModel
{
    protected readonly Depo Depo;
    IDisposable? _bag;

    protected DernekSayfa(Depo depo) => Depo = depo;

    public int DernekId => User.DernekId();
    public bool Sistem => User.SistemMi();
    public bool Yonetici => User.YoneticiMi();
    public string Yol(string p) => (Request.PathBase.Value ?? "") + p;

    public string? Uyari { get; protected set; }
    public string? Tamam { get; protected set; }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext ctx)
    {
        _bag = Depo.IstekAc();
        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            var d = Depo.DernekGetir(DernekId);
            if (d is null)
            {
                ctx.Result = Sistem
                    ? new RedirectToPageResult("/Dernekler/Index")
                    : new RedirectToPageResult("/Giris");
                return;
            }
            ViewData["DernekAd"] = d.Ad;
            if (Sistem)
                ViewData["DernekListe"] = Depo.DernekListe();
        }
        base.OnPageHandlerExecuting(ctx);
    }

    public override void OnPageHandlerExecuted(PageHandlerExecutedContext ctx)
    {
        _bag?.Dispose();
        _bag = null;
        base.OnPageHandlerExecuted(ctx);
    }
}
