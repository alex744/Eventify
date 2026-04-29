using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ya.Events.WebApi.Tests.Fixtures;

public class WebApiFactory : WebApplicationFactory<Program>
{
    // При необходимости можно переопределить конфигурацию,
    // подменить сервисы, использовать InMemory-хранилища и т.д.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Пример: замена реального IBookingStore на InMemoryBookingStore (если нужно)
            // services.AddSingleton<IBookingStore, InMemoryBookingStore>();
        });
    }
}
