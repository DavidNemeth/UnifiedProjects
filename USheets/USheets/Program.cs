using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using USheets;
using USheets.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddTransient<BrowserRequestCredentialsMessageHandler>();

//builder.Services.AddScoped<ITimesheetService, LocalStorageTimesheetService>();
builder.Services.AddHttpClient<ITimesheetService, RestApiTimesheetService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7198");
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }); // This one is for general HttpClient use, ensure it doesn't conflict or remove if not needed.

// Register UserService with a configured HttpClient
// The base address for UPortal API should ideally come from configuration.
builder.Services.AddHttpClient<IUserService, UserService>(client =>
{
    client.BaseAddress = new Uri("https://dev.uportal.local:7293");
})
.AddHttpMessageHandler<BrowserRequestCredentialsMessageHandler>();

await builder.Build().RunAsync();
