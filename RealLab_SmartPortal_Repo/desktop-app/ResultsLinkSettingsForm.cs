using System;
using System.Data.SqlClient;
using System.IO;
using System.Windows.Forms;

namespace WhatsApp_Auto_Sender
{
    public partial class ResultsLinkSettingsForm : Form
    {
        private ResultsLinkSettings s;

        public ResultsLinkSettingsForm()
        {
            InitializeComponent();

            // RTL setup
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = false; // عشان المحاذاة اليدوية تضبط

            s = ResultsLinkSettings.Load();

            // Load values into UI
            chkEnabled.Checked = s.Enabled;
            chkSendLinkWhatsApp.Checked = s.SendLinkOnWhatsApp;
            chkAddQrToReceipt.Checked = s.AddQrCodeToReceipt;

            txtMsgPrefix.Text = s.WhatsAppMessagePrefix ?? "";
            txtReceiptCaption.Text = s.ReceiptQrCaption ?? "";

            txtExportFolder.Text = s.ExportWatchRootFolder ?? @"D:\PDF";
            numPoll.Value = Math.Max(1, s.QueuePollSeconds);

            lblRootFolderStatus.Text = string.IsNullOrWhiteSpace(s.GoogleRootFolderId)
                ? "Root Folder: غير مُجهز بعد"
                : "Root Folder: جاهز ✅ (Lab results)";

            // Manual SQL
            chkUseManualSql.Checked = s.UseManualSql;
            txtSqlServer.Text = s.SqlServerOrIp ?? "";
            txtSqlDb.Text = string.IsNullOrWhiteSpace(s.SqlDatabase) ? "Patients" : s.SqlDatabase;
            txtSqlUser.Text = string.IsNullOrWhiteSpace(s.SqlUser) ? "sa" : s.SqlUser;
            txtSqlPass.Text = s.SqlPassword ?? "";

            ToggleManualSqlEnabled();

            chkUseManualSql.CheckedChanged += (a, b) => ToggleManualSqlEnabled();
        }

        private void ToggleManualSqlEnabled()
        {
            bool en = chkUseManualSql.Checked;

            txtSqlServer.Enabled = en;
            txtSqlDb.Enabled = en;
            txtSqlUser.Enabled = en;
            txtSqlPass.Enabled = en;
        }

        private void SaveToSettingsObject()
        {
            s.Enabled = chkEnabled.Checked;
            s.SendLinkOnWhatsApp = chkSendLinkWhatsApp.Checked;
            s.AddQrCodeToReceipt = chkAddQrToReceipt.Checked;

            if (!chkUseManualSql.Checked)
            {
                string foundBase = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                if (!string.IsNullOrWhiteSpace(foundBase))
                    s.BaseIniPath = foundBase;
            }

            s.WhatsAppMessagePrefix = txtMsgPrefix.Text ?? "";
            s.ReceiptQrCaption = txtReceiptCaption.Text ?? "";

            s.UploadMode = 2; // Always Watch Folder
            s.ExportWatchRootFolder = (txtExportFolder.Text ?? "").Trim();
            s.QueuePollSeconds = (int)numPoll.Value;

            // Manual SQL
            s.UseManualSql = chkUseManualSql.Checked;
            s.SqlServerOrIp = (txtSqlServer.Text ?? "").Trim();
            s.SqlDatabase = (txtSqlDb.Text ?? "").Trim();
            s.SqlUser = (txtSqlUser.Text ?? "").Trim();
            s.SqlPassword = txtSqlPass.Text ?? "";
        }

        private string BuildSqlConnectionString()
        {
            // (1) Manual SQL
            if (chkUseManualSql.Checked)
            {
                string server = (txtSqlServer.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(server))
                    throw new Exception("من فضلك اكتب Server name أو IP في إعدادات SQL اليدوية.");

                string db = (txtSqlDb.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(db))
                    db = "Patients";

                string user = (txtSqlUser.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(user))
                    user = "sa";

                string pass = txtSqlPass.Text ?? "";

                var b = new SqlConnectionStringBuilder();
                b.DataSource = server;
                b.InitialCatalog = db;
                b.UserID = user;
                b.Password = pass;
                b.PersistSecurityInfo = true;
                b.ConnectTimeout = 10;
                b.Encrypt = false;
                b.TrustServerCertificate = true;

                return b.ToString();
            }

            // (2) BASE.ini fallback (Automatic)
            string basePath = BaseIniSqlConnectionBuilder.FindBaseIniPath();

            if (string.IsNullOrWhiteSpace(basePath))
                throw new Exception("لم يتم العثور على ملف BASE تلقائياً في المسارات المعروفة.");

            return BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(basePath, "sa", "12345678");
        }

