using System;
using System.IO;
using Newtonsoft.Json;

namespace WhatsApp_Auto_Sender
{
    public class ResultsLinkSettings
    {
        public bool Enabled { get; set; } = true;

        public bool SendLinkOnWhatsApp { get; set; } = true;

        // ✅ خاصية جديدة للتحكم في إضافة الـ QR للإيصال
        public bool AddQrCodeToReceipt { get; set; } = true;

        public string WhatsAppMessagePrefix { get; set; } =
            "📌 لإستلام النتائج أونلاين فور الإنتهاء، افتح اللينك التالي:";

        public string ReceiptQrCaption { get; set; } =
            "إدخل على الـ QR Code لإستلام النتائج أونلاين فور الإنتهاء";

        // ✅ تم تثبيت الوضع على 2 (Watch Folder)
        public int UploadMode { get; set; } = 2;

        public string ExportWatchRootFolder { get; set; } = @"D:\PDF";

        // ✅ المسار ثابت وسيتم جلبه تلقائياً
        public string BaseIniPath { get; set; } = "";

        public string GoogleRootFolderId { get; set; } = "";

        public int QueuePollSeconds { get; set; } = 2;

        // ======================================================
        // إعدادات SQL اليدوية
        // ======================================================
        public bool UseManualSql { get; set; } = false;

        public string SqlServerOrIp { get; set; } = "";
        public string SqlDatabase { get; set; } = "Patients";
        public string SqlUser { get; set; } = "sa";
        public string SqlPassword { get; set; } = "12345678";

        private static string GetSettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhatsAppAutoSender");

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "results_link_settings.json");
        }

        public static ResultsLinkSettings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path))
                    return new ResultsLinkSettings();

                string json = File.ReadAllText(path);
                var s = JsonConvert.DeserializeObject<ResultsLinkSettings>(json);
                return s ?? new ResultsLinkSettings();
            }
            catch
            {
                return new ResultsLinkSettings();
            }
        }

        public void Save()
        {
            string path = GetSettingsPath();
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}