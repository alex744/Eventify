using Ya.Events.Application;
using Ya.Events.Infrastructure;
using Ya.Events.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

// Конфигурация Swagger для разработки.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Конфигурация конвейера HTTP-запросов.
app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseInfrastructure();
app.MapControllers();

app.Run();
