using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using RomRepoMgr.Blazor.Components;
using RomRepoMgr.Database;
using RomRepoMgr.Settings;
using Serilog;
using Serilog.Events;

// Start the application
Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().Enrich.FromLogContext().CreateLogger();

Log.Information("Welcome to ROM Repository Manager!");
Log.Information("Copyright © 2020-2026 Natalia Portillo");

// Configuration and settings
// We need the builder now
Log.Debug("Creating the builder...");
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Access the full configuration
Log.Debug("Creating the configuration reader...");
ConfigurationManager config = builder.Configuration;

string logFile         = config["LogFile"]                  ?? "logs/rom-repo-mgr.log";
string defaultLogLevel = config["Logging:LogLevel:Default"] ?? "Information";

// Parse to LogEventLevel
if(!Enum.TryParse(defaultLogLevel, true, out LogEventLevel level))
{
    // Fallback if parsing fails
#if DEBUG
    level = LogEventLevel.Debug;
#else
    level = LogEventLevel.Information;
#endif
}

// Now create a logger with the specified log level and log file
Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.Console()
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1048576)
            .Enrich.FromLogContext()
            .CreateLogger();

// Read the rest of the configuration and settings
Log.Debug("Reading configuration settings...");
string repoFolder            = config["DataFolders:Repository"] ?? "repo";
string importRoms            = config["DataFolders:ImportRoms"] ?? "incoming";
string importDats            = config["DataFolders:ImportDats"] ?? "incoming-dats";
string exportRoms            = config["DataFolders:ExportRoms"] ?? "export";
string exportDats            = config["DataFolders:ExportDats"] ?? "export-dats";
string databaseFolder        = config["DataFolders:Database"]   ?? "db";
string temporaryFolder       = config["DataFolders:Temporary"]  ?? "tmp";
string compressionTypeString = config["CompressionType"]        ?? "Zstd";

// Parse the compression type
if(!Enum.TryParse(compressionTypeString, true, out CompressionType compressionType))
{
    // Fallback if parsing fails
    compressionType = CompressionType.Zstd;
}

// Ensure the folders exist
Log.Information("Ensuring folders exist...");

string[] folders = [repoFolder, importRoms, importDats, databaseFolder, exportRoms, exportDats, temporaryFolder];

foreach(string folder in folders)
{
    // Check File.Exists for symlinks or junctions
    if(!Directory.Exists(folder) && !File.Exists(folder))
    {
        Log.Debug("Creating folder: {Folder}", folder);
        Directory.CreateDirectory(folder);
    }
    else
        Log.Debug("Folder already exists: {Folder}", folder);
}

// ✅ Plug Serilog into the host
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// Localization
builder.Services.AddLocalization();

Log.Debug("Creating database context...");

builder.Services.AddDbContextFactory<Context>(options =>
{
    options.UseSqlite($"Data Source={databaseFolder}/database.db");
#if DEBUG
    options.EnableSensitiveDataLogging();
    options.LogTo(Log.Debug);
#else
    options.LogTo(Log.Information, LogLevel.Information);
#endif
});

builder.Services.AddDataGridEntityFrameworkAdapter();

Log.Debug("Setting the settings...");

Settings.Current = new SetSettings
{
    DatabasePath            = Path.Combine(Environment.CurrentDirectory, databaseFolder, "database.db"),
    TemporaryFolder         = Path.Combine(Environment.CurrentDirectory, temporaryFolder),
    RepositoryPath          = Path.Combine(Environment.CurrentDirectory, repoFolder),
    UseInternalDecompressor = true,
    Compression             = compressionType
};

Log.Debug("Building the application...");
WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if(!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Localization
string[] supportedCultures = ["en", "es"];

RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions().SetDefaultCulture("en")
   .AddSupportedCultures(supportedCultures)
   .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

Stopwatch stopwatch = new();

using(IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;

    try
    {
        Log.Information("Updating the database...");
        stopwatch.Start();
        Context dbContext = services.GetRequiredService<Context>();
        await dbContext.Database.MigrateAsync();
        stopwatch.Stop();
        Log.Debug("Database migration: {Elapsed} seconds", stopwatch.Elapsed.TotalSeconds);
    }
    catch(Exception ex)
    {
        Log.Error(ex, "An error occurred while updating the database");

        return;
    }
}

Log.Debug("Running the application...");
app.Run();