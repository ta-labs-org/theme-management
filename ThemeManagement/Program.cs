using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ThemeManagement.Components;
using ThemeManagement.Data;
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

app.UseAntiforgery();

// DB バックアップダウンロード
app.MapGet("/api/backup/download", async (IConfiguration config, HttpContext context) =>
{
    var connStr = config.GetConnectionString("DefaultConnection") ?? "Data Source=theme_management.db";
    // SQLiteのパスを抽出
    var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(connStr);
    var dataSource = string.IsNullOrWhiteSpace(sqliteConnectionStringBuilder.DataSource)
        ? "theme_management.db"
        : sqliteConnectionStringBuilder.DataSource;

    if (!File.Exists(dataSource))
        return Results.NotFound("DBファイルが見つかりません");

    var tempFile = Path.Combine(Path.GetTempPath(), $"theme_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
    try
    {
        // VACUUM INTO で安全にコピー（WALのコミット済みデータを含む）
        using var conn = new SqliteConnection($"Data Source={dataSource}");
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"VACUUM INTO '{tempFile}'";
        await cmd.ExecuteNonQueryAsync();

        var bytes = await File.ReadAllBytesAsync(tempFile);
        var fileName = Path.GetFileName(tempFile);
        return Results.File(bytes, "application/octet-stream", fileName);
    }
    finally
    {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
