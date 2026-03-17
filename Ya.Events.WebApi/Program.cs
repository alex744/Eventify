using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регитрация зависимостей
builder.Services.AddScoped<IEventService, EventService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Конфигурация конвейера HTTP-запросов.
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Определяем минимальные API
app.MapGet("/hello", () => "Hello World");

app.Run();
