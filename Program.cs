using System.Globalization;
using DernekTakip.Servisler;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var tr = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = tr;
CultureInfo.DefaultThreadCurrentUICulture = tr;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(5130);
    o.Limits.MaxRequestBodySize = 12_000_000;
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddSingleton<Depo>();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Giris";
        o.LogoutPath = "/Giris";
        o.Cookie.Name = "DernekTakip";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Giris");
    o.Conventions.AllowAnonymousToPage("/Kurulum");
    o.Conventions.AllowAnonymousToPage("/Error");
}).AddMvcOptions(o =>
{
    o.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UsePathBase("/dernek");
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/dernek", out var kalan))
    {
        var hedef = (ctx.Request.PathBase.Value ?? "/dernek") + (kalan.HasValue && kalan.Value.Length > 0 ? kalan.Value : "/") + ctx.Request.QueryString;
        ctx.Response.Redirect(hedef, permanent: true);
        return;
    }
    await next();
});
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
_ = app.Services.GetRequiredService<Depo>();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    if (p.StartsWith("/Kurulum", StringComparison.OrdinalIgnoreCase) || p.Contains('.', StringComparison.Ordinal))
    {
        await next();
        return;
    }
    var depo = ctx.RequestServices.GetRequiredService<Depo>();
    if (!depo.KuruluMu())
    {
        ctx.Response.Redirect(ctx.Request.PathBase + "/Kurulum");
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/DernekLogo/{id:int}", IResult (int id, Depo depo, HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!ctx.User.SistemMi() && ctx.User.DernekId() != id) return Results.Forbid();
    var l = depo.DernekLogoBul(id);
    if (l is null) return Results.NotFound();
    ctx.Response.Headers.CacheControl = "private, max-age=86400";
    return Results.Stream(File.OpenRead(l.Value.Yol), l.Value.ContentType);
});
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
