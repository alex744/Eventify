using Ya.Events.WebApi.Extensions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Services;
using Ya.Events.WebApi.Storages;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регитрация зависимостей
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddSingleton<IStorage<Event>>(sp => new InMemoryStorage<Event>(new List<Event>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Конфигурация конвейера HTTP-запросов.
app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Определяем минимальные API
app.MapGet("/hello", () => "Hello World");

app.Run();
