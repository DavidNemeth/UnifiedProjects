using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using USheets;
using USheets.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddScoped<ITimesheetService, LocalStorageTimesheetService>();
builder.Services.AddHttpClient<ITimesheetService, RestApiTimesheetService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7198"); 
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }); // This one is for general HttpClient use, ensure it doesn't conflict or remove if not needed.

await builder.Build().RunAsync();
