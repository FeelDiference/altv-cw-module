using AltV.Net;
using MeshHub.Core.GameFiles;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис для работы с RPF архивами
    /// Все методы публичные и доступны через экспорт
    /// </summary>
    public class RpfService
    {
        private readonly Dictionary<string, RpfFile> _openedArchives = new();
        private readonly Dictionary<string, bool> _scannedArchives = new(); // Отслеживаем отсканированные архивы

        /// <summary>
        /// Открывает RPF архив и возвращает его ID
        /// </summary>
        public string? OpenRpfArchive(string rpfPath)
        {
            try
            {
                if (!File.Exists(rpfPath))
                {
                    Alt.LogError($"[RpfService] RPF file not found: {rpfPath}");
                    return null;
                }

                var archiveId = Guid.NewGuid().ToString();
                var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
                
                // Загружаем только заголовок (без рекурсивного сканирования вложенных RPF)
                using (var fs = File.OpenRead(rpfPath))
                using (var br = new BinaryReader(fs))
                {
                    // Вызываем приватный метод ReadHeader через рефлексию
                    var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (readHeaderMethod != null)
                    {
                        readHeaderMethod.Invoke(rpf, new object[] { br });
                        Alt.Log($"[RpfService] ✅ Loaded RPF header: {rpfPath}");
                    }
                    else
                    {
                        Alt.LogError($"[RpfService] Failed to find ReadHeader method!");
                    }
                }
                
                _openedArchives[archiveId] = rpf;
                _scannedArchives[archiveId] = false;
                
                Alt.Log($"[RpfService] ✅ Opened RPF: {rpfPath} (ID: {archiveId})");
                
                // Индексируем имена файлов для JenkIndex
                if (ModuleMain.JenkIndexService != null)
                {
                    ModuleMain.JenkIndexService.IndexRpfArchive(rpf);
                }
                
                return archiveId;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[RpfService] Error opening RPF: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Закрывает открытый RPF архив
        /// </summary>
        public bool CloseRpfArchive(string archiveId)
        {
            if (_openedArchives.Remove(archiveId))
            {
                _scannedArchives.Remove(archiveId); // Убираем из списка отсканированных
                Alt.Log($"[RpfService] ✅ Closed RPF archive: {archiveId}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Получает список файлов в RPF архиве
        /// </summary>
        public string[]? ListRpfFiles(string archiveId)
        {
            if (!_openedArchives.TryGetValue(archiveId, out var rpf))
            {
                Alt.LogError($"[RpfService] Archive not found: {archiveId}");
                return null;
            }

            try
            {
                // НЕ СКАНИРУЕМ архив для списка файлов - слишком тяжело!
                // Этот метод больше не используется для поиска .yft
                Alt.LogWarning($"[RpfService] ListRpfFiles вызван, но не используется для mesh данных");
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Alt.LogError($"[RpfService] Error listing files: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Извлекает файл из RPF архива
        /// </summary>
        public byte[]? ExtractFile(string archiveId, string filePath)
        {
            if (!_openedArchives.TryGetValue(archiveId, out var rpf))
            {
                Alt.LogError($"[RpfService] Archive not found: {archiveId}");
                return null;
            }

            try
            {
                // Нормализуем путь - заменяем backslash на forward slash
                var normalizedPath = filePath.Replace('\\', '/');
                
                // ВАЖНО: Если путь начинается с имени архива (например "dlc.rpf/common/data/handling.meta")
                // нужно убрать этот префикс, так как мы уже внутри этого архива
                if (normalizedPath.StartsWith(rpf.NameLower + "/", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath = normalizedPath.Substring(rpf.NameLower.Length + 1);
                    Alt.Log($"[RpfService] 🔧 Removed archive name prefix, new path: {normalizedPath}");
                }
                
                // Разбиваем путь на части
                var pathParts = normalizedPath.Split('/');
                var currentDir = rpf.Root;
                RpfEntry? currentEntry = null;
                RpfFile currentRpf = rpf;

                // Проходим по каждой части пути
                for (int i = 0; i < pathParts.Length; i++)
                {
                    var part = pathParts[i];
                    if (string.IsNullOrEmpty(part)) continue;

                    // Логи отключены для производительности
                    // Alt.Log($"[RpfService] 🔍 Checking part: {part}");

                    // Проверяем что currentDir существует
                    if (currentDir == null)
                    {
                        Alt.LogError($"[RpfService] Current directory is null!");
                        return null;
                    }

                    // Если это последняя часть - ищем файл
                    if (i == pathParts.Length - 1)
                    {
                        if (currentDir.Files == null)
                        {
                            Alt.LogError($"[RpfService] Files collection is null!");
                            return null;
                        }

                        currentEntry = currentDir.Files
                            .FirstOrDefault(f => f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                        if (currentEntry == null)
                        {
                            Alt.LogError($"[RpfService] File not found: {part}");
                            return null;
                        }
                        break;
                    }

                    // Ищем следующую директорию или RPF файл
                    RpfEntry? nextEntry = null;
                    
                    if (currentDir.Files != null)
                    {
                        nextEntry = currentDir.Files
                            .FirstOrDefault(f => f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                    }
                        
                    if (nextEntry == null && currentDir.Directories != null)
                    {
                        nextEntry = currentDir.Directories
                            .FirstOrDefault(d => d.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                    }

                    if (nextEntry == null)
                    {
                        Alt.LogError($"[RpfService] Entry not found: {part}");
                        return null;
                    }

                    // Если это RPF файл - открываем его
                    if (nextEntry is RpfFileEntry rpfEntry && part.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                    {
                        Alt.Log($"[RpfService] 📦 Opening nested RPF: {part}");
                        
                        // Извлекаем вложенный RPF
                        var nestedRpfData = currentRpf.ExtractFile(rpfEntry);
                        if (nestedRpfData == null)
                        {
                            Alt.LogError($"[RpfService] Failed to extract nested RPF: {part}");
                            return null;
                        }

                        // Создаем временный файл для вложенного RPF
                        var tempPath = Path.GetTempFileName();
                        File.WriteAllBytes(tempPath, nestedRpfData);

                        // Открываем вложенный RPF
                        currentRpf = new RpfFile(tempPath, part);
                        
                        // Загружаем заголовок вложенного RPF
                        using (var nestedFs = new MemoryStream(nestedRpfData))
                        using (var nestedBr = new BinaryReader(nestedFs))
                        {
                            var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (readHeaderMethod != null)
                            {
                                readHeaderMethod.Invoke(currentRpf, new object[] { nestedBr });
                                Alt.Log($"[RpfService] ✅ Loaded nested RPF header: {part}");
                            }
                        }
                        
                        currentDir = currentRpf.Root;

                        Alt.Log($"[RpfService] ✅ Opened nested RPF: {part}");
                    }
                    else if (nextEntry is RpfDirectoryEntry dirEntry)
                    {
                        currentDir = dirEntry;
                    }
                    else
                    {
                        Alt.LogError($"[RpfService] Invalid entry type for: {part}");
                        return null;
                    }
                }

                // Проверяем что нашли файл
                if (currentEntry is not RpfFileEntry fileEntry)
                {
                    Alt.LogError($"[RpfService] File not found or invalid type: {filePath}");
                    return null;
                }

                var data = currentRpf.ExtractFile(fileEntry);
                Alt.Log($"[RpfService] ✅ Extracted file: {filePath} ({data?.Length ?? 0} bytes)");
                return data;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[RpfService] Error extracting file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Заменяет файл в RPF архиве
        /// </summary>
        public bool ReplaceFile(string archiveId, string filePath, byte[] newContent)
        {
            if (!_openedArchives.TryGetValue(archiveId, out var rpf))
            {
                Alt.LogError($"[RpfService] Archive not found: {archiveId}");
                return false;
            }

            try
            {
                var entry = rpf.AllEntries
                    .FirstOrDefault(e => e.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                if (entry is not RpfFileEntry fileEntry)
                {
                    Alt.LogError($"[RpfService] File not found: {filePath}");
                    return false;
                }

                // Получаем родительскую директорию
                if (fileEntry.Parent is not RpfDirectoryEntry parentDir)
                {
                    Alt.LogError($"[RpfService] Parent directory not found for: {filePath}");
                    return false;
                }

                // Заменяем файл
                // CreateFile уже вызывает InsertFileSpace → EnsureAllEntries → WriteHeader
                // Это обновляет заголовок RPF и записывает данные на диск
                RpfFile.CreateFile(parentDir, fileEntry.Name, newContent, overwrite: true);
                
                Alt.Log($"[RpfService] ✅ Replaced file: {filePath} ({newContent.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[RpfService] Error replacing file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Находит файл в архиве по имени (поиск по всему дереву)
        /// </summary>
        public string? FindFileInArchive(string archiveId, string fileName)
        {
            if (!_openedArchives.TryGetValue(archiveId, out var rpf))
            {
                Alt.LogError($"[RpfService] Archive not found: {archiveId}");
                return null;
            }

            try
            {
                // НЕ СКАНИРУЕМ! Пытаемся найти файл напрямую
                Alt.Log($"[RpfService] 🔍 Searching for '{fileName}' (без сканирования)...");
                
                // Пытаемся найти файл без сканирования структуры
                var found = FindFileRecursive(rpf.Root, fileName);
                
                if (found != null)
                {
                    Alt.Log($"[RpfService] ✅ Found: {found.Path}");
                    return found.Path;
                }

                Alt.LogWarning($"[RpfService] ⚠️ '{fileName}' not found");
                return null;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[RpfService] Error finding file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Рекурсивный поиск файла в дереве RPF (оптимизированный)
        /// </summary>
        private RpfEntry? FindFileRecursive(RpfDirectoryEntry directory, string fileName)
        {
            // Проверяем файлы в текущей директории
            foreach (var entry in directory.Files)
            {
                if (entry.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            // Рекурсивно проверяем поддиректории
            foreach (var subDir in directory.Directories)
            {
                var found = FindFileRecursive(subDir, fileName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Получить RPF архив по ID (внутренний метод)
        /// </summary>
        internal RpfFile? GetRpfArchive(string archiveId)
        {
            return _openedArchives.TryGetValue(archiveId, out var rpf) ? rpf : null;
        }
    }
}

        internal RpfFile? GetRpfArchive(string archiveId)
        {
            return _openedArchives.TryGetValue(archiveId, out var rpf) ? rpf : null;
        }
    }
}
