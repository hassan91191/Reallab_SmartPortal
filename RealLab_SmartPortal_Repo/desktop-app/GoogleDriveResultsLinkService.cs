using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System.Collections.Generic;
using Google.Apis.Upload;

// ✅ Aliases لحل التعارض
using IOFile = System.IO.File;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace WhatsApp_Auto_Sender
{
    public class GoogleDriveResultsLinkService
    {
        private readonly ResultsLinkSettings _settings;
        private DriveService _drive;

        private const string OAuthJsonFileName = "google_oauth.json";

        private class GoogleOAuthConfig
        {
            [JsonProperty("client_id")]
            public string ClientId { get; set; }

            [JsonProperty("client_secret")]
            public string ClientSecret { get; set; }
        }

        public GoogleDriveResultsLinkService(ResultsLinkSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private string GetTokenStorePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhatsAppAutoSender",
                "GoogleTokenStore");

            Directory.CreateDirectory(dir);
            return dir;
        }

        private GoogleOAuthConfig LoadOAuthConfigOrThrow()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(exeDir, OAuthJsonFileName);

            if (!IOFile.Exists(path))
            {
                throw new Exception(
                    $"ملف Google OAuth غير موجود.\n" +
                    $"حط ملف '{OAuthJsonFileName}' جنب البرنامج (جنب ملف exe).\n" +
                    $"المسار المتوقع:\n{path}");
            }

            string json = IOFile.ReadAllText(path);

            GoogleOAuthConfig cfg;
            try
            {
                cfg = JsonConvert.DeserializeObject<GoogleOAuthConfig>(json);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"ملف {OAuthJsonFileName} موجود لكن فيه مشكلة في الفورمات (JSON).\n" +
                    $"الخطأ: {ex.Message}");
            }

            if (cfg == null ||
                string.IsNullOrWhiteSpace(cfg.ClientId) ||
                string.IsNullOrWhiteSpace(cfg.ClientSecret))
            {
                throw new Exception(
                    $"ملف {OAuthJsonFileName} ناقص.\n" +
                    $"لازم يبقى فيه client_id و client_secret.");
            }

            return cfg;
        }

        public async Task EnsureAuthenticatedAsync()
        {
            if (_drive != null) return;

            var cfg = LoadOAuthConfigOrThrow();

            var secrets = new ClientSecrets
            {
                ClientId = cfg.ClientId.Trim(),
                ClientSecret = cfg.ClientSecret.Trim()
            };

            var scopes = new[] { DriveService.Scope.DriveFile };

            var cred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(GetTokenStorePath(), true));

            _drive = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "WhatsAppAutoSender-ResultsLink"
            });
        }

        public async Task<string> EnsureLabResultsRootFolderAsync()
        {
            await EnsureAuthenticatedAsync();

            string folderName = "Lab results";

            string q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false";
            var listReq = _drive.Files.List();
            listReq.Q = q;
            listReq.Fields = "files(id,name)";
            listReq.Spaces = "drive";

            var list = await listReq.ExecuteAsync();
            if (list.Files != null && list.Files.Count > 0)
                return list.Files[0].Id;

            var meta = new DriveFile
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };

            var createReq = _drive.Files.Create(meta);
            createReq.Fields = "id";
            var created = await createReq.ExecuteAsync();
            return created.Id;
        }

        // =========================================================
        // ✅ دالة إنشاء أو جلب فولدر المريض (بالاسم)
        // =========================================================
        public async Task<(string folderId, string folderUrl)> CreateOrGetPatientFolderAsync(string folderName)
        {
            await EnsureAuthenticatedAsync();

            if (string.IsNullOrWhiteSpace(_settings.GoogleRootFolderId))
            {
                _settings.GoogleRootFolderId = await EnsureLabResultsRootFolderAsync();
                _settings.Save();
            }

            string rootId = _settings.GoogleRootFolderId;
            // تنظيف الاسم من أي رموز غريبة
            string safeName = (folderName ?? "").Replace("'", "");

            // البحث عن الفولدر بالاسم داخل المجلد الرئيسي
            string q = $"mimeType='application/vnd.google-apps.folder' and name='{safeName}' and trashed=false and '{rootId}' in parents";

            var listReq = _drive.Files.List();
            listReq.Q = q;
            listReq.Fields = "files(id, name, webViewLink)";
            listReq.Spaces = "drive";

            var list = await listReq.ExecuteAsync();

            if (list.Files != null && list.Files.Count > 0)
            {
                // لقيناه موجود -> نرجعه
                var f = list.Files[0];
                string url = !string.IsNullOrWhiteSpace(f.WebViewLink)
                    ? f.WebViewLink
                    : ("https://drive.google.com/drive/folders/" + f.Id);

                await EnsureAnyoneWithLinkAsync(f.Id);
                return (f.Id, url);
            }

            // مش موجود -> ننشئه جديد
            var fileMeta = new Google.Apis.Drive.v3.Data.File
            {
                Name = safeName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new[] { rootId }
            };

            var createReq = _drive.Files.Create(fileMeta);
            createReq.Fields = "id, webViewLink";
            var created = await createReq.ExecuteAsync();

            await EnsureAnyoneWithLinkAsync(created.Id);

            string folderUrl = !string.IsNullOrWhiteSpace(created.WebViewLink)
                ? created.WebViewLink
                : ("https://drive.google.com/drive/folders/" + created.Id);

            return (created.Id, folderUrl);
        }

        private async Task EnsureAnyoneWithLinkAsync(string folderId)
        {
            var perm = new Permission
            {
                Type = "anyone",
                Role = "reader",
                AllowFileDiscovery = false
            };

            var req = _drive.Permissions.Create(perm, folderId);
            req.Fields = "id";

            try { await req.ExecuteAsync(); }
            catch { }
        }
        public async Task<string> FindFileIdInFolderAsync(string parentFolderId, string fileName)
        {
            await EnsureAuthenticatedAsync();

            string safeName = (fileName ?? "").Replace("'", "\\'");
            string q = $"'{parentFolderId}' in parents and trashed=false and name='{safeName}'";

            var listReq = _drive.Files.List();
            listReq.Q = q;
            listReq.Fields = "files(id,name)";
            listReq.Spaces = "drive";

            var list = await listReq.ExecuteAsync();
            if (list.Files != null && list.Files.Count > 0)
                return list.Files[0].Id;

            return null;
        }

        public async Task<string> UploadOrReplaceFileAsync(string parentFolderId, string localPath, string driveFileName)
        {
            await EnsureAuthenticatedAsync();

            if (string.IsNullOrWhiteSpace(parentFolderId))
                throw new Exception("parentFolderId فاضي");

            if (string.IsNullOrWhiteSpace(localPath) || !IOFile.Exists(localPath))
                throw new Exception("الملف المحلي غير موجود: " + localPath);

            if (string.IsNullOrWhiteSpace(driveFileName))
                driveFileName = Path.GetFileName(localPath);

            // MimeType بسيط
            string ext = Path.GetExtension(localPath).ToLower();
            string mime =
                (ext == ".pdf") ? "application/pdf" :
                (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" :
                (ext == ".png") ? "image/png" :
                "application/octet-stream";

            string existingId = await FindFileIdInFolderAsync(parentFolderId, driveFileName);

            using (var stream = IOFile.OpenRead(localPath))
            {
                if (!string.IsNullOrWhiteSpace(existingId))
                {
                    // UPDATE (Replace)
                    var meta = new DriveFile { Name = driveFileName };
                    var upd = _drive.Files.Update(meta, existingId, stream, mime);
                    upd.Fields = "id";
                    IUploadProgress p = await upd.UploadAsync();
                    if (p.Status != UploadStatus.Completed)
                        throw new Exception("فشل تحديث الملف على Drive: " + (p.Exception?.Message ?? p.Status.ToString()));

                    return existingId;
                }
                else
                {
                    // CREATE
                    var meta = new DriveFile
                    {
                        Name = driveFileName,
                        Parents = new[] { parentFolderId }
                    };

                    var create = _drive.Files.Create(meta, stream, mime);
                    create.Fields = "id";
                    IUploadProgress p = await create.UploadAsync();
                    if (p.Status != UploadStatus.Completed)
                        throw new Exception("فشل رفع الملف على Drive: " + (p.Exception?.Message ?? p.Status.ToString()));

                    return create.ResponseBody?.Id;
                }
            }
        }

        public async Task DeleteAllFilesInFolderAsync(string folderId)
        {
            await EnsureAuthenticatedAsync();

            if (string.IsNullOrWhiteSpace(folderId))
                return;

            string pageToken = null;

            do
            {
                var listReq = _drive.Files.List();
                listReq.Q = $"'{folderId}' in parents and trashed=false";
                listReq.Fields = "nextPageToken, files(id,name)";
                listReq.Spaces = "drive";
                listReq.PageToken = pageToken;

                var list = await listReq.ExecuteAsync();

                if (list.Files != null)
                {
                    foreach (var f in list.Files)
                    {
                        try
                        {
                            await _drive.Files.Delete(f.Id).ExecuteAsync();
                        }
                        catch { }
                    }
                }

                pageToken = list.NextPageToken;
            }
            while (!string.IsNullOrWhiteSpace(pageToken));
        }

    }
}
