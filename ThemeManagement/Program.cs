using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QuestPDF.Infrastructure;
using ThemeManagement.Components;
using ThemeManagement.Data;
using ThemeManagement.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application services
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IEngineerService, EngineerService>();
builder.Services.AddScoped<IWorkDayService, WorkDayService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IJapaneseBusinessDayService, JapaneseBusinessDayService>();
builder.Services.AddSingleton<ICapacitySettings, CapacitySettings>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ===== PDF レポートエンドポイント =====
app.MapGet("/api/report/pdf", async (
    string type,
    IReportPdfService pdfService,
    int? year,
    int? month,
    int? fiscalYear,
    string? half) =>
{
    try
    {
        byte[] pdfBytes;
        string fileName;

        if (type == "dashboard")
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;
            pdfBytes = await pdfService.GenerateDashboardPdfAsync(y, m);
            fileName = $"dashboard_{y}{m:D2}.pdf";
        }
        else if (type == "forecast")
        {
            var fy = fiscalYear ?? (DateTime.Today.Month >= 4 ? DateTime.Today.Year : DateTime.Today.Year - 1);
            var isFirst = !(half == "second");
            pdfBytes = await pdfService.GenerateForecastPdfAsync(fy, isFirst);
            var halfLabel = isFirst ? "first" : "second";
            fileName = $"forecast_{fy}_{halfLabel}.pdf";
        }
        else
        {
            return Results.BadRequest("type パラメータには 'dashboard' または 'forecast' を指定してください。");
        }

        return Results.File(pdfBytes, "application/pdf", fileName);
    }
    catch (Exception ex)
    {
        return Results.Problem($"PDF 生成中にエラーが発生しました: {ex.Message}");
    }
})
.WithName("GetReportPdf")
.WithTags("Reports");

app.Run();
