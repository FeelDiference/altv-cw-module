using AltV.Net;
using AltV.Net.Async;
using System;
using System.Collections.Generic;

namespace MeshHub.Rpf
{
    /// <summary>
    /// Главный класс модуля - точка входа для ALT:V
    /// </summary>
    public class ModuleMain : Resource
    {
        public static Services.RpfService? RpfService { get; private set; }
        public static Services.HandlingService? HandlingService { get; private set; }
        public static Services.MeshService? MeshService { get; private set; }
        public static Services.InteriorService? InteriorService { get; private set; }
        public static Services.JenkIndexService? JenkIndexService { get; private set; }
        public static Services.AutoUpdaterService? AutoUpdater { get; private set; }

        public override void OnStart()
        {
            Alt.Log("[MeshHub.Rpf Resource] 🚀 Starting C# resource...");

            try
            {
                // Инициализируем сервисы
                RpfService = new Services.RpfService();
                HandlingService = new Services.HandlingService(RpfService);
                MeshService = new Services.MeshService(RpfService);
                
                // Инициализируем Jenkins Index словарь
                var cwd = System.IO.Directory.GetCurrentDirectory();
                var resourcePath = System.IO.Path.Combine(cwd, "resources", "meshhub-rpf");
                JenkIndexService = new Services.JenkIndexService(resourcePath);
                JenkIndexService.LoadBaseDictionary();
                
                InteriorService = new Services.InteriorService(RpfService);
                
                // Получаем версию от JS ресурса meshhub
                string currentVersion = GetMeshhubVersion();
                Alt.Log($"[MeshHub.Rpf Resource] 📋 Meshhub version from JS: {currentVersion}");
                
                AutoUpdater = new Services.AutoUpdaterService(currentVersion);

                Alt.Log("[MeshHub.Rpf Resource] ✅ Services initialized");

                // Регистрируем экспорты для межресурсного взаимодействия
                RegisterExports();
                
                // Инициализируем систему автообновления
                AutoUpdater.Initialize();

                Alt.Log("[MeshHub.Rpf Resource] ✅ C# resource started successfully!");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshHub.Rpf Resource] ❌ Failed to start resource: {ex.Message}");
                Alt.LogError($"[MeshHub.Rpf Resource] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Получает версию ресурса meshhub из constants.js
        /// </summary>
        private string GetMeshhubVersion()
        {
            try
            {
                // Читаем constants.js из ресурса meshhub
                var cwd = System.IO.Directory.GetCurrentDirectory();
                var constantsPath = System.IO.Path.Combine(cwd, "resources", "meshhub", "server", "config", "constants.js");
                
                if (System.IO.File.Exists(constantsPath))
                {
                    var content = System.IO.File.ReadAllText(constantsPath);
                    
                    // Ищем строку с версией: version: '0.1',
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(content, @"version:\s*['""]([^'""]+)['""]");
                    if (versionMatch.Success)
                    {
                        var version = versionMatch.Groups[1].Value;
                        Alt.Log($"[MeshHub.Rpf Resource] ✅ Read version from constants.js: {version}");
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                Alt.LogWarning($"[MeshHub.Rpf Resource] ⚠️ Failed to read version from constants.js: {ex.Message}");
            }
            
            // Fallback - используем версию по умолчанию
            Alt.LogWarning("[MeshHub.Rpf Resource] ⚠️ Using fallback version 0.1");
            return "0.1";
        }

        public override void OnStop()
        {
            Alt.Log("[MeshHub.Rpf Resource] 🛑 Stopping resource...");
            
            // Освобождаем ресурсы автоапдейтера
            AutoUpdater?.Dispose();
        }

        /// <summary>
        /// Регистрация экспортов, доступных из других ресурсов (JS или C#)
        /// </summary>
        private void RegisterExports()
        {
            try
            {
                // Регистрируем экспорты через Alt.Export
                // Эти функции будут доступны из JS через alt.getResourceExports('meshhub-rpf')
                Alt.Log("[MeshHub.Rpf Resource] 📝 Registering exports...");

                // Функция открытия RPF архива
                Alt.Export("openRpfArchive", new Func<string, string?>((rpfPath) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Opening RPF: {rpfPath}");
                        return RpfService?.OpenRpfArchive(rpfPath);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error opening RPF: {ex.Message}");
                        return null;
                    }
                }));

                // Функция извлечения файла
                Alt.Export("extractFile", new Func<string, string, byte[]?>((archiveId, filePath) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Extracting file: {filePath} from {archiveId}");
                        return RpfService?.ExtractFile(archiveId, filePath);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error extracting file: {ex.Message}");
                        return null;
                    }
                }));

                // Функция замены файла
                Alt.Export("replaceFile", new Action<string, string, byte[]>((archiveId, filePath, content) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Replacing file: {filePath} in {archiveId}");
                        RpfService?.ReplaceFile(archiveId, filePath, content);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error replacing file: {ex.Message}");
                    }
                }));

