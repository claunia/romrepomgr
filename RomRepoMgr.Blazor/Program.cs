using Microsoft.FluentUI.AspNetCore.Components;
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

Log.Debug("Creating the builder...");
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(); // ✅ Plug Serilog into the host

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

Log.Debug("Building the application...");
var app = builder.Build();

// Configure the HTTP request pipeline.
if(!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

Log.Debug("Running the application...");
app.Run();