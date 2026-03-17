var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер.
builder.Services.AddControllers();

var app = builder.Build();

// Конфигурация конвейера HTTP-запросов.
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
