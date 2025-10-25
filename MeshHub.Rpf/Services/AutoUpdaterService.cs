using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AltV.Net;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис автоматического обновления MeshHub ресурса
    /// Проверяет версии на backend и автоматически обновляет ресурс
    /// </summary>
    public class AutoUpdaterService
    {
        private const int UPDATE_CHECK_INTERVAL_MS = 3600000; // 1 час
        private const string RESOURCE_NAME = "meshhub";
        private const string SOFTWARE_NAME = "MeshHub"; // Имя софта на backend для поиска
        
        // Software API Key (начинается с software_)
        // Автоматически создается при загрузке софта через админ-панель
        private const string SOFTWARE_ID = "d29cd9ab-41a9-4b15-8d68-db8b8e1862c8";
        private const string API_KEY = "software_77e2ae52bda09744c445904256e63009f5f0251397f9c1a00f59657bb94711fd";
        private const string BACKEND_URL = "https://hub.feeld.space";
        
        private readonly HttpClient _httpClient;
        private readonly string _resourcePath;
        private readonly string _backupPath;
        private readonly string _newVersionPath; // Путь для новой версии (meshhub_new)
        
        private Timer? _updateCheckTimer;
        private bool _isChecking = false;
        private string _currentVersion;

        public AutoUpdaterService(string currentVersion)
        {
            _currentVersion = currentVersion;
            
            // Пути к ресурсам (относительно рабочей директории altv-server)
            var cwd = Directory.GetCurrentDirectory();
            _resourcePath = Path.Combine(cwd, "resources", RESOURCE_NAME);
            _backupPath = Path.Combine(cwd, "resources", $"{RESOURCE_NAME}.old");
            _newVersionPath = Path.Combine(cwd, "resources", $"{RESOURCE_NAME}_new"); // Для отложенного обновления
            
            // Настройка HTTP клиента
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Игнорируем SSL для dev
            };
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", API_KEY);
        }

        /// <summary>
        /// Инициализация автообновления
        /// Запускает периодическую проверку обновлений
        /// </summary>
        public void Initialize()
        {
            Alt.Log("[AutoUpdate] 🚀 Initializing auto-updater...");
            
            // ВАЖНО: Сначала проверяем наличие отложенного обновления
            // Это должно произойти ДО загрузки других ресурсов
            CheckAndApplyPendingUpdate();
            
            // Проверяем конфигурацию
            if (!IsConfigured())
            {
                Alt.LogWarning("[AutoUpdate] ⚠️ Auto-update disabled: credentials not configured");
                Alt.LogWarning("[AutoUpdate] 💡 Update SOFTWARE_ID and API_KEY in AutoUpdaterService.cs");
                return;
            }
            
            Alt.Log($"[AutoUpdate] Current version: {_currentVersion}");
            Alt.Log($"[AutoUpdate] Backend URL: {BACKEND_URL}");
            Alt.Log($"[AutoUpdate] Software name: {SOFTWARE_NAME}");
            
            // Первая проверка СРАЗУ при старте
            Alt.Log("[AutoUpdate] 🔍 Performing initial update check...");
            Task.Run(async () => await CheckForUpdatesAsync());
            
            // Периодическая проверка каждый час
            _updateCheckTimer = new Timer(
                async _ => await CheckForUpdatesAsync(),
                null,
                UPDATE_CHECK_INTERVAL_MS,
                UPDATE_CHECK_INTERVAL_MS
            );
            
            Alt.Log($"[AutoUpdate] ✅ Auto-updater initialized");
            Alt.Log($"[AutoUpdate] Update checks every {UPDATE_CHECK_INTERVAL_MS / 60000} minutes");
        }

        /// <summary>
        /// Проверяет и применяет отложенное обновление (если есть meshhub_new)
        /// Вызывается при загрузке модуля ДО загрузки ресурса meshhub
        /// </summary>
        private void CheckAndApplyPendingUpdate()
        {
            try
            {
                // Проверяем наличие папки meshhub_new
                if (!Directory.Exists(_newVersionPath))
                {
                    // Нет отложенного обновления
                    return;
                }
                
                Alt.Log("========================================");
                Alt.Log("🔄 PENDING UPDATE DETECTED!");
                Alt.Log("========================================");
                Alt.Log($"[AutoUpdate] Found pending update in: {_newVersionPath}");
                Alt.Log($"[AutoUpdate] NOTE: This check happens BEFORE {RESOURCE_NAME} is loaded");
                
                // Даём серверу время завершить сканирование папок
                Alt.Log("[AutoUpdate] Waiting for server to release file handles...");
                System.Threading.Thread.Sleep(2000);
                
                // НОВЫЙ ПОДХОД: Запускаем PowerShell скрипт для копирования
                // Это обходит проблемы с блокировкой .git и других служебных файлов
                
                Alt.Log($"[AutoUpdate] Starting PowerShell update script...");
                Alt.Log($"[AutoUpdate] Source: {_newVersionPath}");
                Alt.Log($"[AutoUpdate] Target: {_resourcePath}");
                
                bool scriptSuccess = RunUpdateScript();
                
                if (!scriptSuccess)
                {
                    Alt.LogError("[AutoUpdate] ❌ PowerShell script failed, trying direct copy as fallback...");
                    
                    // Fallback - пробуем прямое копирование
                    try
                    {
                        CopyDirectoryContents(_newVersionPath, _resourcePath, overwrite: true);
                        Alt.Log($"[AutoUpdate] ✅ Files replaced successfully (fallback method)!");
                    }
                    catch (Exception ex)
                    {
                        Alt.LogError($"[AutoUpdate] ❌ Fallback copy also failed: {ex.Message}");
                        throw;
                    }
                }
                
                // Удаляем meshhub_new после успешного копирования
                Alt.Log($"[AutoUpdate] Cleaning up {RESOURCE_NAME}_new...");
                try
                {
                    Directory.Delete(_newVersionPath, true);
                    Alt.Log($"[AutoUpdate] ✅ Cleanup completed");
                }
                catch (Exception ex)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Could not delete {RESOURCE_NAME}_new: {ex.Message}");
                    Alt.LogWarning($"[AutoUpdate] You can manually delete it later");
                }
                
                Alt.Log("========================================");
                Alt.LogWarning("⚠️  UPDATE APPLIED SUCCESSFULLY!");
                Alt.LogWarning("⚠️  PLEASE RESTART THE SERVER NOW!");
                Alt.LogWarning("⚠️  Shut down the server and start it again.");
                Alt.Log("========================================");
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Failed to apply pending update: {ex.Message}");
                Alt.LogError($"[AutoUpdate] Stack trace: {ex.StackTrace}");
                Alt.LogError("[AutoUpdate] You may need to manually rename folders:");
                Alt.LogError($"[AutoUpdate] - Delete: {_resourcePath}");
                Alt.LogError($"[AutoUpdate] - Rename: {_newVersionPath} → {_resourcePath}");
            }
        }

        /// <summary>
        /// Проверяет наличие обновлений на backend
        /// Запрашивает конкретный софт по SOFTWARE_ID
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (_isChecking)
            {
                Alt.LogWarning("[AutoUpdate] Update check already in progress");
                return null;
            }
            
            try
            {
                _isChecking = true;
                
                Alt.Log("[AutoUpdate] 🔍 Checking for updates...");
                Alt.Log($"[AutoUpdate] Current version: {_currentVersion}");
                Alt.Log($"[AutoUpdate] Software ID: {MaskSecret(SOFTWARE_ID)}");
                
                // Запрашиваем конкретный софт по ID (более надежно чем поиск по имени)
                var url = $"{BACKEND_URL}/api/software/{SOFTWARE_ID}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Failed to get software: {response.StatusCode}");
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Alt.LogError("[AutoUpdate] ❌ API key is invalid or revoked");
                    }
                    return null;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var software = JsonSerializer.Deserialize<SoftwareInfo>(content);
                
                if (software == null)
                {
                    Alt.LogWarning("[AutoUpdate] ⚠️ Failed to parse software response");
                    return null;
                }
                
                var latestVersion = software.Version ?? "0.0.0";
                Alt.Log($"[AutoUpdate] ✅ Found software: {software.Name}");
                Alt.Log($"[AutoUpdate] Latest version: {latestVersion}");
                Alt.Log($"[AutoUpdate] Server type: {software.ServerType}");
                
                // Сравниваем версии
                var comparison = CompareVersions(latestVersion, _currentVersion);
                
                if (comparison > 0)
                {
                    Alt.Log($"[AutoUpdate] 🎉 New version available: {latestVersion}");
                    
                    var updateInfo = new UpdateInfo
                    {
                        Id = software.Id ?? "",
                        Version = latestVersion,
                        DownloadUrl = software.DownloadUrl ?? "",
                        FileSize = software.FileSize,
                        Name = software.Name ?? ""
                    };
                    
                    // Автоматически скачиваем и устанавливаем
                    Alt.Log($"[AutoUpdate] 🚀 Starting automatic update to version {latestVersion}...");
                    var success = await DownloadAndInstallAsync(updateInfo);
                    
                    if (success)
                    {
                        Alt.Log($"[AutoUpdate] 🎉 Update to {latestVersion} completed successfully!");
                        Alt.Log("[AutoUpdate] 🔄 Please restart the server to load new version");
                    }
                    
                    return updateInfo;
                }
                else if (comparison == 0)
                {
                    Alt.Log("[AutoUpdate] ✅ Already on latest version");
                }
                else
                {
                    Alt.LogWarning("[AutoUpdate] ⚠️ Current version is newer than backend version!");
                }
                
                return null;
            }
            catch (HttpRequestException ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Update check error: {ex.Message}");
                return null;
            }
            finally
            {
                _isChecking = false;
            }
        }

        /// <summary>
        /// Скачивает и подготавливает обновление (распаковывает в meshhub_new)
        /// Фактическая установка произойдет при следующем запуске сервера
        /// </summary>
        public async Task<bool> DownloadAndInstallAsync(UpdateInfo updateInfo)
        {
            try
            {
                Alt.Log($"[AutoUpdate] 📥 Starting download of version {updateInfo.Version}...");
                Alt.Log($"[AutoUpdate] File size: {updateInfo.FileSize / 1024.0 / 1024.0:F2} MB");
                
                // Удаляем старую папку meshhub_new если существует
                if (Directory.Exists(_newVersionPath))
                {
                    Alt.Log($"[AutoUpdate] Removing old pending update: {_newVersionPath}");
                    Directory.Delete(_newVersionPath, true);
                }
                
                // DownloadUrl уже содержится в updateInfo (получили из /api/software)
                // Это относительный путь, добавляем BACKEND_URL
                var downloadUrl = updateInfo.DownloadUrl.StartsWith("http") 
                    ? updateInfo.DownloadUrl 
                    : $"{BACKEND_URL}{updateInfo.DownloadUrl}";
                    
                Alt.Log($"[AutoUpdate] Downloading from: {downloadUrl}");
                
                // Скачиваем ZIP файл
                var downloadResponse = await _httpClient.GetAsync(downloadUrl);
                
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    Alt.LogError($"[AutoUpdate] ❌ Download failed: {downloadResponse.StatusCode}");
                    return false;
                }
                
                var zipData = await downloadResponse.Content.ReadAsByteArrayAsync();
                
                // Сохраняем ZIP временно
                var tempDir = Path.Combine(Path.GetTempPath(), "meshhub_updates");
                Directory.CreateDirectory(tempDir);
                
                var zipPath = Path.Combine(tempDir, $"meshhub_{updateInfo.Version}.zip");
                await File.WriteAllBytesAsync(zipPath, zipData);
                
                Alt.Log($"[AutoUpdate] ✅ Downloaded: {zipPath} ({zipData.Length / 1024.0 / 1024.0:F2} MB)");
                
                // Распаковываем в meshhub_new
                Alt.Log("[AutoUpdate] 📦 Extracting update to meshhub_new...");
                var installSuccess = PrepareUpdate(zipPath, updateInfo.Version);
                
                if (installSuccess)
                {
                    Alt.Log("========================================");
                    Alt.LogWarning($"✅ UPDATE DOWNLOADED AND PREPARED!");
                    Alt.LogWarning($"📦 New version {updateInfo.Version} is ready");
                    Alt.LogWarning("⚠️  PLEASE RESTART THE SERVER");
                    Alt.LogWarning("⚠️  The update will be applied on next startup");
                    Alt.Log("========================================");
                    return true;
                }
                else
                {
                    Alt.LogError("[AutoUpdate] ❌ Failed to prepare update");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Download/Install error: {ex.Message}");
                Alt.LogError($"[AutoUpdate] Stack trace: {ex.StackTrace}");
                
                // Очищаем meshhub_new если что-то пошло не так
                if (Directory.Exists(_newVersionPath))
                {
                    try
                    {
                        Directory.Delete(_newVersionPath, true);
                    }
                    catch { }
                }
                
                return false;
            }
        }

        /// <summary>
        /// Создает бэкап текущей версии (переименование meshhub → meshhub.old)
        /// </summary>
        private bool CreateBackup()
        {
            try
            {
                // Останавливаем ресурс перед созданием бэкапа
                Alt.Log($"[AutoUpdate] 🛑 Stopping {RESOURCE_NAME} resource...");
                Alt.StopResource(RESOURCE_NAME);
                Alt.Log($"[AutoUpdate] ✅ Resource {RESOURCE_NAME} stopped");
                
                // Небольшая задержка для освобождения файлов
                System.Threading.Thread.Sleep(1000);
                
                // Удаляем старый бэкап если существует
                if (Directory.Exists(_backupPath))
                {
                    Alt.Log($"[AutoUpdate] Removing old backup: {_backupPath}");
                    Directory.Delete(_backupPath, true);
                }
                
                // Переименовываем текущий ресурс в .old
                if (Directory.Exists(_resourcePath))
                {
                    Alt.Log($"[AutoUpdate] Renaming {RESOURCE_NAME} → {RESOURCE_NAME}.old");
                    Directory.Move(_resourcePath, _backupPath);
                    Alt.Log($"[AutoUpdate] ✅ Backup created: {_backupPath}");
                    return true;
                }
                else
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Resource directory not found: {_resourcePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Backup creation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Подготавливает обновление - распаковывает в meshhub_new
        /// Фактическая установка произойдет при следующем запуске сервера
        /// </summary>
        private bool PrepareUpdate(string zipPath, string version)
        {
            try
            {
                Alt.Log($"[AutoUpdate] Preparing update from: {zipPath}");
                
                // Создаем временную директорию для распаковки
                var extractPath = Path.Combine(Path.GetTempPath(), $"meshhub_extract_{version}_{DateTime.Now.Ticks}");
                Directory.CreateDirectory(extractPath);
                
                // Распаковываем ZIP
                Alt.Log("[AutoUpdate] 📦 Extracting ZIP archive...");
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                Alt.Log("[AutoUpdate] ✅ ZIP extracted successfully");
                
                // Проверяем структуру ZIP: если внутри есть папка meshhub, используем её
                var sourcePath = extractPath;
                var possibleNestedPath = Path.Combine(extractPath, RESOURCE_NAME);
                
                if (Directory.Exists(possibleNestedPath))
                {
                    // ZIP содержит папку meshhub внутри
                    Alt.Log($"[AutoUpdate] 📂 Detected nested {RESOURCE_NAME} folder in ZIP");
                    sourcePath = possibleNestedPath;
                }
                
                Alt.Log($"[AutoUpdate] Source path for copy: {sourcePath}");
                
                // Создаем директорию meshhub_new
                Directory.CreateDirectory(_newVersionPath);
                
                // Копируем файлы из распакованного архива в meshhub_new
                Alt.Log($"[AutoUpdate] 🔄 Copying files to {RESOURCE_NAME}_new...");
                CopyDirectory(sourcePath, _newVersionPath);
                
                Alt.Log("[AutoUpdate] ✅ Files copied to meshhub_new successfully");
                
                // Очищаем временные файлы
                Alt.Log("[AutoUpdate] 🗑️ Cleaning up temporary files...");
                try
                {
                    Directory.Delete(extractPath, true);
                    File.Delete(zipPath);
                }
                catch
                {
                    // Игнорируем ошибки очистки
                }
                
                Alt.Log($"[AutoUpdate] ✅ Update prepared successfully in {RESOURCE_NAME}_new");
                
                return true;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Preparation failed: {ex.Message}");
                Alt.LogError($"[AutoUpdate] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Восстанавливает бэкап (переименование meshhub.old → meshhub)
        /// </summary>
        private bool RestoreBackup()
        {
            try
            {
                if (!Directory.Exists(_backupPath))
                {
                    Alt.LogError("[AutoUpdate] ❌ Backup directory not found, cannot restore");
                    return false;
                }
                
                Alt.Log($"[AutoUpdate] 🔄 Restoring backup from: {_backupPath}");
                
                // Удаляем поврежденную версию если существует
                if (Directory.Exists(_resourcePath))
                {
                    Directory.Delete(_resourcePath, true);
                }
                
                // Восстанавливаем из бэкапа
                Directory.Move(_backupPath, _resourcePath);
                
                Alt.Log("[AutoUpdate] ✅ Backup restored successfully");
                return true;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Restore failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Копирует содержимое одной директории в другую (с перезаписью)
        /// Используется для обновления без переименования папок
        /// </summary>
        private void CopyDirectoryContents(string sourceDir, string destinationDir, bool overwrite = false)
        {
            var dir = new DirectoryInfo(sourceDir);
            
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }
            
            // Создаем директорию назначения если не существует
            Directory.CreateDirectory(destinationDir);
            
            int filesCopied = 0;
            int directoriesCopied = 0;
            
            // Копируем файлы
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetPath, overwrite);
                filesCopied++;
            }
            
            // Рекурсивно копируем поддиректории
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string targetPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryContentsRecursive(subDir.FullName, targetPath, overwrite, ref filesCopied, ref directoriesCopied);
                directoriesCopied++;
            }
            
            Alt.Log($"[AutoUpdate] Copied {filesCopied} files and {directoriesCopied} directories");
        }
        
        /// <summary>
        /// Рекурсивный помощник для копирования директорий
        /// </summary>
        private void CopyDirectoryContentsRecursive(string sourceDir, string destinationDir, bool overwrite, ref int filesCopied, ref int directoriesCopied)
        {
            var dir = new DirectoryInfo(sourceDir);
            
            // Создаем директорию назначения
            Directory.CreateDirectory(destinationDir);
            
            // Копируем файлы
            foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    string targetPath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetPath, overwrite);
                    filesCopied++;
                }
                catch (Exception ex)
                {
                    // Логируем но продолжаем копирование
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Failed to copy file {file.Name}: {ex.Message}");
                }
            }
            
            // Рекурсивно копируем поддиректории (исключая только .git и другие VCS папки)
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                // Пропускаем ТОЛЬКО служебные папки VCS (НЕ исключаем node_modules - она нужна!)
                if (subDir.Name == ".git" || subDir.Name == ".svn" || subDir.Name == ".vs" || subDir.Name == ".idea")
                {
                    Alt.Log($"[AutoUpdate] ⏭️ Skipping directory: {subDir.Name}");
                    continue;
                }
                
                string targetPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryContentsRecursive(subDir.FullName, targetPath, overwrite, ref filesCopied, ref directoriesCopied);
                directoriesCopied++;
            }
        }
        
        /// <summary>
        /// Копирует директорию (старый метод для обратной совместимости)
        /// </summary>
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            CopyDirectoryContents(sourceDir, destinationDir, overwrite: true);
        }

        /// <summary>
        /// Сравнивает версии (semver)
        /// </summary>
        /// <returns>1 если v1 > v2, -1 если v1 < v2, 0 если равны</returns>
        private int CompareVersions(string v1, string v2)
        {
            try
            {
                var parts1 = v1.Split('.');
                var parts2 = v2.Split('.');
                
                int maxLength = Math.Max(parts1.Length, parts2.Length);
                
                for (int i = 0; i < maxLength; i++)
                {
                    int p1 = i < parts1.Length && int.TryParse(parts1[i], out int parsed1) ? parsed1 : 0;
                    int p2 = i < parts2.Length && int.TryParse(parts2[i], out int parsed2) ? parsed2 : 0;
                    
                    if (p1 > p2) return 1;
                    if (p1 < p2) return -1;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] Version comparison error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Запускает PowerShell скрипт для обновления файлов
        /// </summary>
        private bool RunUpdateScript()
        {
            try
            {
                var cwd = Directory.GetCurrentDirectory();
                var scriptPath = Path.Combine(cwd, "update-meshhub.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Update script not found: {scriptPath}");
                    return false;
                }
                
                Alt.Log($"[AutoUpdate] 🔧 Executing PowerShell script: {scriptPath}");
                
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = cwd
                };
                
                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        Alt.LogError("[AutoUpdate] ❌ Failed to start PowerShell process");
                        return false;
                    }
                    
                    // Читаем вывод скрипта
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit(30000); // Ждём максимум 30 секунд
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Alt.Log($"[AutoUpdate] Script output:\n{output}");
                    }
                    
                    if (!string.IsNullOrEmpty(errors))
                    {
                        Alt.LogWarning($"[AutoUpdate] Script errors:\n{errors}");
                    }
                    
                    if (process.ExitCode == 0)
                    {
                        Alt.Log("[AutoUpdate] ✅ PowerShell script completed successfully");
                        return true;
                    }
                    else
                    {
                        Alt.LogError($"[AutoUpdate] ❌ PowerShell script failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Failed to run PowerShell script: {ex.Message}");
                Alt.LogError($"[AutoUpdate] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Переименовывает директорию с повторными попытками
        /// </summary>
        private void RetryMoveDirectory(string source, string destination, int maxRetries = 5)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        Alt.Log($"[AutoUpdate] Retry attempt {attempt}/{maxRetries}...");
                        System.Threading.Thread.Sleep(1000 * attempt); // Увеличивающаяся задержка
                    }
                    
                    Directory.Move(source, destination);
                    return; // Успешно
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Move failed (attempt {attempt}/{maxRetries}): {ex.Message}");
                    // Продолжаем попытки
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Access denied (attempt {attempt}/{maxRetries}): {ex.Message}");
                    // Продолжаем попытки
                }
            }
            
            // Все попытки исчерпаны
            throw new IOException($"Failed to move directory after {maxRetries} attempts: {source} → {destination}");
        }
        
        /// <summary>
        /// Удаляет директорию с повторными попытками
        /// </summary>
        private void RetryDeleteDirectory(string path, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        Alt.Log($"[AutoUpdate] Delete retry {attempt}/{maxRetries}...");
                        System.Threading.Thread.Sleep(1000);
                    }
                    
                    Directory.Delete(path, true);
                    return; // Успешно
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Delete failed (attempt {attempt}/{maxRetries}): {ex.Message}");
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Delete access denied (attempt {attempt}/{maxRetries}): {ex.Message}");
                }
            }
            
            // Не критично если не удалось удалить backup
            Alt.LogWarning($"[AutoUpdate] ⚠️ Could not delete {path} after {maxRetries} attempts");
        }

        /// <summary>
        /// Проверяет что credentials настроены
        /// </summary>
        private bool IsConfigured()
        {
            return !string.IsNullOrEmpty(SOFTWARE_ID) && 
                   !SOFTWARE_ID.StartsWith("ЗАМЕНИТЕ") &&
                   !string.IsNullOrEmpty(API_KEY) &&
                   !API_KEY.StartsWith("ЗАМЕНИТЕ");
        }

        /// <summary>
        /// Маскирует секретное значение для безопасного логирования
        /// </summary>
        private string MaskSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
            {
                return "***";
            }
            
            return secret.Substring(0, 4) + "***" + secret.Substring(secret.Length - 4);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            _updateCheckTimer?.Dispose();
            _httpClient?.Dispose();
        }

        // ==================== Вспомогательные классы ====================

        /// <summary>
        /// Информация о программном обеспечении с backend (из списка /api/software)
        /// </summary>
        private class SoftwareInfo
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string? Name { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("version")]
            public string? Version { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("download_url")]
            public string? DownloadUrl { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("file_size")]
            public long FileSize { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("server_type")]
            public string? ServerType { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("is_active")]
            public bool IsActive { get; set; }
        }

        /// <summary>
        /// Информация об обновлении
        /// </summary>
        public class UpdateInfo
        {
            public string Id { get; set; } = "";
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public long FileSize { get; set; }
            public string Name { get; set; } = "";
        }
    }
}

