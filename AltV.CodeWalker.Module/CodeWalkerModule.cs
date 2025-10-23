using AltV.Net;
using AltV.Net.Async;
using System;

namespace MeshHub.Rpf
{
    /// <summary>
    /// MeshHub RPF Resource
    /// Предоставляет публичные методы для работы с RPF архивами через CodeWalker.Core
    /// </summary>
    public class MeshHubRpfResource : Resource
    {
        public static Services.RpfService RpfService { get; private set; } = null!;
        public static Services.HandlingService HandlingService { get; private set; } = null!;

        public override void OnStart()
        {
            Alt.Log("[MeshHub RPF] Starting...");
            
            // Регистрируем сервисы
            RpfService = new Services.RpfService();
            HandlingService = new Services.HandlingService(RpfService);
            
            Alt.Log("[MeshHub RPF] ✅ Resource started successfully!");
            Alt.Log("[MeshHub RPF] Available methods:");
            Alt.Log("  - RpfService.OpenRpfArchive(path)");
            Alt.Log("  - RpfService.ExtractFile(archiveId, filePath)");
            Alt.Log("  - RpfService.ReplaceFile(archiveId, filePath, content)");
            Alt.Log("  - HandlingService.GetHandlingXml(archiveId, filePath)");
            Alt.Log("  - HandlingService.SaveHandlingXml(archiveId, filePath, xml)");
        }

        public override void OnStop()
        {
            Alt.Log("[MeshHub RPF] Stopping...");
        }
    }
}

