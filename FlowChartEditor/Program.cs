using FlowChartEditor;
using FlowChartEditor.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<FlowChartService>();
builder.Services.AddScoped<SyntaxValidator>();
builder.Services.AddScoped<UfaLoader>();

// HttpClient for loading YAML files from wwwroot
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

var host = builder.Build();

// Load UFA definitions before first render
var ufaLoader = host.Services.GetRequiredService<UfaLoader>();
await ufaLoader.LoadAsync();

await host.RunAsync();
