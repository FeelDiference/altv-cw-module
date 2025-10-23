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
                
                // НЕ сканируем архив сразу - ленивая загрузка!
                // Сканирование произойдет только когда понадобится найти файл
                _openedArchives[archiveId] = rpf;
                _scannedArchives[archiveId] = false; // Помечаем как не отсканированный
                
                Alt.Log($"[RpfService] ✅ Opened RPF: {rpfPath} (ID: {archiveId}) - lazy loading enabled");
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
                var files = rpf.AllEntries
                    .Where(e => e is RpfFileEntry)
                    .Select(e => e.Path)
                    .ToArray();

                Alt.Log($"[RpfService] ✅ Listed {files.Length} files from archive: {archiveId}");
                return files;
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
                var entry = rpf.AllEntries
                    .FirstOrDefault(e => e.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                if (entry is not RpfFileEntry fileEntry)
                {
                    Alt.LogError($"[RpfService] File not found: {filePath}");
                    return null;
                }

                var data = rpf.ExtractFile(fileEntry);
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
                // ЛЕНИВОЕ СКАНИРОВАНИЕ: сканируем только когда нужно найти файл
                if (!_scannedArchives.GetValueOrDefault(archiveId, false))
                {
                    Alt.Log($"[RpfService] 📂 Scanning archive structure (lazy loading)...");
                    rpf.ScanStructure(null, null);
                    _scannedArchives[archiveId] = true;
                    Alt.Log($"[RpfService] ✅ Archive scanned");
                }
                
                Alt.Log($"[RpfService] 🔍 Searching for '{fileName}'...");
                
                // Оптимизированный поиск без загрузки всех вложенных архивов
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
