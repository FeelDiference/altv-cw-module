using AltV.Net;
using MeshHub.Core.GameFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис для работы с Jenkins Hash индексом
    /// Загружает словарь имен и собирает имена из RPF архивов
    /// </summary>
    public class JenkIndexService
    {
        private readonly HashSet<uint> _loadedHashes = new HashSet<uint>();
        private readonly string _dictionaryPath;
        private readonly string _cachePath;

        public JenkIndexService(string resourcePath)
        {
            _dictionaryPath = Path.Combine(resourcePath, "gta_files.txt");
            _cachePath = Path.Combine(resourcePath, "jenkins_cache.txt");
        }

        /// <summary>
        /// Загружает базовый словарь GTA файлов
        /// </summary>
        public void LoadBaseDictionary()
        {
            try
            {
                if (!File.Exists(_dictionaryPath))
                {
                    Alt.LogWarning($"[JenkIndex] Dictionary not found: {_dictionaryPath}");
                    return;
                }

                Alt.Log($"[JenkIndex] Loading dictionary from {_dictionaryPath}");
                
                var lines = File.ReadAllLines(_dictionaryPath);
                int loaded = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Формат: "name hash_XXXXXXXX"
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        JenkIndex.Ensure(name);
                        loaded++;
                    }
                }

                Alt.Log($"[JenkIndex] ✅ Loaded {loaded} entries from base dictionary");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[JenkIndex] Error loading dictionary: {ex.Message}");
            }
        }

        /// <summary>
        /// Сканирует RPF архив и добавляет все имена файлов в индекс (рекурсивно)
        /// </summary>
        public void IndexRpfArchive(RpfFile rpf)
        {
            try
            {
                var names = new HashSet<string>();
                CollectFileNamesRecursive(rpf, rpf.Root, names);

                int added = 0;
                foreach (var name in names)
                {
                    // Убираем расширение
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
                    if (!string.IsNullOrEmpty(nameWithoutExt))
                    {
                        JenkIndex.Ensure(nameWithoutExt);
                        added++;
                    }
                    
                    // Также добавляем с расширением
                    JenkIndex.Ensure(name);
                }

                Alt.Log($"[JenkIndex] Added {added} names from RPF");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[JenkIndex] Error indexing RPF: {ex.Message}");
            }
        }

        /// <summary>
        /// Рекурсивно собирает имена всех файлов из RPF (включая вложенные)
        /// </summary>
        private void CollectFileNamesRecursive(RpfFile rpf, RpfDirectoryEntry directory, HashSet<string> names)
        {
            // Собираем имена файлов
            foreach (var file in directory.Files)
            {
                names.Add(file.Name);
                
                // Если это вложенный RPF - заходим внутрь
                if (file.NameLower.EndsWith(".rpf"))
                {
                    try
                    {
                        var nestedData = rpf.ExtractFile(file);
                        if (nestedData != null)
                        {
                            var tempPath = Path.GetTempFileName();
                            File.WriteAllBytes(tempPath, nestedData);
                            
                            try
                            {
                                var nestedRpf = new RpfFile(tempPath, file.Name);
                                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                                using (var br = new BinaryReader(fs))
                                {
                                    var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    readHeaderMethod?.Invoke(nestedRpf, new object[] { br });
                                }
                                
                                // Рекурсивно собираем из вложенного RPF
                                CollectFileNamesRecursive(nestedRpf, nestedRpf.Root, names);
                            }
                            finally
                            {
                                if (File.Exists(tempPath))
                                    File.Delete(tempPath);
                            }
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки вложенных RPF
                    }
                }
            }

            // Рекурсия по директориям
            foreach (var subDir in directory.Directories)
            {
                CollectFileNamesRecursive(rpf, subDir, names);
            }
        }

        /// <summary>
        /// Сохраняет расширенный индекс в кэш
        /// </summary>
        public void SaveCache()
        {
            // TODO: Реализовать сохранение кэша если нужно
        }
    }
}

