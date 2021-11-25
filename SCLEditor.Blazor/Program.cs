using System.Net.Http;
using System.Threading.Tasks;
using BlazorDownloadFile;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Reductech.Utilities.SCLEditor.Blazor
{

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");

        builder.Services.AddScoped(
            sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }
        );

        builder.Services.AddBlazorDownloadFile();
        builder.Services.AddBlazoredLocalStorage();

        builder.Services.AddMudBlazorJsApi().AddMudServices();
        builder.Services.AddScoped<CompoundFileSystem>();

        await builder.Build().RunAsync();
    }
}

}