        private void btnTestDb_Click(object sender, EventArgs e)
        {
            try
            {
                string cs = BuildSqlConnectionString();

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                }

                if (!chkUseManualSql.Checked)
                {
                    var builder = new SqlConnectionStringBuilder(cs);

                    txtSqlServer.Text = builder.DataSource;
                    txtSqlDb.Text = builder.InitialCatalog;
                    txtSqlUser.Text = builder.UserID;
                    txtSqlPass.Text = builder.Password;
                }

                MessageBox.Show(this,
                    "✅ اتصال DB جاهز وتم التعرف على السيرفر بنجاح:\n\n" + cs,
                    "DB OK",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "❌ فشل اختبار قاعدة البيانات:\n" + ex.Message,
                    "DB Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        private void btnInitDb_Click(object sender, EventArgs e)
        {
            try
            {
                SaveToSettingsObject();

                string cs = BuildSqlConnectionString();
                SqlResultsLinkBootstrapper.EnsureInstalled(cs);

                s.Save();

                MessageBox.Show(this,
                    "✅ تم تهيئة قاعدة البيانات (إنشاء الجداول + Trigger).",
                    "تم",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "❌ فشل تهيئة قاعدة البيانات:\n" + ex.Message,
                    "خطأ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        private async void btnGoogleLogin_Click(object sender, EventArgs e)
        {
            try
            {
                SaveToSettingsObject();

                var svc = new GoogleDriveResultsLinkService(s);
                await svc.EnsureAuthenticatedAsync();

                string rootId = await svc.EnsureLabResultsRootFolderAsync();
                s.GoogleRootFolderId = rootId;
                s.Save();

                lblRootFolderStatus.Text = "Root Folder: جاهز ✅ (Lab results)";

                MessageBox.Show(this,
                    "✅ تم تسجيل الدخول وتجهيز فولدر Lab results.",
                    "Google Drive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "❌ فشل تسجيل الدخول:\n" + ex.Message,
                    "Google Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(this,
                "هل أنت متأكد من استعادة جميع إعدادات لينك النتائج إلى الوضع الافتراضي؟\n" +
                "(سيتم مسح بيانات السيرفر اليدوية ومسار التصدير)",
                "تأكيد",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            if (confirm == DialogResult.Yes)
            {
                // القيم الافتراضية
                chkEnabled.Checked = true;
                chkSendLinkWhatsApp.Checked = true;
                chkAddQrToReceipt.Checked = true;

                txtMsgPrefix.Text = "📌 لإستلام النتائج أونلاين فور الإنتهاء، افتح اللينك التالي:";
                txtReceiptCaption.Text = "إدخل على الـ QR Code لإستلام النتائج أونلاين فور الإنتهاء";

                txtExportFolder.Text = @"D:\PDF";
                numPoll.Value = 2;

                chkUseManualSql.Checked = false;
                txtSqlServer.Text = "";
                txtSqlDb.Text = "Patients";
                txtSqlUser.Text = "sa";
                txtSqlPass.Text = "12345678";

                ToggleManualSqlEnabled();

                MessageBox.Show(this, "✅ تم استعادة الإعدادات الافتراضية.", "تم");
            }
        }

        private void btnUndoSql_Click(object sender, EventArgs e)
        {
            // 1. رسالة التأكيد (مع ضبط اتجاه النص عشان الإنجليزي ميبوظش العربي)
            string msg = "هل تريد الرجوع عن أي تغييرات تمت على قاعدة بيانات SQL ؟\n\n" +
                         "لن يتم حذف بيانات المرضى، ولكن سيتوقف الحفظ التلقائي للواتساب.";

            DialogResult result = MessageBox.Show(msg,
                                                  "Undo SQL Changes",
                                                  MessageBoxButtons.YesNo,
                                                  MessageBoxIcon.Warning,
                                                  MessageBoxDefaultButton.Button2,
                                                  MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // تحضير جملة الاتصال
                    string connectionString;
                    if (chkUseManualSql.Checked)
                    {
                        var builder = new SqlConnectionStringBuilder
                        {
                            DataSource = txtSqlServer.Text.Trim(),
                            InitialCatalog = txtSqlDb.Text.Trim(),
                            UserID = string.IsNullOrWhiteSpace(txtSqlUser.Text) ? "sa" : txtSqlUser.Text.Trim(),
                            Password = txtSqlPass.Text,
                            ConnectTimeout = 10,
                            IntegratedSecurity = false
                        };
                        connectionString = builder.ToString();
                    }
                    else
                    {
                        // نفس الكود اللي بتستخدمه لجلب الاتصال من ملف الـ INI
                        string baseIniPath = s.BaseIniPath;
                        if (string.IsNullOrWhiteSpace(baseIniPath) || !File.Exists(baseIniPath))
                        {
                            baseIniPath = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                        }
                        string user = string.IsNullOrWhiteSpace(txtSqlUser.Text) ? "sa" : txtSqlUser.Text.Trim();
                        string pass = txtSqlPass.Text;
                        connectionString = BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(baseIniPath, user, pass);
                    }

                    // 2. استدعاء دالة الحذف
                    SqlResultsLinkBootstrapper.Uninstall(connectionString);

                    MessageBox.Show("تم حذف التعديلات بنجاح.\nعادت قاعدة البيانات كما كانت.", "تمت العملية", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("حدث خطأ أثناء محاولة التراجع:\n" + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveToSettingsObject();
            s.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}