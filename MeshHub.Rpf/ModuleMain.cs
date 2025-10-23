using AltV.Net;
using AltV.Net.Async;
using System;

namespace MeshHub.Rpf
{
    /// <summary>
    /// Главный класс модуля - точка входа для ALT:V
    /// </summary>
    public class ModuleMain : Resource
    {
        public static Services.RpfService? RpfService { get; private set; }
        public static Services.HandlingService? HandlingService { get; private set; }

        public override void OnStart()
        {
            Alt.Log("[MeshHub.Rpf Resource] 🚀 Starting C# resource...");

            try
            {
                // Инициализируем сервисы
                RpfService = new Services.RpfService();
                HandlingService = new Services.HandlingService(RpfService);

                Alt.Log("[MeshHub.Rpf Resource] ✅ Services initialized");

                // Регистрируем экспорты для межресурсного взаимодействия
                RegisterExports();

                Alt.Log("[MeshHub.Rpf Resource] ✅ C# resource started successfully!");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshHub.Rpf Resource] ❌ Failed to start resource: {ex.Message}");
                Alt.LogError($"[MeshHub.Rpf Resource] Stack trace: {ex.StackTrace}");
            }
        }

        public override void OnStop()
        {
            Alt.Log("[MeshHub.Rpf Resource] 🛑 Stopping resource...");
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

                Alt.Log("[MeshHub.Rpf Resource] ✅ Exports registered successfully");
                Alt.Log("[MeshHub.Rpf Resource] Available exports:");
                Alt.Log("  - openRpfArchive(path)");
                Alt.Log("  - findHandlingMeta(archiveId)");
                Alt.Log("  - extractFile(archiveId, filePath)");
                Alt.Log("  - replaceFile(archiveId, filePath, content)");
                Alt.Log("  - closeRpfArchive(archiveId)");
                Alt.Log("  - getHandlingXml(archiveId, filePath)");
                Alt.Log("  - saveHandlingXml(archiveId, filePath, xml)");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshHub.Rpf Resource] ❌ Error registering exports: {ex.Message}");
            }
        }
    }
}

