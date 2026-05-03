# Cards

Веб-приложение для обучающих карточек (flashcards), построенное на Blazor Web App
с серверным рендерингом и хранением данных в JSON-файлах.

## Возможности (MVP)

- CRUD колод (название, описание).
- CRUD карточек внутри колоды (вопрос/ответ).
- Режим изучения: перемешивание карточек, flip-анимация (вопрос → ответ),
  кнопки «Знаю / Не знаю», итог сессии.

## Стек

- .NET 8 (ASP.NET Core, Blazor Web App, Interactive Server render mode).
- Хранилище: JSON-файлы (один файл на колоду) в каталоге `src/Cards.Web/Data/`.
- UI: Bootstrap (поставляется с шаблоном Blazor Web App).

## Структура проекта

```
Cards/
  Cards.slnx
  src/
    Cards.Web/
      Models/             -- Card, Deck
      Services/           -- IDeckService, JsonDeckService
      Components/
        Layout/           -- MainLayout, NavMenu
        Pages/
          Home.razor
          Decks/          -- DeckList, DeckCreate, DeckEdit
          Study/          -- StudySession
        Shared/           -- CardForm, CardDisplay
      Data/               -- JSON-файлы колод (создаётся автоматически)
      wwwroot/
```

## Требования

- .NET SDK 8.0 или новее.

## Запуск

```powershell
dotnet run --project src/Cards.Web/Cards.Web.csproj
```

После запуска приложение будет доступно по адресу, указанному в выводе
(например, `https://localhost:5001`).

## Сборка

```powershell
dotnet build
```

## Хранение данных

Каждая колода сохраняется в отдельный JSON-файл `src/Cards.Web/Data/{deck-id}.json`.
Каталог `Data/` создаётся автоматически при первом запуске. JSON-файлы исключены из
git (см. `.gitignore`), сам каталог сохраняется через `.gitkeep`.

Запись на диск сериализуется через `SemaphoreSlim` и выполняется атомарно
(через временный файл с последующим `File.Replace`), чтобы избежать повреждения
данных при одновременных операциях.
