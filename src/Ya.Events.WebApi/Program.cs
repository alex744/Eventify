using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.Extensions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Services;
using Ya.Events.WebApi.Services.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddHostedService<BookingProcessorService>();

var app = builder.Build();

// Инициализация базы данных при запуске приложения.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Конфигурация Swagger для разработки.
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
