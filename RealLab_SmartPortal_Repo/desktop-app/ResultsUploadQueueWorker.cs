using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfiumViewer;

namespace WhatsApp_Auto_Sender
{
    // ✅ مهم: Settings الصحيح (مش WhatsAppAutoSender)
    using AppSettings = global::WhatsAppAutoSender.Properties.Settings;

    public class ResultsUploadQueueWorker
    {
        private readonly ResultsLinkSettings _settings;
        private readonly GoogleDriveResultsLinkService _drive;

        private CancellationTokenSource _cts;
        private Task _loopTask;

        // ✅ Wake-up signal (زي ResultsLinkQueueWorker)
        private readonly AutoResetEvent _wakeUp = new AutoResetEvent(false);

        public ResultsUploadQueueWorker(ResultsLinkSettings settings)
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
            try { _wakeUp.Set(); } catch { }
            _loopTask = null;
        }

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
                    if (_settings.Enabled && _settings.UploadMode == 2)
                    {
                        await ProcessPatientsWatch_30DaysAsync(token);
                    }
                }
                catch
                {
                    // تجاهل علشان العامل ميقفش
                }

                int delayMs = Math.Max(1, _settings.QueuePollSeconds) * 1000;

                int idx = WaitHandle.WaitAny(
                    new WaitHandle[] { _wakeUp, token.WaitHandle },
                    delayMs
                );

                if (idx == 1)
                    break;
            }
        }

        // =========================================================
        // ✅ الطريقة A: تابع المرضى الجدد فقط لمدة 30 يوم
        // المصدر: WA_ResultLinkQueue (اللي بيتغذي من Trigger على patientinfo)
        // شرطنا: أي PatientId اتعمله Queue في آخر 30 يوم => نراقب فولدره المحلي على D:\PDF\<id>
        // =========================================================
        // =========================================================
        // ✅ التعديل الجديد: اللامركزية (Decentralized Watch)
        // كل جهاز يراقب المريض محليًا، لو عنده ملفات (أحدث) يرفعها، لو معندوش يسكت.
        // =========================================================
        private async Task ProcessPatientsWatch_30DaysAsync(CancellationToken token)
        {
            string root = (_settings.ExportWatchRootFolder ?? "").Trim(); // مثال: D:\PDF

            // لو أساسًا الفولدر الكبير مش موجود على الجهاز ده، اخرج فورًا
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            string cs = BuildSqlConnectionString();

            // 1. هات قائمة المرضى المطلوب متابعتهم من الداتابيز (آخر 30 يوم)
            var patientIds = new List<string>();

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);
                // بنجيب المرضى اللي لسه في فترة المتابعة (WatchUntil >= النهاردة)
                using (var cmd = new SqlCommand(@"
                    SELECT DISTINCT PatientId 
                    FROM dbo.WA_PatientWatch 
                    WHERE WatchUntil >= GETDATE()", con))
                using (var r = await cmd.ExecuteReaderAsync(token))
                {
                    while (await r.ReadAsync(token))
                    {
                        string pid = Convert.ToString(r.GetValue(0));
                        if (!string.IsNullOrWhiteSpace(pid))
                            patientIds.Add(pid.Trim());
                    }
                }
            }

            // 2. الفحص المحلي (Local Check) لكل مريض
            foreach (var patientId in patientIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();

                string patientDir = Path.Combine(root, patientId);

                // =================================================================================
                // 🛑 التعديل الجوهري (الحماية):
                // لو فولدر المريض مش موجود عندي على الجهاز، أو فاضي.. 
                // إذن "أنا لست مصدر البيانات" -> اعمل Skip فوراً (عشان ممسحش شغل غيري من على الدرايف)
                // =================================================================================
                if (!Directory.Exists(patientDir))
                    continue;

                // كمان نتأكد إن فيه ملفات بجد (مش فولدر فاضي)
                var localFiles = Directory.GetFiles(patientDir)
                                          .Where(IsAllowedExt)
                                          .ToArray();

                if (localFiles.Length == 0)
                    continue; // أنا معنديش ملفات للمريض ده، يبقى مش دوري أرفع

                // =================================================================================

                // 3. حساب البصمة المحلية ومقارنتها بالداتابيز
                // (عشان نعرف هل ملفاتي أنا "جديدة" ومحتاجة تترفع؟ ولا هي هي اللي اترفعت قبل كده؟)
                string currentSig = ComputeFolderSignature(patientDir);
                string prevSig = await GetPrevSignatureAsync(cs, patientId, token);

                // لو البصمة زي ما هي، يبقى ملفاتي دي اترفعت قبل كده (سواء مني أو من جهاز تاني)
                if (string.Equals(currentSig, prevSig, StringComparison.OrdinalIgnoreCase))
                    continue; // خلاص متعملش حاجة، الـ Drive متحدث

                // 4. اكتشفنا اختلاف! (currentSig != prevSig)
                // ده معناه إن الملفات اللي عندي "أحدث" أو "مختلفة" عن اللي متسجل في السيرفر
                // إذن: أنا المصدر الحقيقي دلوقتي -> لازم أفرض نسختي
                try
                {
                    // ارفع ملفاتي (أنا المصدر، فهعمل Overwrite للي على الدرايف)
                    await SyncPatientFolder_FullResyncAsync(cs, patientId, patientDir, token);

                    // سجل بصمتي الجديدة في الداتابيز (عشان باقي الأجهزة تعرف إن التحديث تم وتشوف نفس البصمة فتسكت)
                    await UpsertSignatureAsync(cs, patientId, currentSig, token);
                }
                catch (Exception ex)
                {
                    await SaveSyncErrorAsync(cs, patientId, ex.Message, token);
                }
            }
        }

        // =========================================================
        // ✅ الحل النهائي: (Time Barrier Filter)
        // 1. اسم الفولدر هو ID المريض فقط (عشان اللينك يفضل ثابت).
        // 2. بنجيب تاريخ تسجيل المريض من الداتابيز.
        // 3. بنفلتر الملفات المحلية: أي ملف تاريخه "أقدم" من التسجيل بنتجاهله.
        // =========================================================
        private async Task SyncPatientFolder_FullResyncAsync(string cs, string patientId, string patientDir, CancellationToken token)
        {
            await _drive.EnsureAuthenticatedAsync();

            // 1) المتغيرات الزمنية
            DateTime registrationDateServer = DateTime.MinValue; // وقت التسجيل بتوقيت السيرفر
            DateTime serverNow = DateTime.MinValue;              // الساعة كام دلوقتي على السيرفر؟

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);

                // بنجيب (وقت التسجيل) و (وقت السيرفر الحالي) في نفس الاستعلام
                // عشان نحسب فرق التوقيت بين جهازك والسيرفر
                string sql = @"
                    SELECT TOP 1 CreatedAt, GETDATE() 
                    FROM dbo.WA_ResultLinkQueue 
                    WHERE PatientId=@P 
                    ORDER BY Id DESC";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    using (var r = await cmd.ExecuteReaderAsync(token))
                    {
                        if (await r.ReadAsync(token))
                        {
                            var valCreate = r.GetValue(0);
                            var valServerNow = r.GetValue(1);

                            if (valCreate != DBNull.Value) registrationDateServer = Convert.ToDateTime(valCreate);
                            if (valServerNow != DBNull.Value) serverNow = Convert.ToDateTime(valServerNow);
                        }
                    }
                }
            }

            // لو ملقناش داتا (نادر)، نفترض إن التسجيل حصل دلوقتي
            if (registrationDateServer == DateTime.MinValue) registrationDateServer = DateTime.Now;
            if (serverNow == DateTime.MinValue) serverNow = DateTime.Now;

            // 2) معادلة تصحيح الوقت (Time Correction)
            // بنحسب الفرق بين (ساعة جهازك) و (ساعة السيرفر)
            TimeSpan clockSkew = serverNow - DateTime.Now;

            // بنحول وقت التسجيل من "توقيت السيرفر" لـ "توقيت جهازك"
            DateTime registrationDateLocal = registrationDateServer - clockSkew;

            // 3) اسم الفولدر = ID فقط
            string simpleFolderName = patientId;

            // 4) التأكد من الفولدر وتحديث اللينك
            string folderId = null;
            string folderUrl = null;

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);
                using (var cmd = new SqlCommand("SELECT FolderId, FolderUrl FROM dbo.WA_PatientDriveLinks WHERE PatientId=@P", con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    using (var r = await cmd.ExecuteReaderAsync(token))
                    {
                        if (await r.ReadAsync(token))
                        {
                            folderId = Convert.ToString(r["FolderId"]);
                            folderUrl = Convert.ToString(r["FolderUrl"]);
                        }
                    }
                }
            }

            var folderInfo = await _drive.CreateOrGetPatientFolderAsync(simpleFolderName);

            if (folderInfo.folderId != folderId)
            {
                folderId = folderInfo.folderId;
                folderUrl = folderInfo.folderUrl;

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync(token);
                    string upsert = @"
                        IF EXISTS (SELECT 1 FROM dbo.WA_PatientDriveLinks WHERE PatientId=@PatientId)
                            UPDATE dbo.WA_PatientDriveLinks SET FolderId=@FolderId, FolderUrl=@FolderUrl WHERE PatientId=@PatientId
                        ELSE
                            INSERT INTO dbo.WA_PatientDriveLinks (PatientId, FolderId, FolderUrl) VALUES (@PatientId, @FolderId, @FolderUrl)";

                    using (var cmd = new SqlCommand(upsert, con))
                    {
                        cmd.Parameters.AddWithValue("@PatientId", patientId);
                        cmd.Parameters.AddWithValue("@FolderId", folderId);
                        cmd.Parameters.AddWithValue("@FolderUrl", folderUrl ?? "");
                        await cmd.ExecuteNonQueryAsync(token);
                    }
                }
            }

            // مسح القديم
            await _drive.DeleteAllFilesInFolderAsync(folderId);

            // 5) الفلتر الزمني المصحح (Corrected Time Filter)
            // بناخد وقت التسجيل (المصحح بتوقيت جهازك) ونطرح منه دقيقتين كمان "سماحية" (Safety Margin)
            // عشان لو فيه ثواني فرق، الملف يتقبل
            DateTime cutoffTime = registrationDateLocal.AddMinutes(-2);

            var localFiles = Directory.GetFiles(patientDir)
                .Select(path => new FileInfo(path))
                .Where(fi => IsAllowedExt(fi.FullName))
                // المقارنة الآن دقيقة جداً لأن الطرفين بقوا نفس التوقيت
                .Where(fi => fi.LastWriteTime >= cutoffTime)
                .OrderBy(fi => fi.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 🛑 Fallback (خطة بديلة):
            // لو الفلتر ده رجع صفر ملفات (لأي سبب غريب)، والملفات موجودة فعلاً،
            // والملفات دي "طازة" (معمولة في آخر 15 دقيقة)، يبقى أكيد هي دي المطلوبة، ارفعها وخلاص.
            if (localFiles.Count == 0)
            {
                var fallbackFiles = Directory.GetFiles(patientDir)
                    .Select(path => new FileInfo(path))
                    .Where(fi => IsAllowedExt(fi.FullName))
                    .Where(fi => fi.LastWriteTime >= DateTime.Now.AddMinutes(-15)) // هل الملف لسه معمول حالا؟
                    .ToList();

                if (fallbackFiles.Count > 0)
                {
                    localFiles = fallbackFiles; // اعتمد الملفات دي
                }
                else
                {
                    return; // خلاص مفيش أمل، مفيش ملفات جديدة
                }
            }

            // 6) الرفع
            foreach (var fi in localFiles)
            {
                string f = fi.FullName;
                token.ThrowIfCancellationRequested();
                WaitForFileReady(f);

                string ext = Path.GetExtension(f).ToLowerInvariant();

                if (IsImageExt(ext))
                {
                    string processed = ProcessImageForUpload(f);
                    await _drive.UploadOrReplaceFileAsync(folderId, processed, Path.GetFileName(processed));
                }
                else if (ext == ".pdf")
                {
                    bool convert = false;
                    try { convert = AppSettings.Default.ConvertPdfToImage_Link; } catch { convert = false; }

                    if (convert)
                    {
                        var pages = ConvertPdfToJpeg_MultiPage_Ghostscript(f);
                        int idx = 1;
                        foreach (var page in pages)
                        {
                            token.ThrowIfCancellationRequested();
                            string processedImg = ProcessImageForUpload(page);

                            string baseName = Path.GetFileNameWithoutExtension(f);
                            string driveName = $"{baseName}_page_{idx:000}.jpg";
                            await _drive.UploadOrReplaceFileAsync(folderId, processedImg, driveName);
                            idx++;
                        }
                    }
                    else
                    {
                        string processedPdf = ProcessPdfForUpload(f);
                        await _drive.UploadOrReplaceFileAsync(folderId, processedPdf, Path.GetFileName(f));
                    }
                }
            }
        }

        // =========================================================
        // ✅ Table: WA_PatientFolderState
        // نخزن فيه Signature علشان نعرف هل فولدر المريض اتغير ولا لا
        // =========================================================

        private async Task EnsurePatientFolderStateTableAsync(string cs, CancellationToken token)
        {
            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);
                string sql = @"
