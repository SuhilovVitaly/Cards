using Cards.Web.Components;
using Cards.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Audio recordings and pasted images are streamed back from the browser to .NET
    // as base64 data URLs via JS interop (SignalR). The default 32 KB cap (and the
    // previous 512 KB) is easily exceeded by a few seconds of audio or a pasted image,
    // which silently aborts the circuit and freezes the modal on production.
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

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

// Serve recorded audio files from the writable data directory.
// The file is only returned when:
//   1) the requested name has the form "{guid}.webm" (no traversal, no other extensions);
//   2) a card with that id exists and one of its values references exactly this file
//      via AudioPath — so files left on disk but not registered are never exposed;
//   3) the resolved physical path is still inside the audio directory.
app.MapGet("/audio/{fileName}", async (string fileName, IWebHostEnvironment env, ITermCardService cardService, CancellationToken ct) =>
{
    var safeName = Path.GetFileName(fileName);
    if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
        return Results.BadRequest();

    var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
    var ext = Path.GetExtension(safeName);
    if (!string.Equals(ext, ".webm", StringComparison.OrdinalIgnoreCase) ||
        !Guid.TryParse(nameWithoutExt, out var cardId))
    {
        return Results.BadRequest();
    }

    var card = await cardService.GetByIdAsync(cardId, ct);
    if (card is null) return Results.NotFound();

    var expectedPath = $"/audio/{safeName}";
    if (!string.Equals(card.Value1.AudioPath, expectedPath, StringComparison.Ordinal) &&
        !string.Equals(card.Value2.AudioPath, expectedPath, StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    var dir = DataPathHelper.PrepareEntityPath(env, "audio");
    var fullDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
    var fullPath = Path.GetFullPath(Path.Combine(fullDir, safeName));
    var parentDir = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(fullPath) ?? string.Empty);
    if (!string.Equals(parentDir, fullDir, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest();

    if (!File.Exists(fullPath)) return Results.NotFound();

    return Results.File(fullPath, "audio/webm");
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
