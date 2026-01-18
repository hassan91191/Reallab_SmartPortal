using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace WhatsApp_Auto_Sender
{
    public class ResultsLinkQueueWorker
    {
        private readonly ResultsLinkSettings _settings;
        private readonly GoogleDriveResultsLinkService _drive;

        private CancellationTokenSource _cts;
        private Task _loopTask;

        // ✅ Wake-up signal (بدل ما يستنى polling فقط)
        private readonly AutoResetEvent _wakeUp = new AutoResetEvent(false);

        public ResultsLinkQueueWorker(ResultsLinkSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _drive = new GoogleDriveResultsLinkService(_settings);
        }

        public void Start()
        {
            if (_loopTask != null) return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _wakeUp.Set(); } catch { } // فك الانتظار
            _loopTask = null;
        }

        // ✅ نقدر نصحّي العامل فورًا (بدون تأخير الطباعة)
        public void WakeUpNow()
        {
            try { _wakeUp.Set(); } catch { }
        }

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_settings.Enabled)
                    {
                        // ✅ معالجة كل العناصر المتاحة بدل عنصر واحد فقط
                        await ProcessAvailableAsync();
                    }
                }
                catch { }

                int delayMs = Math.Max(1, _settings.QueuePollSeconds) * 1000;

                // ✅ استنى يا إمّا wakeUp يا إمّا timeout يا إمّا cancel
                int idx = WaitHandle.WaitAny(
                    new WaitHandle[] { _wakeUp, token.WaitHandle },
                    delayMs
                );

                if (idx == 1) // token cancelled
                    break;
            }
        }

        private async Task ProcessAvailableAsync()
        {
            // عالج لحد ما ميبقاش فيه Pending
            while (true)
            {
                bool didOne = await ProcessOneAsync();
                if (!didOne) break;
            }
        }

        private string BuildSqlConnectionString()
        {
            if (_settings == null)
                throw new Exception("ResultsLinkSettings غير موجودة.");

            // ===== Option 1: Manual SQL (من الفورم) =====
            if (_settings.UseManualSql)
            {
                if (string.IsNullOrWhiteSpace(_settings.SqlServerOrIp))
                    throw new Exception("SQL Server/IP غير مضبوط في الإعدادات.");

                var b = new SqlConnectionStringBuilder();
                b.DataSource = _settings.SqlServerOrIp.Trim();
                b.InitialCatalog = string.IsNullOrWhiteSpace(_settings.SqlDatabase) ? "Patients" : _settings.SqlDatabase.Trim();
                b.UserID = string.IsNullOrWhiteSpace(_settings.SqlUser) ? "sa" : _settings.SqlUser.Trim();
                b.Password = _settings.SqlPassword ?? "";
                b.PersistSecurityInfo = true;
                b.ConnectTimeout = 10;
                b.Encrypt = false;
                b.TrustServerCertificate = true;
                return b.ToString();
            }

            // ===== Option 2: BASE.ini =====
            string basePath = _settings.BaseIniPath;

            if (string.IsNullOrWhiteSpace(basePath) || !System.IO.File.Exists(basePath))
            {
                basePath = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                if (string.IsNullOrWhiteSpace(basePath))
                    throw new Exception("لم يتم العثور على ملف BASE لبناء اتصال قاعدة البيانات.");

                _settings.BaseIniPath = basePath;
                _settings.Save();
            }

            string user = string.IsNullOrWhiteSpace(_settings.SqlUser) ? "sa" : _settings.SqlUser.Trim();
            string pass = _settings.SqlPassword ?? "";

            return BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(basePath, user, pass);
        }

        // ✅ ترجع true لو عالج عنصر، false لو مفيش Pending
        private async Task<bool> ProcessOneAsync()
        {
            string cs = BuildSqlConnectionString();

            using (var con = new SqlConnection(cs))
            {
                con.Open();

                string pickSql = @"
SELECT TOP 1 Id, PatientId
FROM dbo.WA_ResultLinkQueue
WHERE Status = 0
ORDER BY Id ASC";

                int id = 0;
                string patientId = null;

                using (var cmd = new SqlCommand(pickSql, con))
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        id = Convert.ToInt32(r["Id"]);
                        patientId = Convert.ToString(r["PatientId"]);
                    }
                }

                if (id == 0 || string.IsNullOrWhiteSpace(patientId))
                    return false;

                using (var cmd = new SqlCommand("UPDATE dbo.WA_ResultLinkQueue SET Status=1, Attempts=Attempts+1 WHERE Id=@Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    var (folderId, folderUrl) = await _drive.CreateOrGetPatientFolderAsync(patientId);

                    string upsert = @"
IF EXISTS (SELECT 1 FROM dbo.WA_PatientDriveLinks WHERE PatientId=@PatientId)
    UPDATE dbo.WA_PatientDriveLinks SET FolderId=@FolderId, FolderUrl=@FolderUrl WHERE PatientId=@PatientId
ELSE
    INSERT INTO dbo.WA_PatientDriveLinks (PatientId, FolderId, FolderUrl) VALUES (@PatientId, @FolderId, @FolderUrl)";

                    using (var cmd = new SqlCommand(upsert, con))
                    {
                        cmd.Parameters.AddWithValue("@PatientId", patientId);
                        cmd.Parameters.AddWithValue("@FolderId", folderId);
                        cmd.Parameters.AddWithValue("@FolderUrl", folderUrl);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SqlCommand("UPDATE dbo.WA_ResultLinkQueue SET Status=2, LastError=NULL WHERE Id=@Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    using (var cmd = new SqlCommand("UPDATE dbo.WA_ResultLinkQueue SET Status=3, LastError=@E WHERE Id=@Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@E", ex.Message);
                        cmd.ExecuteNonQuery();
                    }
                }

                return true;
            }
        }

        public static string LookupFolderUrl(ResultsLinkSettings s, string patientId)
        {
            if (s == null || string.IsNullOrWhiteSpace(patientId))
                return null;

            string cs;

            if (s.UseManualSql)
            {
                if (string.IsNullOrWhiteSpace(s.SqlServerOrIp))
                    return null;

                var b = new SqlConnectionStringBuilder();
                b.DataSource = s.SqlServerOrIp.Trim();
                b.InitialCatalog = string.IsNullOrWhiteSpace(s.SqlDatabase) ? "Patients" : s.SqlDatabase.Trim();
                b.UserID = string.IsNullOrWhiteSpace(s.SqlUser) ? "sa" : s.SqlUser.Trim();
                b.Password = s.SqlPassword ?? "";
                b.PersistSecurityInfo = true;
                b.ConnectTimeout = 10;
                b.Encrypt = false;
                b.TrustServerCertificate = true;
                cs = b.ToString();
            }
            else
            {
                string baseIniPath = s.BaseIniPath;

                if (string.IsNullOrWhiteSpace(baseIniPath) || !System.IO.File.Exists(baseIniPath))
                {
                    baseIniPath = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                    if (string.IsNullOrWhiteSpace(baseIniPath))
                        return null;
                }

                string user = string.IsNullOrWhiteSpace(s.SqlUser) ? "sa" : s.SqlUser.Trim();
                string pass = s.SqlPassword ?? "";

                cs = BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(baseIniPath, user, pass);
            }

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                using (var cmd = new SqlCommand("SELECT FolderUrl FROM dbo.WA_PatientDriveLinks WHERE PatientId=@P", con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    var v = cmd.ExecuteScalar();
                    return v == null ? null : Convert.ToString(v);
                }
            }
        }
    }
}