IF OBJECT_ID('dbo.WA_PatientFolderState', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WA_PatientFolderState
    (
        PatientId VARCHAR(13) NOT NULL PRIMARY KEY,
        LastSignature NVARCHAR(200) NULL,
        LastSyncedAt DATETIME NULL,
        LastError NVARCHAR(4000) NULL
    );
END";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.CommandTimeout = 60;
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private async Task<string> GetPrevSignatureAsync(string cs, string patientId, CancellationToken token)
        {
            await EnsurePatientFolderStateTableAsync(cs, token);

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);

                using (var cmd = new SqlCommand("SELECT ISNULL(LastSignature,'') FROM dbo.WA_PatientFolderState WHERE PatientId=@P", con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    var v = await cmd.ExecuteScalarAsync(token);
                    return v == null ? "" : Convert.ToString(v);
                }
            }
        }

        private async Task UpsertSignatureAsync(string cs, string patientId, string sig, CancellationToken token)
        {
            await EnsurePatientFolderStateTableAsync(cs, token);

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);

                string sql = @"
IF EXISTS (SELECT 1 FROM dbo.WA_PatientFolderState WHERE PatientId=@P)
    UPDATE dbo.WA_PatientFolderState
    SET LastSignature=@S, LastSyncedAt=GETDATE(), LastError=NULL
    WHERE PatientId=@P
