using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ThemeManagement.Components;
using ThemeManagement.Data;
using ThemeManagement.Infrastructure;
using ThemeManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Azure App Service Easy Auth — authentication is handled by Easy Auth at the infrastructure level.
// This handler reads the X-MS-CLIENT-PRINCIPAL header forwarded by App Service and constructs
// the ClaimsPrincipal, including Entra ID App Role claims used for authorization below.
//
// In local development, set DevAuth:Enabled = true in appsettings.Development.json to bypass
// Easy Auth and auto-authenticate with a local dev identity (see DevBypassAuthHandler).
var devAuthEnabled = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevAuth:Enabled");
var authBuilder = builder.Services.AddAuthentication("EasyAuth");
if (devAuthEnabled)
    authBuilder.AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>("EasyAuth", null);
else
    authBuilder.AddScheme<AuthenticationSchemeOptions, EasyAuthAuthenticationHandler>("EasyAuth", null);

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

// Application services
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IEngineerService, EngineerService>();
builder.Services.AddScoped<IWorkDayService, WorkDayService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IJapaneseBusinessDayService, JapaneseBusinessDayService>();
builder.Services.AddSingleton<ICapacitySettings, CapacitySettings>();

var app = builder.Build();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