                // Функция закрытия архива
                Alt.Export("closeRpfArchive", new Action<string>((archiveId) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Closing archive: {archiveId}");
                        RpfService?.CloseRpfArchive(archiveId);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error closing archive: {ex.Message}");
                    }
                }));

                // Функция поиска handling.meta в архиве
                Alt.Export("findHandlingMeta", new Func<string, string?>((archiveId) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Searching for handling.meta in archive {archiveId}");
                        return RpfService?.FindFileInArchive(archiveId, "handling.meta");
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error finding handling.meta: {ex.Message}");
                        return null;
                    }
                }));

                // Функция получения handling.meta XML
                Alt.Export("getHandlingXml", new Func<string, string, string?>((archiveId, filePath) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Getting handling XML from {archiveId}:{filePath}");
                        return HandlingService?.GetHandlingXml(archiveId, filePath);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error getting handling XML: {ex.Message}");
                        return null;
                    }
                }));

                // Функция сохранения handling.meta XML
                Alt.Export("saveHandlingXml", new Action<string, string, string>((archiveId, filePath, xml) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Saving handling XML to {archiveId}:{filePath}");
                        HandlingService?.SaveHandlingXml(archiveId, filePath, xml);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error saving handling XML: {ex.Message}");
                    }
                }));

                // Функция извлечения mesh данных из .yft файла
                Alt.Export("extractVehicleMeshData", new Func<string, Dictionary<string, object>?>((vehicleName) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Extracting mesh data for vehicle: {vehicleName}");
                        return MeshService?.ExtractVehicleMeshData(vehicleName);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error extracting mesh data: {ex.Message}");
                        return null;
                    }
                }));

                // Interior YTYP поиск
                Alt.Export("findMloYtypXml", new Func<string, string, string?>((archiveId, interiorName) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Finding MLO YTYP: {interiorName}");
                        return InteriorService?.FindMloYtypXml(archiveId, interiorName);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error finding MLO YTYP: {ex.Message}");
                        return null;
                    }
                }));

                // Interior Entity Sets
                Alt.Export("getMloEntitySets", new Func<string, string, string[]?>((archiveId, interiorName) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Getting MLO entity sets: {interiorName}");
                        return InteriorService?.GetMloEntitySets(archiveId, interiorName);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error getting entity sets: {ex.Message}");
                        return null;
                    }
                }));

                Alt.Log("[MeshHub.Rpf Resource] ✅ Exports registered successfully");
                Alt.Log("[MeshHub.Rpf Resource] Available exports:");
                Alt.Log("  - openRpfArchive(path)");
                Alt.Log("  - findHandlingMeta(archiveId)");
                Alt.Log("  - extractFile(archiveId, filePath)");
                Alt.Log("  - replaceFile(archiveId, filePath, content)");
                Alt.Log("  - closeRpfArchive(archiveId)");
                Alt.Log("  - getHandlingXml(archiveId, filePath)");
                Alt.Log("  - saveHandlingXml(archiveId, filePath, xml)");
                Alt.Log("  - extractVehicleMeshData(vehicleName)");
                Alt.Log("  - findMloYtypXml(archiveId, interiorName)");
                Alt.Log("  - getMloEntitySets(archiveId, interiorName)");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshHub.Rpf Resource] ❌ Error registering exports: {ex.Message}");
            }
        }
    }
}


                        Alt.LogError($"[MeshHub.Rpf] Error finding MLO YTYP: {ex.Message}");
                        return null;
                    }
                }));

                // Interior Entity Sets
                Alt.Export("getMloEntitySets", new Func<string, string, string[]?>((archiveId, interiorName) =>
                {
                    try
                    {
                        Alt.Log($"[MeshHub.Rpf] Getting MLO entity sets: {interiorName}");
                        return InteriorService?.GetMloEntitySets(archiveId, interiorName);
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[MeshHub.Rpf] Error getting entity sets: {ex.Message}");
                        return null;
                    }
                }));

                Alt.Log("[MeshHub.Rpf Resource] ✅ Exports registered successfully");
                Alt.Log("[MeshHub.Rpf Resource] Available exports:");
                Alt.Log("  - openRpfArchive(path)");
                Alt.Log("  - findHandlingMeta(archiveId)");
                Alt.Log("  - extractFile(archiveId, filePath)");
                Alt.Log("  - replaceFile(archiveId, filePath, content)");
                Alt.Log("  - closeRpfArchive(archiveId)");
                Alt.Log("  - getHandlingXml(archiveId, filePath)");
                Alt.Log("  - saveHandlingXml(archiveId, filePath, xml)");
                Alt.Log("  - extractVehicleMeshData(vehicleName)");
                Alt.Log("  - findMloYtypXml(archiveId, interiorName)");
                Alt.Log("  - getMloEntitySets(archiveId, interiorName)");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshHub.Rpf Resource] ❌ Error registering exports: {ex.Message}");
            }
        }
    }
}

