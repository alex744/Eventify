# Eventify
API для управления событиями (CRUD операции). Построено на ASP.NET Core Web API.

## Требования
- [.NET 10 SDK](https://dotnet.microsoft.com/ru-ru/download/dotnet/10.0)

## Запуск проекта
1. Клонируйте репозиторий:   
   git clone https://github.com/alex744/Eventify.git
   cd Eventify\src

2. Восстановите зависимости:
   dotnet restore

3. Собирите проект
   dotnet build

4. Запустите проект:
   dotnet run --project Ya.Events.WebApi

5. Приложение будет доступно по адресам: 
   http://localhost:5000
   https://localhost:5001

6. Документация Swagger: 
   http://localhost:5000/swagger.
   
## Краткая документация API
Все эндпоинты находятся по базовому пути /events

Модель Event
{
  "id": "guid",
  "title": "string",
  "description": "string | null",
  "startAt": "2025-06-01",
  "endAt": "2025-06-01"
}
Валидация: endAt должен быть позже startAt
   
1. Получить все события
   GET /events
   Ответ: 200 OK с массивом событий
   
2. Получить событие по ID
   GET /events/{id}
   Параметры: id (Guid)
   Ответ: 200 OK с объектом события или 404 Not Found
   
3. Создать событие
   POST /events
   Тело запроса:
   {
     "title": "Конференция",
	 "description": "Описание (необязательно)",
	 "startAt": "2025-06-01T10:00:00Z",
	 "endAt": "2025-06-01T18:00:00Z"
   }
   Ответ: 201 Created и Location на созданный ресурс
   
4. Обновить событие
   PUT /events/{id}
   Параметры: id (Guid)
   Тело запроса: полный объект события (без id)
   Ответ: 204 No Content при успехе, 400 Bad Request или 404 Not Found

5. Удалить событие
   DELETE /events/{id}
   Параметры: id (Guid)
   Ответ: 204 No Content или 404 Not Found
   
## Используемые технологии
ASP.NET Core 10
Swagger/OpenAPI (Swashbuckle)
