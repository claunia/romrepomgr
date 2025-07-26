using Microsoft.FluentUI.AspNetCore.Components;
using RomRepoMgr.Blazor;
using RomRepoMgr.Blazor.Components;
using Serilog;

Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateLogger();

Log.Information("Welcome to ROM Repository Manager!");
Log.Information("Copyright © 2020-2025 Natalia Portillo");

// Ensure the folders exist
Log.Information("Ensuring folders exist...");

string[] folders =
[
    Consts.DbFolder, Consts.DatFolder, Consts.IncomingDatFolder, Consts.RepositoryFolder, Consts.IncomingRomsFolder
];

foreach(string folder in folders)
{
    if(!Directory.Exists(folder))
    {
        Log.Debug("Creating folder: {Folder}", folder);
        Directory.CreateDirectory(folder);
    }
    else
        Log.Debug("Folder already exists: {Folder}", folder);
}

// Ensure the temporary folder exists but it can also be a symlink
if(!Directory.Exists(Consts.TemporaryFolder) && !File.Exists(Consts.TemporaryFolder))
{
    Log.Debug("Creating folder: {TemporaryFolder}", Consts.TemporaryFolder);
    Directory.CreateDirectory(Consts.TemporaryFolder);
}
else
    Log.Debug("Folder already exists: {TemporaryFolder}", Consts.TemporaryFolder);

Log.Debug("Creating the builder...");
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(); // ✅ Plug Serilog into the host

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

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

Log.Debug("Running the application...");
app.Run();