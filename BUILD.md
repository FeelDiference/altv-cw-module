# MeshHub C# Module - Build Instructions

## Требования

- .NET 8.0 SDK или выше
- Visual Studio 2022 или Rider (опционально)
- ALT:V Server

## Сборка проекта

### Вариант 1: Через командную строку

```bash
# Перейти в директорию проекта
cd altv-cw-module

# Собрать проект в Release режиме
dotnet build MeshHub.Rpf.sln -c Release

# Результат:
# MeshHub.Rpf/bin/Release/net8.0/MeshHub.Rpf.dll
# MeshHub.Core/bin/Release/netstandard2.0/MeshHub.Core.dll
```

### Вариант 2: Через Visual Studio

1. Откройте `MeshHub.Rpf.sln` в Visual Studio
2. Выберите конфигурацию **Release**
3. Нажмите **Build → Build Solution** (Ctrl+Shift+B)

### Вариант 3: Через Rider

1. Откройте `MeshHub.Rpf.sln` в Rider
2. Выберите конфигурацию **Release**
3. Нажмите **Build → Build Solution**

## Установка в ALT:V сервер

### Автоматическое копирование (PowerShell)

```powershell
# Из директории altv-cw-module
dotnet build MeshHub.Rpf.sln -c Release

# Копируем DLL в ALT:V modules
Copy-Item -Path "MeshHub.Rpf\bin\Release\net8.0\MeshHub.Rpf.dll" -Destination "..\altv-server\modules\" -Force
Copy-Item -Path "MeshHub.Core\bin\Release\netstandard2.0\MeshHub.Core.dll" -Destination "..\altv-server\modules\" -Force

Write-Host "✅ DLL files copied to ALT:V modules" -ForegroundColor Green
```

### Автоматическое копирование (Bash)

```bash
# Из директории altv-cw-module
dotnet build MeshHub.Rpf.sln -c Release

# Копируем DLL в ALT:V modules
cp MeshHub.Rpf/bin/Release/net8.0/MeshHub.Rpf.dll ../altv-server/modules/
cp MeshHub.Core/bin/Release/netstandard2.0/MeshHub.Core.dll ../altv-server/modules/

echo "✅ DLL files copied to ALT:V modules"
```

### Ручное копирование

1. Собрать проект (см. выше)
2. Скопировать файлы:
   - `MeshHub.Rpf/bin/Release/net8.0/MeshHub.Rpf.dll` → `altv-server/modules/`
   - `MeshHub.Core/bin/Release/netstandard2.0/MeshHub.Core.dll` → `altv-server/modules/`

## Конфигурация версии ресурса

**Версия задаётся в JS ресурсе** `altv-server/resources/meshhub/server/config/constants.js`:

```javascript
export const PROJECT_INFO = {
  name: 'MeshHub',
  version: '0.1',  // ← Измените здесь при обновлении!
  description: 'ALT:V Integration for vehicle management'
}
```

**При изменении версии:**
1. Откройте `altv-server/resources/meshhub/server/config/constants.js`
2. Измените `version: '0.1'` на нужную версию (например `'0.2'`)
3. Перезапустите сервер

C# модуль автоматически получит версию от JS ресурса через export.

## Конфигурация AutoUpdater

После сборки нужно настроить `software_` API ключ для автообновления:

### Получение software_ API ключа

1. **Откройте админ-панель:** https://hub.feeld.space/software
2. **Кликните на MeshHub** для открытия детальной страницы
3. **В секции "🔑 API Ключи"** нажмите **"Создать"**
4. **Введите название:** например "AltV Production Server"
5. **Скопируйте созданный ключ** (начинается с `software_`)

### Вставка ключа в код

**Файл:** `MeshHub.Rpf/Services/AutoUpdaterService.cs`

Найдите строку (примерно 26):
```csharp
private const string API_KEY = "software_ЗАМЕНИТЕ_НА_РЕАЛЬНЫЙ_КЛЮЧ_ИЗ_АДМИНКИ";
```

Замените на ваш реальный `software_` ключ из шага выше и пересоберите проект.

