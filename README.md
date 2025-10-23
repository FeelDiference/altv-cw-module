# MeshHub RPF Native Module для alt:V

## Описание

Это **нативный C# модуль** для alt:V Server, который позволяет работать с RPF архивами и handling.meta файлами напрямую из JavaScript ресурсов.

## Технические детали

### Что такое нативный модуль?

В alt:V есть два типа расширений:

1. **Native Modules** (C++ или C# DLL) - загружаются глобально в `modules/` папку
   - Требуют экспорт функции `GetSDKHash` для проверки совместимости
   - Доступны для всех ресурсов на сервере
   - Регистрируют глобальные экспорты через `Alt.Export()`

2. **Resources** (C# или JS) - загружаются из `resources/` папки
   - Не требуют `GetSDKHash`
   - Изолированы от других ресурсов

### Почему C# модуль вместо C# ресурса?

Мы создали **C# Native Module** по следующим причинам:

1. ✅ **Глобальная доступность** - экспорты доступны для всех ресурсов
2. ✅ **Производительность** - загружается один раз при старте сервера
3. ✅ **Изоляция** - работа с RPF файлами отделена от игровой логики
4. ✅ **Переиспользование** - один модуль для всех ресурсов

## Структура проекта

```
MeshHub.Rpf/              # Главный модуль (Native Module)
├── ModuleMain.cs         # Точка входа + экспорт GetSDKHash
├── Services/
│   ├── RpfService.cs     # Работа с RPF архивами
│   └── HandlingService.cs # Работа с handling.meta
└── MeshHub.Rpf.csproj

MeshHub.Core/             # CodeWalker библиотека
└── GameFiles/            # Парсинг GTA V файлов
```

## Как работает GetSDKHash

```csharp
public static class ModuleExport
{
    [UnmanagedCallersOnly(EntryPoint = "GetSDKHash")]
    public static uint GetSDKHash()
    {
        // Возвращает SDK версию для проверки совместимости
        return 193; // Для alt:V Server 16.4.2
    }
}
```

### Что делает GetSDKHash?

- alt:V Server вызывает эту функцию при загрузке модуля
- Проверяет совместимость модуля с версией сервера
- Если hash не совпадает - модуль не загружается

## Сборка модуля

### Требования

- .NET 8.0 SDK
- AltV.Net 16.4.28-rc.2

### Команды

```bash
# Очистка
dotnet clean MeshHub.Rpf/MeshHub.Rpf.csproj

# Сборка Release
dotnet build MeshHub.Rpf/MeshHub.Rpf.csproj -c Release

# Результат:
# MeshHub.Rpf/bin/Release/net8.0/meshhub-rpf-module.dll
```

## Установка

1. Собрать проект в Release режиме
2. Скопировать файлы в `altv-server/modules/`:
   - `meshhub-rpf-module.dll` (главный модуль)
   - `MeshHub.Core.dll` (зависимость)
   - `ShadersGen9Conversion.xml` (ресурс)
   - `strings.txt` (ресурс)

3. Добавить модуль в `server.toml`:
```toml
modules = ['js-bytecode-module', 'js-module', 'csharp-module', 'meshhub-rpf-module']
```

## Использование в JS ресурсах

Модуль регистрирует следующие экспорты:

```javascript
import * as alt from 'alt-server';

// Открыть RPF архив
const archiveId = alt.callExport('MeshHub.Rpf.OpenArchive', rpfPath);

// Извлечь файл
const fileData = alt.callExport('MeshHub.Rpf.ExtractFile', archiveId, filePath);

// Заменить файл
alt.callExport('MeshHub.Rpf.ReplaceFile', archiveId, filePath, newContent);

// Получить handling.meta XML
const xml = alt.callExport('MeshHub.Rpf.GetHandlingXml', archiveId, handlingPath);

// Сохранить handling.meta XML
alt.callExport('MeshHub.Rpf.SaveHandlingXml', archiveId, handlingPath, newXml);

// Закрыть архив
alt.callExport('MeshHub.Rpf.CloseArchive', archiveId);
```

## Настройки проекта (важно!)

В `MeshHub.Rpf.csproj` обязательны следующие параметры:

```xml
<PropertyGroup>
  <!-- Имя выходной DLL -->
  <AssemblyName>meshhub-rpf-module</AssemblyName>
  
  <!-- Разрешить UnmanagedCallersOnly атрибут -->
  <EnableUnmanagedCallersOnlyAttribute>true</EnableUnmanagedCallersOnlyAttribute>
  
  <!-- Разрешить небезопасный код -->
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  
  <!-- Создать shared библиотеку -->
  <NativeLib>Shared</NativeLib>
  
  <!-- Не включать .NET Runtime -->
  <SelfContained>false</SelfContained>
</PropertyGroup>
```

## Отладка

Логи модуля можно посмотреть в `server.log`:

```
[17:56:02] Starting alt:V Server on [::]:7788
[17:56:02] Loading module: meshhub-rpf-module
[MeshHub.Rpf Module] 🚀 Starting native module...
[MeshHub.Rpf Module] ✅ Services initialized
[MeshHub.Rpf Module] ✅ Module started successfully!
```

## Возможные ошибки

### "Could not find GetSDKHash function"

**Причина:** Функция GetSDKHash не экспортирована или неправильно скомпилирована

**Решение:**
1. Проверить атрибут `[UnmanagedCallersOnly(EntryPoint = "GetSDKHash")]`
2. Убедиться, что `EnableUnmanagedCallersOnlyAttribute = true` в .csproj
3. Пересобрать в Release режиме

### "SDK version mismatch"

**Причина:** Неправильный SDK Hash

**Решение:**
1. Проверить версию alt:V Server
2. Обновить return значение в `GetSDKHash()`
3. Для версии 16.4.x используется hash = 193

### "Module failed to load"

**Причина:** Отсутствуют зависимости

**Решение:**
1. Проверить, что `MeshHub.Core.dll` в папке `modules/`
2. Проверить, что все NuGet пакеты установлены
3. Пересобрать проект с `CopyLocalLockFileAssemblies = true`

## Версии

- alt:V Server: 16.4.2
- AltV.Net: 16.4.28-rc.2
- .NET: 8.0
- SDK Hash: 193

## Автор

MeshHub Development Team

