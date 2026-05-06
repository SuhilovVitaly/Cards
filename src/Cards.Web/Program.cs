using Cards.Web.Components;
using Cards.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 512 * 1024);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Cards.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    // Anonymous browsing is allowed by default. Pages that require authentication
    // must opt in with the [Authorize] attribute.
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

builder.Services.AddSingleton<IUserService, JsonUserService>();
builder.Services.AddSingleton<ICollectionService, JsonCollectionService>();
builder.Services.AddSingleton<ITermCardService, JsonTermCardService>();
builder.Services.AddSingleton<IAppInfoService, AppInfoService>();
builder.Services.AddSingleton<IAdminService, AdminService>();

builder.Services.AddHttpClient<ITranslationService, GoogleTranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    // A User-Agent helps avoid 403 from the public Google translate endpoint
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 Cards.Web");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Serve recorded audio files from the writable data directory
app.MapGet("/audio/{fileName}", (string fileName, IWebHostEnvironment env) =>
{
    if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        return Results.BadRequest();

    var dir = DataPathHelper.PrepareEntityPath(env, "audio");
    var path = Path.Combine(dir, fileName);
    if (!File.Exists(path)) return Results.NotFound();

    return Results.File(path, "audio/webm");
});

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Logout endpoint: clears the auth cookie. Requires antiforgery token (validated by UseAntiforgery middleware)
app.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