**Важно:** Ключ привязан к конкретному софту (MeshHub) и позволяет только скачивать обновления этого софта.

## Проверка работы

1. Запустите ALT:V сервер
2. Проверьте логи в консоли:

```
[MeshHub.Rpf Resource] 🚀 Starting C# resource...
[MeshHub.Rpf Resource] ✅ Services initialized
[AutoUpdate] 🚀 Initializing auto-updater...
[AutoUpdate] Current version: 0.1
[AutoUpdate] Backend URL: https://hub.feeld.space
[AutoUpdate] Software name: MeshHub
[AutoUpdate] ✅ Auto-updater initialized
[AutoUpdate] Update checks every 60 minutes
```

Через 10 секунд:
```
[AutoUpdate] 🔍 Checking for updates...
[AutoUpdate] Current version: 0.1
[AutoUpdate] Found 'MeshHub' on backend
[AutoUpdate] Latest version: 0.2
[AutoUpdate] 🎉 New version available: 0.2
```

## Troubleshooting

### Ошибка "credentials not configured"

**Проблема:** Не заменили захардкоженные значения в AutoUpdaterService.cs

**Решение:**
1. Откройте `MeshHub.Rpf/Services/AutoUpdaterService.cs`
2. Замените `SOFTWARE_ID` и `API_KEY` на реальные значения
3. Пересоберите проект: `dotnet build MeshHub.Rpf.sln -c Release`
4. Скопируйте DLL в altv-server/modules
5. Перезапустите сервер

### Ошибка "MeshHub.Core.dll not found"

**Проблема:** Не скопирована зависимость MeshHub.Core.dll

**Решение:**
```bash
cp MeshHub.Core/bin/Release/netstandard2.0/MeshHub.Core.dll ../altv-server/modules/
```

### Ошибка "Failed to get software list"

**Проблема:** Backend недоступен или API_KEY неверный

**Решение:**
1. Проверить доступность `https://hub.feeld.space`
2. Проверить валидность API_KEY через админ-панель
3. Проверить что SOFTWARE_NAME="MeshHub" существует в `/api/software`

## Версионность

Текущая версия задана в `ModuleMain.cs`:

```csharp
private const string CURRENT_VERSION = "0.1";
```

При обновлении версии MeshHub:
1. Измените `CURRENT_VERSION` в `ModuleMain.cs`
2. Пересоберите проект
3. Скопируйте DLL
4. Перезапустите сервер

## Файлы после сборки

```
altv-server/
└── modules/
    ├── MeshHub.Rpf.dll      ← Главный модуль с AutoUpdater
    └── MeshHub.Core.dll     ← Зависимость (CodeWalker Core)
```

## Команды для быстрой сборки и установки

**Windows (PowerShell):**
```powershell
# Полный цикл: сборка + копирование
cd altv-cw-module
dotnet build MeshHub.Rpf.sln -c Release
Copy-Item "MeshHub.Rpf\bin\Release\net8.0\MeshHub.Rpf.dll" "..\altv-server\modules\" -Force
Copy-Item "MeshHub.Core\bin\Release\netstandard2.0\MeshHub.Core.dll" "..\altv-server\modules\" -Force
cd ..\altv-server
./altv-server.exe
```

**Linux/macOS:**
```bash
# Полный цикл: сборка + копирование
cd altv-cw-module
dotnet build MeshHub.Rpf.sln -c Release
cp MeshHub.Rpf/bin/Release/net8.0/MeshHub.Rpf.dll ../altv-server/modules/
cp MeshHub.Core/bin/Release/netstandard2.0/MeshHub.Core.dll ../altv-server/modules/
cd ../altv-server
./altv-server
```

## Зависимости

Проект использует:
- `AltV.Net` - ALT:V API для C#
- `AltV.Net.Async` - Асинхронные операции
- `System.IO.Compression` - Работа с ZIP архивами (встроено в .NET)
- `System.Text.Json` - JSON сериализация (встроено в .NET)

Все зависимости устанавливаются автоматически через NuGet при сборке.