ELSE
    INSERT INTO dbo.WA_PatientFolderState (PatientId, LastSignature, LastSyncedAt, LastError)
    VALUES (@P, @S, GETDATE(), NULL)";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    cmd.Parameters.AddWithValue("@S", sig ?? "");
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private async Task SaveSyncErrorAsync(string cs, string patientId, string err, CancellationToken token)
        {
            await EnsurePatientFolderStateTableAsync(cs, token);

            using (var con = new SqlConnection(cs))
            {
                await con.OpenAsync(token);

                string sql = @"
IF EXISTS (SELECT 1 FROM dbo.WA_PatientFolderState WHERE PatientId=@P)
    UPDATE dbo.WA_PatientFolderState SET LastError=@E WHERE PatientId=@P
ELSE
    INSERT INTO dbo.WA_PatientFolderState (PatientId, LastSignature, LastSyncedAt, LastError)
    VALUES (@P, NULL, NULL, @E)";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@P", patientId);
                    cmd.Parameters.AddWithValue("@E", (err ?? "").Length > 3900 ? (err ?? "").Substring(0, 3900) : (err ?? ""));
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        // =========================================================
        // ✅ SQL ConnectionString (نفس بتاع ResultsLinkQueueWorker)
        // =========================================================
        private string BuildSqlConnectionString()
        {
            if (_settings == null)
                throw new Exception("ResultsLinkSettings غير موجودة.");

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

            // ✅ الصحيح: BuildSqlConnectionStringOrThrow
            string baseIniPath = _settings.BaseIniPath;
            return BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(baseIniPath);
        }

        // =========================================================
        // ✅ Helpers
        // =========================================================
        private static bool IsAllowedExt(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            return e == ".pdf" || e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".bmp" || e == ".gif";
        }

        private static bool IsImageExt(string ext)
        {
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif";
        }

        private static void WaitForFileReady(string path)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 20000)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (fs.Length > 0) return;
                    }
                }
                catch { }
                Thread.Sleep(150);
            }
        }

        private static string ComputeFolderSignature(string folderPath)
        {
            var files = Directory.GetFiles(folderPath)
                .Where(IsAllowedExt)
                .Select(f =>
                {
                    var fi = new FileInfo(f);
                    return (fi.Name ?? "").ToLowerInvariant()
                           + "|" + fi.Length.ToString()
                           + "|" + fi.LastWriteTimeUtc.Ticks.ToString();
                })
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string joined = string.Join(";", files);

            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(joined);
                var hash = sha1.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        // =========================================================
        // ✅ Image/PDF Processing (Header/Footer/Watermark)
        // =========================================================

        private string ProcessImageForUpload(string imagePath)
        {
            string p1 = ApplyLetterheadToImage_NoUI(imagePath);
            string p2 = RemoveTrialWatermark_NoUI(p1);
            string p3 = AddWatermarkToImage_NoUI(p2);
            return p3;
        }

        private static string ApplyLetterheadToImage_NoUI(string path)
        {
            bool enabled = false;
            try { enabled = AppSettings.Default.EnableLetterhead; } catch { enabled = false; }
            if (!enabled) return path;

            string headerPath = null, footerPath = null;
            int headerOpacity = 100, footerOpacity = 100;
            int headerOffsetTop = 0, footerOffsetBottom = 0;

            try
            {
                headerPath = AppSettings.Default.HeaderImagePath;
                footerPath = AppSettings.Default.FooterImagePath;
                headerOpacity = AppSettings.Default.HeaderOpacity;
                footerOpacity = AppSettings.Default.FooterOpacity;
                headerOffsetTop = AppSettings.Default.HeaderOffsetTop;
                footerOffsetBottom = AppSettings.Default.FooterOffsetBottom;
            }
            catch { }

            string outDir = Path.Combine(Path.GetTempPath(), "WA_Letterhead");
            Directory.CreateDirectory(outDir);
            string newPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "_lh.jpg");

            using (Image original = Image.FromFile(path))
            using (Bitmap bmp = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(original, 0, 0, original.Width, original.Height);

                // Header
                if (!string.IsNullOrWhiteSpace(headerPath) && File.Exists(headerPath))
                {
                    using (Image header = Image.FromFile(headerPath))
                    {
                        DrawImageWithOpacity_FitWidth(g, header, bmp.Width, headerOffsetTop, headerOpacity);
                    }
                }

                // Footer
                if (!string.IsNullOrWhiteSpace(footerPath) && File.Exists(footerPath))
                {
                    using (Image footer = Image.FromFile(footerPath))
                    {
                        int footerHeight = (int)(footer.Height * (bmp.Width / (float)footer.Width));
                        int y = bmp.Height - footerHeight - Math.Max(0, footerOffsetBottom);
                        if (y < 0) y = 0;

                        DrawImageWithOpacity_FitWidth(g, footer, bmp.Width, y, footerOpacity);
                    }
                }

                bmp.Save(newPath, ImageFormat.Jpeg);
            }

            return newPath;
        }

        private static void DrawImageWithOpacity_FitWidth(Graphics g, Image img, int targetWidth, int y, int opacityPercent)
        {
            float alpha = Math.Max(0, Math.Min(100, opacityPercent)) / 100f;

            int h = (int)(img.Height * (targetWidth / (float)img.Width));
            if (h <= 0) h = 1;

            var matrix = new ColorMatrix { Matrix33 = alpha };
            using (var attr = new ImageAttributes())
            {
                attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(img,
                    new Rectangle(0, y, targetWidth, h),
                    0, 0, img.Width, img.Height,
                    GraphicsUnit.Pixel,
                    attr);
            }
        }

        private static string RemoveTrialWatermark_NoUI(string originalPath)
        {
            // لو عندك منطق إزالة Watermark تجريبية في MainForm
            // ابعتهولي وأنا أنقله حرفيًا هنا.
            return originalPath;
        }

        private static string AddWatermarkToImage_NoUI(string originalPath)
        {
            string watermarkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Watermark", "logo.png");
            if (!File.Exists(watermarkPath))
                return originalPath;

            int sizePercent = 18;
            int opacity = 20;
            string position = "أسفل يمين";
            int offsetRight = 20, offsetLeft = 0, offsetTop = 0, offsetBottom = 20;

            try
            {
                sizePercent = AppSettings.Default.WatermarkSizePercent;
                opacity = AppSettings.Default.WatermarkOpacity;
                position = AppSettings.Default.WatermarkPosition;
                offsetRight = AppSettings.Default.WatermarkOffsetRight;
                offsetLeft = AppSettings.Default.WatermarkOffsetLeft;
                offsetTop = AppSettings.Default.WatermarkOffsetTop;
                offsetBottom = AppSettings.Default.WatermarkOffsetBottom;
            }
            catch { }

            string outDir = Path.Combine(Path.GetTempPath(), "WA_Watermark");
            Directory.CreateDirectory(outDir);
            string newPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(originalPath) + "_wm.jpg");

            using (Image baseImage = Image.FromFile(originalPath))
            using (Image watermark = Image.FromFile(watermarkPath))
            using (Bitmap canvas = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);

                int targetWidth = (int)(baseImage.Width * (Math.Max(1, Math.Min(90, sizePercent)) / 100.0));
                int targetHeight = (int)(watermark.Height * (targetWidth / (float)watermark.Width));
                if (targetWidth <= 0) targetWidth = 1;
                if (targetHeight <= 0) targetHeight = 1;

                using (var resized = new Bitmap(watermark, new Size(targetWidth, targetHeight)))
                {
                    int x = (baseImage.Width - targetWidth) / 2;
                    int y = (baseImage.Height - targetHeight) / 2;

                    if (position == "أعلى يسار") { x = 0; y = 0; }
                    else if (position == "أعلى يمين") { x = baseImage.Width - targetWidth; y = 0; }
                    else if (position == "أسفل يسار") { x = 0; y = baseImage.Height - targetHeight; }
                    else if (position == "أسفل يمين") { x = baseImage.Width - targetWidth; y = baseImage.Height - targetHeight; }

                    x += (offsetRight - offsetLeft);
                    y += (offsetBottom - offsetTop);

                    if (x < 0) x = 0;
                    if (y < 0) y = 0;
                    if (x + targetWidth > baseImage.Width) x = baseImage.Width - targetWidth;
                    if (y + targetHeight > baseImage.Height) y = baseImage.Height - targetHeight;

                    var matrix = new ColorMatrix();
                    matrix.Matrix33 = Math.Max(0, Math.Min(100, opacity)) / 100f;

                    using (var attr = new ImageAttributes())
                    {
                        attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(resized,
                            new Rectangle(x, y, resized.Width, resized.Height),
                            0, 0, resized.Width, resized.Height,
                            GraphicsUnit.Pixel,
                            attr);
                    }
                }

                canvas.Save(newPath, ImageFormat.Jpeg);
            }

            return newPath;
        }

        // 1. ضيف الدالة دي عشان تجيب مسار Ghostscript المحفوظ
        private static string GetGhostscriptPath()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghostscript.path.txt");
                if (File.Exists(configPath))
                {
                    string path = File.ReadAllText(configPath).Trim();
                    if (File.Exists(path)) return path;
                }
            }
            catch { }
            return null;
        }

        // 2. دي الدالة الجديدة البديلة (تستخدم Ghostscript)
        private static List<string> ConvertPdfToJpeg_MultiPage_Ghostscript(string pdfPath)
        {
            var outputImages = new List<string>();

            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return outputImages;

            string gsExe = GetGhostscriptPath();
            if (string.IsNullOrEmpty(gsExe))
            {
                // لو ملقاش المسار، مفيش حاجة نقدر نعملها غير إننا نرجّع لسته فاضية أو نطلّع خطأ
                // بس عشان البرنامج ميعملش كراش، هنرجعه فاضي
                return outputImages;
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "WA_Upload_GS");
                Directory.CreateDirectory(tempDir);

                string baseName = Path.GetFileNameWithoutExtension(pdfPath);
                string outputPattern = Path.Combine(tempDir, baseName + "_page_%03d.jpg");

                // إعدادات التحويل
                string args = $"-dNOPAUSE -dBATCH -sDEVICE=jpeg -r200 -dJPEGQ=90 -q -sOutputFile=\"{outputPattern}\" \"{pdfPath}\"";

                // التعديل هنا: إضافة خصائص الإخفاء
                var psi = new ProcessStartInfo(gsExe, args)
                {
                    UseShellExecute = false,            // لازم تكون false عشان نقدر نتحكم في النافذة
                    CreateNoWindow = true,              // دي الأمر المباشر بعدم إنشاء نافذة
                    WindowStyle = ProcessWindowStyle.Hidden, // زيادة تأكيد لإخفاء النافذة
                    RedirectStandardOutput = true,      // كتم أي مخرجات نصية
                    RedirectStandardError = true        // كتم أي رسائل خطأ
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(30000);
                }

                // تجميع الملفات الناتجة
                var files = Directory.GetFiles(tempDir, baseName + "_page_*.jpg")
                                     .OrderBy(f => f)
                                     .ToList();

                outputImages.AddRange(files);
            }
            catch (Exception ex)
            {
                // ممكن نسجل الخطأ هنا لو حابب
            }

            return outputImages;
        }

        // =========================================================
        // ✅ دوال معالجة الـ PDF (Header/Footer/Watermark) للرفع
        // =========================================================

        private string ProcessPdfForUpload(string pdfPath)
        {
            string currentPath = pdfPath;

            try
            {
                // 1. تطبيق الهيدر والفوتر (لو الخيار مفعل في الإعدادات)
                if (AppSettings.Default.EnableLetterhead)
                {
                    currentPath = ApplyLetterheadToPdf_NoUI(currentPath);
                }

                // 2. تطبيق العلامة المائية (لو الصورة موجودة)
                currentPath = AddWatermarkToPdf_NoUI(currentPath);
            }
            catch (Exception ex)
            {
                // لو حصل خطأ في المعالجة، بنرجع الملف زي ما وصل لحد دلوقتي
            }

            return currentPath;
        }

        private static string ApplyLetterheadToPdf_NoUI(string originalPath)
        {
            string headerPath = AppSettings.Default.HeaderImagePath;
            string footerPath = AppSettings.Default.FooterImagePath;
            int headerOpacity = AppSettings.Default.HeaderOpacity;
            int footerOpacity = AppSettings.Default.FooterOpacity;
            int headerOffset = AppSettings.Default.HeaderOffsetTop;
            int footerOffset = AppSettings.Default.FooterOffsetBottom;

            if (!File.Exists(headerPath) && !File.Exists(footerPath))
                return originalPath;

            try
            {
                string outputPath = Path.Combine(Path.GetTempPath(), "WA_Upload_PDF_LH");
                Directory.CreateDirectory(outputPath);
                string finalPath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(originalPath) + "_lh.pdf");

                using (var reader = new iTextSharp.text.pdf.PdfReader(originalPath))
                using (var fs = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
                using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, fs))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        var pageSize = reader.GetPageSizeWithRotation(i);
                        float width = pageSize.Width;
                        float height = pageSize.Height;

                        var content = stamper.GetOverContent(i);

                        // تغطية الشريط الأحمر (اختياري)
                        CoverTrialLineInPdf(content, width);

                        // 1) Header
                        if (File.Exists(headerPath))
                        {
                            var header = iTextSharp.text.Image.GetInstance(headerPath);
                            float headerWidth = width;
                            float scale = headerWidth / header.Width;
                            float headerHeight = header.Height * scale;
                            float y = height - headerHeight - headerOffset;

                            header.ScaleToFit(headerWidth, headerHeight);
                            header.SetAbsolutePosition(0, y);

                            var gstate = new iTextSharp.text.pdf.PdfGState { FillOpacity = headerOpacity / 100f };
                            content.SaveState();
                            content.SetGState(gstate);
                            content.AddImage(header);
                            content.RestoreState();
                        }

                        // 2) Footer
                        if (File.Exists(footerPath))
                        {
                            var footer = iTextSharp.text.Image.GetInstance(footerPath);
                            float footerWidth = width;
                            float scale = footerWidth / footer.Width;
                            float footerHeight = footer.Height * scale;
                            float y = footerOffset;

                            footer.ScaleToFit(footerWidth, footerHeight);
                            footer.SetAbsolutePosition(0, y);

                            var gstate = new iTextSharp.text.pdf.PdfGState { FillOpacity = footerOpacity / 100f };
                            content.SaveState();
                            content.SetGState(gstate);
                            content.AddImage(footer);
                            content.RestoreState();
                        }
                    }
                }
                return finalPath;
            }
            catch
            {
                return originalPath;
            }
        }

        private static string AddWatermarkToPdf_NoUI(string originalPath)
        {
            string watermarkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Watermark", "logo.png");
            if (!File.Exists(watermarkPath)) return originalPath;

            try
            {
                int percent = AppSettings.Default.WatermarkSizePercent;
                string position = AppSettings.Default.WatermarkPosition;
                int offsetRight = AppSettings.Default.WatermarkOffsetRight;
                int offsetLeft = AppSettings.Default.WatermarkOffsetLeft;
                int offsetTop = AppSettings.Default.WatermarkOffsetTop;
                int offsetBottom = AppSettings.Default.WatermarkOffsetBottom;
                int opacity = AppSettings.Default.WatermarkOpacity;

                string outputPath = Path.Combine(Path.GetTempPath(), "WA_Upload_PDF_WM");
                Directory.CreateDirectory(outputPath);
                string finalPath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(originalPath) + "_wm.pdf");

                using (var reader = new iTextSharp.text.pdf.PdfReader(originalPath))
                using (var fs = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
                using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, fs))
                {
                    var watermarkImage = iTextSharp.text.Image.GetInstance(File.ReadAllBytes(watermarkPath));

                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        var pageSize = reader.GetPageSizeWithRotation(i);
                        float pageWidth = pageSize.Width;
                        float pageHeight = pageSize.Height;

                        float logoWidth = pageWidth * (percent / 100f);
                        float scale = logoWidth / watermarkImage.Width;
                        float logoHeight = watermarkImage.Height * scale;

                        watermarkImage.ScaleToFit(logoWidth, logoHeight);

                        float x = (pageWidth - logoWidth) / 2;
                        float y = (pageHeight - logoHeight) / 2;

                        switch (position)
                        {
                            case "أعلى يسار": x = 0; y = pageHeight - logoHeight; break;
                            case "أعلى يمين": x = pageWidth - logoWidth; y = pageHeight - logoHeight; break;
                            case "أسفل يسار": x = 0; y = 0; break;
                            case "أسفل يمين": x = pageWidth - logoWidth; y = 0; break;
                        }

                        x += offsetRight - offsetLeft;
                        y += offsetBottom - offsetTop;

                        watermarkImage.SetAbsolutePosition(x, y);

                        var content = stamper.GetOverContent(i);
                        var gstate = new iTextSharp.text.pdf.PdfGState { FillOpacity = opacity / 100f };

                        content.SaveState();
                        content.SetGState(gstate);
                        content.AddImage(watermarkImage);
                        content.RestoreState();
                    }
                }
                return finalPath;
            }
            catch
            {
                return originalPath;
            }
        }

        private static void CoverTrialLineInPdf(iTextSharp.text.pdf.PdfContentByte content, float pageWidth)
        {
            const float stripHeightPt = 22f;
            content.SaveState();
            content.SetColorFill(iTextSharp.text.BaseColor.WHITE);
            content.Rectangle(0, 0, pageWidth, stripHeightPt);
            content.Fill();
            content.RestoreState();
        }
    }
}
