using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.codec;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using PdfiumViewer;
using WhatsApp_Auto_Sender;
using WindowsInput;
using WindowsInput.Native;
using ZXing;
using ZXing.Common;

namespace WhatsAppAutoSender
{
    public partial class MainForm : Form
    {
        private IWebDriver driver;
        // متغير الطريقة 3
        private WppBrowserForm webViewForm;
        private ToolStripMenuItem sendMethod3; // عنصر القائمة الجديد
        private readonly string chromeProfile = @"C:\WhatsAppChromeSession";
        private IntPtr chromeWindowHandle = IntPtr.Zero;
        private string initialPhone;
        private string initialFolder;
        private TextBox txtCountryCode;
        private int selectedSendMethod = 2; // ✅ الآن WPPConnect هي الافتراضية
        private string currentPhoneNumber = "";
        private ToolStripMenuItem sendMethod1;
        private ToolStripMenuItem sendMethod2;
        private ToolStripMenuItem convertPdfToImageItem; // ✅ خيار تحويل PDF لصورة
        private ToolStripMenuItem convertPdfToImageLinkItem;
        private ToolStripMenuItem selectGhostscriptItem;
        private const string GhostscriptPathConfigFile = "ghostscript.path.txt";
        // ✅ خيار إغلاق فولدرات D:\PDF\ID تلقائيًا
        private bool autoClosePdfFoldersEnabled;
        private ToolStripMenuItem autoClosePdfFoldersItem;
        // ✅ متغيّرات الأيقونة اللي تحت (System Tray)
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayClosePdfMenuItem;
        private ToolStripMenuItem trayConvertPdfMenuItem;

        private ToolStripMenuItem traySendReceiptsMenuItem;
        private ToolStripMenuItem trayPrintReceiptsMenuItem;

        // عناصر منيو الإعدادات الخاصة بالإيصالات
        private ToolStripMenuItem sendReceiptsToWhatsAppMenuItem;
        private ToolStripMenuItem printReceiptsOnPrinterMenuItem;

        private MenuStrip topMenuStrip;
        private ToolStripMenuItem settingsMenu;

        // ===== كوبرى إرسال الإيصالات (Receipt Bridge) =====
        private FileSystemWatcher receiptWatcher;
        private readonly object receiptLock = new object();
        private readonly HashSet<string> processedReceiptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ✅ خيار: فتح البرنامج تلقائياً مع بدء تشغيل الويندوز
        private ToolStripMenuItem autoStartWithWindowsMenuItem;
        private const string AutoStartRegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartRegistryValueName = "WhatsAppAutoSender";

        private ToolStripMenuItem resultsLinkSettingsMenuItem;
        private ToolStripMenuItem sendResultsLinkOnWhatsAppMenuItem;

        private ResultsLinkQueueWorker resultsLinkWorker;
        private ResultsLinkSettings resultsLinkSettingsCache;

        // ✅ آخر إيصال اتعالج (علشان نستخدمه في الواتساب)
        private string _lastReceiptPatientId = null;
        private string _lastReceiptFolderUrl = null;
        private WhatsApp_Auto_Sender.ResultsUploadQueueWorker resultsUploadWorker;



        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
    string section,
    string key,
    string defaultValue,
    StringBuilder returnValue,
    int size,
    string filePath);


        // صورة الإيصال الجاهزة للطباعة
        private Image receiptPrintImage;
        private string receiptImagePathForPrint;

        // ✅ تعريفات WinEventHook
        private IntPtr winEventHookHandle = IntPtr.Zero;
        private WinEventDelegate winEventDelegate;

        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const int OBJID_WINDOW = 0;
        private const int WM_CLOSE = 0x0010;
        private bool _trayHintShown = false;

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwflags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


        public MainForm() : this(null, null) { }

        public MainForm(string phone, string folder)
        {
            // ✅ تحقق من إصدار الويندوز
            if (Environment.OSVersion.Version.Major < 6)
            {
                MessageBox.Show(
                    "❌ هذا البرنامج يتطلب Windows 7 أو أحدث.\nيرجى الترقية إلى نظام أحدث.",
                    "نظام غير مدعوم",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Environment.Exit(0);
            }

            InitializeComponent();
            if (tooltip == null) tooltip = new ToolTip();

            // ربط الرسالة بخانة كود الدولة
            tooltip.SetToolTip(txtCountryCode, "كود الدولة الخاص بإرسال الإيصالات و بدء الشات");
            InitializeWhatsAppProfile();
            CenterTextVertically(txtPhone);
            CenterTextVertically(txtCountryCode);
            resultsLinkSettingsCache = ResultsLinkSettings.Load();
            EnsureResultsLinkWorker();
            EnsureResultsUploadWorker();

            txtPhone.Resize += (s, e) => CenterTextVertically(txtPhone);
            txtCountryCode.Resize += (s, e) => CenterTextVertically(txtCountryCode);

            txtPhone.FontChanged += (s, e) => CenterTextVertically(txtPhone);
            txtCountryCode.FontChanged += (s, e) => CenterTextVertically(txtCountryCode);

            initialPhone = phone;
            initialFolder = folder;
            autoClosePdfFoldersEnabled = Properties.Settings.Default.AutoClosePdfFolders;

            // إنشاء شريط القائمة العلوي
            topMenuStrip = new MenuStrip();
            settingsMenu = new ToolStripMenuItem("الإعدادات");

            topMenuStrip.RightToLeft = RightToLeft.Yes;
            settingsMenu.RightToLeft = RightToLeft.Yes;

            topMenuStrip.Items.Add(settingsMenu);
            this.MainMenuStrip = topMenuStrip;

            // ✅✅✅ هنا التعديل المهم عشان القائمة تطلع فوق خالص ✅✅✅
            this.Controls.Add(topMenuStrip);
            topMenuStrip.Dock = DockStyle.Top;
            topMenuStrip.SendToBack(); // دي بتخليها تاخد "أول" مكان في السقف قبل أي بانل تاني
            // ✅✅✅✅✅✅

            // 1. إعدادات العلامة المائية
            ToolStripMenuItem watermarkItem = new ToolStripMenuItem("إعدادات العلامة المائية");
            watermarkItem.Click += new EventHandler(this.btnWatermarkSettings_Click);

            // 2. إعدادات Header/Footer
            ToolStripMenuItem headerFooterItem = new ToolStripMenuItem("إعدادات ‎Header/Footer‎");
            headerFooterItem.Click += new EventHandler(this.btnApplyLetterhead_Click);

            // 3. إعدادات كوبرى الإيصالات
            ToolStripMenuItem receiptBridgeItem = new ToolStripMenuItem("إعدادات إرسال الإيصالات");
            receiptBridgeItem.Click += (s, e) =>
            {
                using (var f = new ReceiptBridgeSettingsForm())
                {
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        InitializeReceiptBridgeFromSettings();
                    }
                }
            };

            ToolStripMenuItem sendReceiptsToWhatsAppMenuItem = new ToolStripMenuItem("إرسال الإيصالات على واتس آب");
            sendReceiptsToWhatsAppMenuItem.CheckOnClick = true;
            sendReceiptsToWhatsAppMenuItem.Checked = Properties.Settings.Default.ReceiptBridge_SendToWhatsApp;
            sendReceiptsToWhatsAppMenuItem.Click += (s, e) =>
            {
                bool enabled = sendReceiptsToWhatsAppMenuItem.Checked;
                ToggleSendReceiptsToWhatsApp(enabled, fromTray: false);
            };

            ToolStripMenuItem printReceiptsOnPrinterMenuItem = new ToolStripMenuItem("طباعة الإيصالات على البرنتر");
            printReceiptsOnPrinterMenuItem.CheckOnClick = true;
            printReceiptsOnPrinterMenuItem.Checked = Properties.Settings.Default.ReceiptBridge_PrintOnPrinter;
            printReceiptsOnPrinterMenuItem.Click += (s, e) =>
            {
                bool enabled = printReceiptsOnPrinterMenuItem.Checked;
                TogglePrintReceiptsOnPrinter(enabled, fromTray: false);
            };

            // 4. إعدادات لينك النتائج (والخيار الجديد تحته)
            resultsLinkSettingsMenuItem = new ToolStripMenuItem("إعدادات لينك النتائج");
            resultsLinkSettingsMenuItem.Click += (s, e) =>
            {
                using (var f = new ResultsLinkSettingsForm())
                {
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        resultsLinkSettingsCache = ResultsLinkSettings.Load();
                        EnsureResultsLinkWorker();
                        EnsureResultsUploadWorker();
                    }
                }
            };

            sendResultsLinkOnWhatsAppMenuItem = new ToolStripMenuItem("إرسال لينك النتائج على واتس آب");
            sendResultsLinkOnWhatsAppMenuItem.CheckOnClick = true;
            sendResultsLinkOnWhatsAppMenuItem.Checked = ResultsLinkSettings.Load().SendLinkOnWhatsApp;
            sendResultsLinkOnWhatsAppMenuItem.Click += (s, e) =>
            {
                var st = ResultsLinkSettings.Load();
                st.SendLinkOnWhatsApp = sendResultsLinkOnWhatsAppMenuItem.Checked;
                st.Save();
                Log(st.SendLinkOnWhatsApp ? "🟢 تم تفعيل إرسال لينك النتائج." : "⚪ تم إيقاف إرسال لينك النتائج.");
            };

            // ✅ خيار: تحويل PDF لصور (لينك النتائج)
            convertPdfToImageLinkItem = new ToolStripMenuItem("تحويل PDF إلى صورة (JPEG) لينك النتائج");
            convertPdfToImageLinkItem.CheckOnClick = true;
            convertPdfToImageLinkItem.Checked = Properties.Settings.Default.ConvertPdfToImage_Link;
            convertPdfToImageLinkItem.Click += (s, e) =>
            {
                bool enabled = convertPdfToImageLinkItem.Checked;
                Properties.Settings.Default.ConvertPdfToImage_Link = enabled;
                Properties.Settings.Default.Save();
                Log(enabled
                    ? "🖼️ (لينك النتائج) سيتم تحويل PDF إلى صور قبل الرفع."
                    : "📄 (لينك النتائج) سيتم رفع PDF كما هو.");
            };


            // 5. خيارات عامة (إغلاق فولدرات)
            autoClosePdfFoldersItem = new ToolStripMenuItem("إغلاق فولدر المريض تلقائياً بعد التصدير");
            autoClosePdfFoldersItem.CheckOnClick = true;
            autoClosePdfFoldersItem.Checked = autoClosePdfFoldersEnabled;
            autoClosePdfFoldersItem.Click += (s, e) =>
            {
                autoClosePdfFoldersEnabled = autoClosePdfFoldersItem.Checked;
                if (trayClosePdfMenuItem != null) trayClosePdfMenuItem.Checked = autoClosePdfFoldersEnabled;
                Properties.Settings.Default.AutoClosePdfFolders = autoClosePdfFoldersEnabled;
                Properties.Settings.Default.Save();
                if (autoClosePdfFoldersEnabled) EnableAutoCloseHook(); else DisableAutoCloseHook();
                Log(autoClosePdfFoldersEnabled ? "🟢 سيتم إغلاق الفولدرات." : "⚪ تم إيقاف إغلاق الفولدرات.");
            };

            // 6. تحويل PDF للواتس آب
            convertPdfToImageItem = new ToolStripMenuItem("تحويل PDF إلى صورة (JPEG) واتس آب");
            convertPdfToImageItem.CheckOnClick = true;
            convertPdfToImageItem.Checked = Properties.Settings.Default.ConvertPdfToImage;
            convertPdfToImageItem.Click += (s, e) =>
            {
                bool enabled = convertPdfToImageItem.Checked;
                if (trayConvertPdfMenuItem != null) trayConvertPdfMenuItem.Checked = enabled;
                Properties.Settings.Default.ConvertPdfToImage = enabled;
                Properties.Settings.Default.Save();
                Log(enabled ? "🖼️ (واتس آب) سيتم تحويل PDF إلى صور." : "📄 (واتس آب) سيتم إرسال PDF أصلي.");
            };

            // 7. Ghostscript
            selectGhostscriptItem = new ToolStripMenuItem("تحديد مسار ‎Ghostscript‎...");
            selectGhostscriptItem.Click += selectGhostscriptItem_Click;

            // 8. طرق الإرسال
            // ✅ استرجاع الطريقة المحفوظة (أو الافتراضي 3 لو مفيش حفظ)
            try
            {
                selectedSendMethod = Properties.Settings.Default.SendMethod;
                if (selectedSendMethod == 0) selectedSendMethod = 3; // الوضع الافتراضي الجديد
            }
            catch
            {
                selectedSendMethod = 3;
            }

            // تعريف العناصر
            sendMethod1 = new ToolStripMenuItem("طريقة إرسال 1 - ‎Via Link‎");
            sendMethod2 = new ToolStripMenuItem("طريقة إرسال 2 - ‎Direct‎");
            sendMethod3 = new ToolStripMenuItem("طريقة 3 - Pro"); // ✅ الاسم الجديد
            sendMethod3.Click += (sender, e) =>
            {
                // إمسح أي كود كان مكتوب هنا وحط السطر ده بس:
                SwitchToMethod3();

                // تحديث علامات الصح (اختياري لو عندك دالة UpdateTrayMenu)
                // UpdateTrayMenu(); 
            };

            // ضبط العلامة (Check) بناءً على ما تم حفظه
            sendMethod1.Checked = (selectedSendMethod == 1);
            sendMethod2.Checked = (selectedSendMethod == 2);
            sendMethod3.Checked = (selectedSendMethod == 3);

            // ✅ إضافة الأحداث (Events) مع كود الحفظ والتحكم في المتصفح
            sendMethod1.Click += (s, e) =>
            {
                selectedSendMethod = 1;
                Properties.Settings.Default.SendMethod = 1;
                Properties.Settings.Default.Save(); // حفظ الاختيار

                sendMethod1.Checked = true;
                sendMethod2.Checked = false;
                sendMethod3.Checked = false;

                ManageWebViewState(); // 🛑 إغلاق المتصفح الخلفي
                Log("📤 تم اختيار: طريقة 1");
            };

            sendMethod2.Click += (s, e) =>
            {
                selectedSendMethod = 2;
                Properties.Settings.Default.SendMethod = 2;
                Properties.Settings.Default.Save(); // حفظ الاختيار

                sendMethod1.Checked = false;
                sendMethod2.Checked = true;
                sendMethod3.Checked = false;

                ManageWebViewState(); // 🛑 إغلاق المتصفح الخلفي
                Log("📤 تم اختيار: طريقة 2");
            };

            sendMethod3.Click += (s, e) =>
            {
                selectedSendMethod = 3;
                Properties.Settings.Default.SendMethod = 3;
                Properties.Settings.Default.Save(); // حفظ الاختيار

                sendMethod1.Checked = false;
                sendMethod2.Checked = false;
                sendMethod3.Checked = true;

                ManageWebViewState(); // 🚀 تشغيل المتصفح الخلفي
                Log("🚀 تم اختيار: طريقة 3 (Pro)");
            };

            // ✅ استدعاء الدالة لضبط حالة المتصفح عند بدء التشغيل
            // (سيتم تشغيله فقط إذا كانت الطريقة 3 هي المحفوظة)
            ManageWebViewState();

            // 9. Startup
            autoStartWithWindowsMenuItem = new ToolStripMenuItem("فتح البرنامج تلقائياً مع فتح الويندوز");
            autoStartWithWindowsMenuItem.CheckOnClick = true;
            autoStartWithWindowsMenuItem.Checked = IsAutoStartEnabled();
            autoStartWithWindowsMenuItem.Click += (s, e) =>
            {
                bool desired = autoStartWithWindowsMenuItem.Checked;
                try { SetAutoStartEnabled(desired); Log(desired ? "🟢 تفعيل Startup" : "⚪ إيقاف Startup"); }
                catch { autoStartWithWindowsMenuItem.Checked = !desired; }
            };

            // ======================
            // بناء القائمة (الترتيب)
            // ======================
            settingsMenu.DropDownItems.Add(watermarkItem);
            settingsMenu.DropDownItems.Add(headerFooterItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            settingsMenu.DropDownItems.Add(receiptBridgeItem);
            settingsMenu.DropDownItems.Add(sendReceiptsToWhatsAppMenuItem);
            settingsMenu.DropDownItems.Add(printReceiptsOnPrinterMenuItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            // ✅ مجموعة لينك النتائج
            settingsMenu.DropDownItems.Add(resultsLinkSettingsMenuItem);
            settingsMenu.DropDownItems.Add(sendResultsLinkOnWhatsAppMenuItem);
            settingsMenu.DropDownItems.Add(convertPdfToImageLinkItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            settingsMenu.DropDownItems.Add(autoClosePdfFoldersItem);

            // ✅ خيارات الواتس آب
            settingsMenu.DropDownItems.Add(convertPdfToImageItem);
            settingsMenu.DropDownItems.Add(selectGhostscriptItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            // ✅✅✅ الترتيب الجديد: 1 ثم 2 ثم 3 ورا بعض ✅✅✅
            settingsMenu.DropDownItems.Add(sendMethod1);
            settingsMenu.DropDownItems.Add(sendMethod2);
            settingsMenu.DropDownItems.Add(sendMethod3);

            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(autoStartWithWindowsMenuItem);

            // التنسيق النهائي
            topMenuStrip.BackColor = Color.White;
            topMenuStrip.Font = new Font("Segoe UI", 9F);
            topMenuStrip.RenderMode = ToolStripRenderMode.System;
            settingsMenu.DropDownOpening += (s, e) => ApplyToolStripTextColor(settingsMenu, chkDarkMode.Checked);

            chkDarkMode.Checked = Properties.Settings.Default.DarkMode;

            if (autoClosePdfFoldersEnabled) EnableAutoCloseHook();
            StartPipeServer();
            InitializeReceiptBridgeFromSettings();
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKeyPath, writable: false))
                {
                    string val = key?.GetValue(AutoStartRegistryValueName) as string;
                    if (string.IsNullOrWhiteSpace(val))
                        return false;

                    string exe = Application.ExecutablePath;
                    return val.IndexOf(exe, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void EnsureResultsLinkWorker()
        {
            try
            {
                if (resultsLinkSettingsCache == null)
                    resultsLinkSettingsCache = ResultsLinkSettings.Load();

                // ✅ خلي Poll = 1 ثانية كحد أدنى (عشان اللينك يجهز قبل الطباعة بدون ما نعمل انتظار)
                if (resultsLinkSettingsCache.QueuePollSeconds < 1)
                {
                    resultsLinkSettingsCache.QueuePollSeconds = 1;
                    try { resultsLinkSettingsCache.Save(); } catch { }
                }

                if (resultsLinkWorker != null)
                    resultsLinkWorker.Stop();

                // BASE.ini auto-detect لو مش موجود
                if (string.IsNullOrWhiteSpace(resultsLinkSettingsCache.BaseIniPath) ||
                    !System.IO.File.Exists(resultsLinkSettingsCache.BaseIniPath))
                {
                    string found = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        resultsLinkSettingsCache.BaseIniPath = found;
                        resultsLinkSettingsCache.Save();
                    }
                }

                resultsLinkWorker = new WhatsApp_Auto_Sender.ResultsLinkQueueWorker(resultsLinkSettingsCache);
                resultsLinkWorker.Start();

                // ✅ اول ما نبدأ صحّي العامل فورًا
                resultsLinkWorker.WakeUpNow();

                Log("✅ لينك النتائج: تم تشغيل Queue Worker (Poll=1s + WakeUp).");
            }
            catch (Exception ex)
            {
                Log("❌ لينك النتائج: فشل تشغيل العامل الخلفي: " + ex.Message);
            }
        }

        private void EnsureResultsUploadWorker()
        {
            try
            {
                if (resultsLinkSettingsCache == null)
                    resultsLinkSettingsCache = ResultsLinkSettings.Load();

                if (resultsUploadWorker != null)
                    resultsUploadWorker.Stop();

                // ✅ شغّل عامل الرفع
                resultsUploadWorker = new WhatsApp_Auto_Sender.ResultsUploadQueueWorker(resultsLinkSettingsCache);
                resultsUploadWorker.Start();

                Log("🟢 ResultsUploadQueueWorker شغّال.");
            }
            catch (Exception ex)
            {
                Log("⚠️ فشل تشغيل ResultsUploadQueueWorker: " + ex.Message);
            }
        }


        private void SetAutoStartEnabled(bool enabled)
        {
            using (RegistryKey key =
                Registry.CurrentUser.OpenSubKey(AutoStartRegistryKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(AutoStartRegistryKeyPath))
            {
                if (key == null)
                    throw new Exception("لا يمكن فتح/إنشاء مفتاح Startup في Registry.");

                if (enabled)
                {
                    // نخلي المسار بين "" عشان لو فيه مسافات
                    string exe = Application.ExecutablePath;
                    string value = "\"" + exe + "\"";
                    key.SetValue(AutoStartRegistryValueName, value, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(AutoStartRegistryValueName, throwOnMissingValue: false);
                }
            }
        }

        private bool IsWindows7()
        {
            // Windows 7 = Version 6.1
            Version v = Environment.OSVersion.Version;
            return (v.Major == 6 && v.Minor == 1);
        }

        private void ApplyToolStripTextColor(ToolStripMenuItem root, bool isDark)
        {
            Color fg = isDark ? Color.FromArgb(200, 205, 210) : Color.FromArgb(30, 35, 40);

            foreach (ToolStripItem it in root.DropDownItems)
            {
                it.ForeColor = fg;

                if (it is ToolStripMenuItem mi && mi.HasDropDownItems)
                    ApplyToolStripTextColor(mi, isDark);
            }
        }

        private void selectGhostscriptItem_Click(object sender, EventArgs e)
        {
            string msg =
                "هذا الخيار مخصّص للأجهزة التى تم تثبيت برنامج \u200EGhostscript\u200E عليها (مثل Windows 7 أو غيره).\n\n" +
                "قبل استخدامه تأكد من الآتي:\n" +
                "1) تم تثبيت برنامج \u200EGhostscript\u200E (نسخة 32 أو 64 بت) على الجهاز.\n" +
                "2) عادةً يتم تثبيته داخل مجلد \u200EProgram Files\u200E في مسار مثل:\n" +
                @"   C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe أو gswin32c.exe." + "\n\n" +
                "بعد الضغط على (موافق) اختر ملف التنفيذ الخاص بـ \u200EGhostscript\u200E من مجلد التثبيت، " +
                "ولا تُحدّد أي ملف آخر خارج مجلد \u200EProgram Files\u200E.";

            // رسالة RTL منسّقة من اليمين للشمال
            MessageBox.Show(
                this,
                msg,
                "تحديد مسار Ghostscript",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "حدد ملف Ghostscript EXE";
                ofd.Filter = "Ghostscript Executable|gswin64*.exe;gswin32*.exe|All Files|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string exePath = ofd.FileName;

                    string configPath = Path.Combine(Application.StartupPath, GhostscriptPathConfigFile);
                    File.WriteAllText(configPath, exePath, Encoding.UTF8);

                    Log("✔ تم حفظ مسار Ghostscript: " + exePath);
                    MessageBox.Show(
                        this,
                        "تم اختيار مسار Ghostscript بنجاح.",
                        "تم",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                }
            }
        }


        private void InitializeWhatsAppProfile()
        {
            try
            {
                if (!Directory.Exists(chromeProfile))
                    Directory.CreateDirectory(chromeProfile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في إعداد الملف الشخصي: {ex.Message}");
            }
        }

        private async void btnOpenWhatsApp_Click(object sender, EventArgs e)
        {
            // ✅ لو مختارين الطريقة 3 -> نظهر الفورم المخفي
            if (selectedSendMethod == 3)
            {
                ManageWebViewState();

                if (webViewForm != null && !webViewForm.IsDisposed)
                {
                    webViewForm.Show();
                    webViewForm.BringToFront();

                    // ✅ نفس التعديل هنا أيضاً
                    if (webViewForm.WindowState == FormWindowState.Minimized)
                    {
                        if (Properties.Settings.Default.BrowserWindowState == FormWindowState.Maximized)
                        {
                            webViewForm.WindowState = FormWindowState.Maximized;
                        }
                        else
                        {
                            webViewForm.WindowState = FormWindowState.Normal;
                        }
                    }

                    Log("🖥️ تم إظهار متصفح WebView2 (Pro)");
                }
                return;
            }

            try
            {
                await EnsureDriverRunningAsync();
                Log("تم فتح واتس آب بنجاح (Selenium)");
                // ... (باقي كودك القديم هنا)
                if (!string.IsNullOrEmpty(initialPhone))
                {
                    await OpenChatAsync(initialPhone);
                    // ...
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح واتساب: {ex.Message}");
            }
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string phone = txtPhone.Text.Trim();
            string code = txtCountryCode.Text.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                MessageBox.Show("الرجاء إدخال رقم الهاتف");
                return;
            }

            if (string.IsNullOrEmpty(code) || !code.All(char.IsDigit))
            {
                MessageBox.Show("الرجاء إدخال كود دولة صالح");
                return;
            }

            // إزالة الصفر من بداية الرقم
            if (phone.StartsWith("0"))
                phone = phone.Substring(1);

            string fullNumber = code + phone;
            string jid = fullNumber + "@c.us";

            try
            {
                // =========================
                // ✅ الطريقة 3: WebView2 + WPP (زي الطريقة 2 بالظبط - بدون Reload/بدون Fallback)
                // =========================
                if (selectedSendMethod == 3)
                {
                    // شغّل/جهّز المتصفح الخلفي لو مش شغال
                    ManageWebViewState();
                    BringWebViewToFront();

                    if (webViewForm == null || webViewForm.IsDisposed)
                    {
                        MessageBox.Show("متصفح WebView2 غير جاهز. اضغط (فتح واتساب) مرة واحدة وسجّل دخولك.");
                        return;
                    }

                    // مهم جداً عشان إرسال الملفات (Method 3) بيستخدم currentPhoneNumber
                    currentPhoneNumber = fullNumber;

                    bool ready = await EnsureWebViewReadyAsync(30000);
                    if (!ready)
                    {
                        MessageBox.Show("WebView2 لم يجهز في الوقت المحدد. جرّب تفتح واتساب وتستنى تحميله.");
                        return;
                    }

                    // اختياري: لو عايز المتصفح يفضل في الخلفية سيب السطور دي مقفولة
                    //webViewForm.Show();
                    //webViewForm.BringToFront();
                    //if (webViewForm.WindowState == FormWindowState.Minimized)
                    //    webViewForm.WindowState = FormWindowState.Normal;

                    // ✅ إرسال WPP فقط (نفس الطريقة 2 بالظبط) - بدون فتح شات/بدون لينك
                    string sendRes = await webViewForm.SendTextWppAsync(jid, "🔬");
                    Log($"✅ (WebView2/WPP) SendText => {sendRes}");

                    if (sendRes == null || sendRes.Contains("WPP_NOT_READY"))
                    {
                        MessageBox.Show("WPP مش جاهز داخل WebView2. افتح واتساب في Method 3 وتأكد إنك عامل Login (QR) وسيبه يحمل شوية.");
                    }

                    return;
                }

                // =========================
                // ✅ الطريقة 1 و 2: Selenium
                // =========================
                await EnsureDriverRunningAsync();
                BringChromeToFront();
                await Task.Delay(500);

                if (selectedSendMethod == 2)
                {
                    // ✅ الطريقة 2: WPPConnect – إرسال إيموجي فقط
                    string js = $@"
(async () => {{
    if (!window.WPP || !WPP.chat) {{
        console.warn('❌ غير جاهز');
        return;
    }}
    try {{
        await WPP.chat.sendTextMessage('{jid}', '🔬', {{ createChat: true }});
        console.log('✅ تم إرسال الإيموجي ');
    }} catch (err) {{
        console.error('❌ فشل في الإرسال:', err);
    }}
}})();";

                    ((IJavaScriptExecutor)driver).ExecuteScript(js);
                    Log($"✅ تم {jid}");
                }
                else
                {
                    // ✅ الطريقة 1: التقليدية – فتح رابط WhatsApp فقط
                    string waUrl = $"https://web.whatsapp.com/send?phone={fullNumber}";
                    driver.Navigate().GoToUrl(waUrl);
                    Log($"🌐 تم: {waUrl}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في التنفيذ:\n\n" + ex.ToString());
            }
        }


        // ✅ دالة جديدة لإدارة حالة المتصفح الخلفي (فتح/إغلاق) حسب الطريقة المختارة
        private void ManageWebViewState()
        {
            if (selectedSendMethod == 3)
            {
                // لو الطريقة 3: نتأكد إن المتصفح شغال، ولو مش شغال نشغله
                if (webViewForm == null || webViewForm.IsDisposed)
                {
                    try
                    {
                        bool notify = Properties.Settings.Default.EnableNotifications;

                        webViewForm = new WppBrowserForm(notify, Log);

                        // ✅ تحسين: جعل النافذة شفافة تماماً لمنع الوميض عند التحميل
                        webViewForm.Opacity = 0;

                        // الترتيب ده مهم عشان يشتغل في الخلفية ويحمل الإعدادات (OnLoad)
                        webViewForm.Show();
                        webViewForm.Hide();

                        // ✅ استعادة الشفافية عشان لما تظهرها بعدين تكون باينة
                        webViewForm.Opacity = 1;

                        Log("🚀 تم تشغيل خدمة WebView2 (Pro) في الخلفية.");
                    }
                    catch (Exception ex)
                    {
                        Log("❌ فشل بدء WebView2: " + ex.Message);
                    }
                }
            }
            else
            {
                // لو أي طريقة تانية: نقفل المتصفح عشان نوفر موارد الجهاز
                if (webViewForm != null && !webViewForm.IsDisposed)
                {
                    // ✅ استخدام الدالة الآمنة للحفظ قبل الإغلاق
                    _ = webViewForm.ShutdownPersistSessionAsync();
                    webViewForm = null;
                    Log("🛑 تم إيقاف خدمة WebView2 لأنك اخترت طريقة أخرى.");
                }
            }
        }


        private void BringWebViewToFront()
        {
            if (webViewForm == null || webViewForm.IsDisposed) return;

            if (!webViewForm.Visible) webViewForm.Show();

            // إضافة صغيرة: إجبار الرسم فوراً قبل تغيير الحالة
            if (IsWindows7()) webViewForm.Refresh();

            if (webViewForm.WindowState == FormWindowState.Minimized)
            {
                if (Properties.Settings.Default.BrowserWindowState == FormWindowState.Maximized)
                {
                    webViewForm.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    webViewForm.WindowState = FormWindowState.Normal;
                }
            }

            webViewForm.Activate();
            webViewForm.BringToFront();
        }


        private async Task<bool> EnsureWebViewReadyAsync(int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (webViewForm != null && !webViewForm.IsDisposed && webViewForm.IsReady)
                    return true;

                await Task.Delay(200);
                waited += 200;
            }
            return false;
        }


        private void StartPipeServer()
        {
            Thread pipeThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var pipeServer = new System.IO.Pipes.NamedPipeServerStream("WhatsAppPipe", System.IO.Pipes.PipeDirection.In))
                        {
                            pipeServer.WaitForConnection();
                            using (var reader = new StreamReader(pipeServer))
                            {
                                string data = reader.ReadLine();
                                if (!string.IsNullOrEmpty(data))
                                {
                                    string[] parts = data.Split('|');
                                    if (parts.Length == 2)
                                    {
                                        string rawPhone = parts[0];
                                        string fileListRaw = parts[1];

                                        Thread actionThread = new Thread(() =>
                                        {
                                            try
                                            {
                                                this.Invoke(new Action(() =>
                                                {
                                                    // ====================================================
                                                    // 1. معالجة الرقم بدقة (لتجنب التكرار والخطأ)
                                                    // ====================================================
                                                    string code = txtCountryCode.Text.Trim();
                                                    if (string.IsNullOrEmpty(code)) code = "20";

                                                    // تنظيف الرقم من أي رموز
                                                    string p = rawPhone.Trim().Replace("+", "").Replace(" ", "");
                                                    string finalPhone = p;

                                                    // الخطوة الأهم: لو الرقم بيبدأ بـ 0، لازم نشيله الأول
                                                    if (p.StartsWith("0"))
                                                    {
                                                        p = p.Substring(1); // شيل الصفر
                                                    }

                                                    // دلوقتي نشوف هل بيبدأ بكود الدولة؟
                                                    if (p.StartsWith(code))
                                                    {
                                                        // لو بيبدأ بالكود، تمام سيبه زي ما هو
                                                        finalPhone = p;
                                                    }
                                                    else
                                                    {
                                                        // لو مش بيبدأ بالكود، ضيف الكود
                                                        finalPhone = code + p;
                                                    }

                                                    // تعيين الرقم الحالي للمعالجة
                                                    currentPhoneNumber = finalPhone;

                                                    // ====================================================
                                                    // 2. توجيه للمتصفح (طريقة 3)
                                                    // ====================================================
                                                    if (selectedSendMethod == 3)
                                                    {
                                                        ManageWebViewState();
                                                        Log($"📞 (Pro) استقبال طلب Pipe للرقم: {currentPhoneNumber}");
                                                    }
                                                    else
                                                    {
                                                        // الطرق القديمة
                                                        if (!IsDriverRunning()) RestartChromeDriver();

                                                        if (selectedSendMethod == 1) OpenChat(currentPhoneNumber);
                                                        else Log("📞 سيتم الإرسال Direct.");
                                                    }
                                                }));

                                                // تنفيذ الإرسال (بدون فحص زائد)
                                                string[] files = fileListRaw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                                ProcessAndSendFiles(files);
                                            }
                                            catch (Exception ex)
                                            {
                                                Log("❌ خطأ Pipe: " + ex.Message);
                                            }
                                        });

                                        actionThread.SetApartmentState(ApartmentState.STA);
                                        actionThread.Start();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("❌ خطأ Pipe Server: " + ex.Message);
                        Thread.Sleep(1000);
                    }
                }
            });

            pipeThread.IsBackground = true;
            pipeThread.SetApartmentState(ApartmentState.STA);
            pipeThread.Start();
        }

        private async Task EnsureDriverRunningAsync()
        {
            await Task.Run(() =>
            {
                if (!IsDriverRunning())
                    RestartChromeDriver();
            });
        }

        private async Task OpenChatAsync(string phone)
        {
            await Task.Run(() => OpenChat(phone));
        }

        private async Task SendFilesToChatAsync(string folder)
        {
            await Task.Run(() => SendFilesToChat(folder));
        }

        private async Task SendSpecificFilesToChatAsync(string[] files)
        {
            await Task.Run(() => SendSpecificFilesToChat(files));
        }

        private async Task SafeExitAsync()
        {
            try
            {
                // ✅ لو Method 3 شغال، اقفل WebView2 بشكل نظيف عشان يحتفظ بالجلسة
                if (webViewForm != null && !webViewForm.IsDisposed)
                {
                    await webViewForm.ShutdownPersistSessionAsync();
                    webViewForm = null;
                }
            }
            catch { }

            try { trayIcon.Visible = false; } catch { }

            // باقي التنضيف عندك (Selenium وغيره) موجود في OnFormClosing
            Application.Exit();
        }

        // ================== Chrome / ChromeDriver Helper Methods ==================

        private string GetInstalledChromeVersion()
        {
            try
            {
                string chromeExePath = null;

                // 1) من Registry - LocalMachine
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                {
                    chromeExePath = key?.GetValue(null) as string;
                }

                // 2) من Registry - CurrentUser لو ملقيناش في LocalMachine
                if (string.IsNullOrEmpty(chromeExePath))
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                    {
                        chromeExePath = key?.GetValue(null) as string;
                    }
                }

                // 3) Paths الشائعة
                if (string.IsNullOrEmpty(chromeExePath))
                {
                    string[] candidates =
                    {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };

                    chromeExePath = candidates.FirstOrDefault(File.Exists);
                }

                if (string.IsNullOrEmpty(chromeExePath) || !File.Exists(chromeExePath))
                    return null;

                var info = FileVersionInfo.GetVersionInfo(chromeExePath);
                return info.FileVersion; // مثال: "131.0.6778.86"
            }
            catch
            {
                return null;
            }
        }

        private string GetChromeDriverVersion(string driverPath)
        {
            if (string.IsNullOrEmpty(driverPath) || !File.Exists(driverPath))
                return null;

            try
            {
                var psi = new ProcessStartInfo(driverPath, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                        return null;

                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    // مثال الخرج:
                    // ChromeDriver 131.0.6778.108 (....)
                    if (string.IsNullOrWhiteSpace(output))
                        return null;

                    string[] parts = output.Split(' ');
                    foreach (var p in parts)
                    {
                        if (!string.IsNullOrWhiteSpace(p) &&
                            char.IsDigit(p[0]) &&
                            p.Contains("."))
                        {
                            return p.Trim(); // "131.0.6778.108"
                        }
                    }
                }
            }
            catch
            {
                // نتجاهل أي خطأ ونرجّع null
            }

            return null;
        }

        private string DownloadAndInstallChromeDriver(string chromeVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(chromeVersion))
                    throw new Exception("لم يتم تحديد إصدار Google Chrome.");

                Log("⬇️ جارٍ الحصول على قائمة الإصدارات المتوافقة من Chrome for Testing ...");

                string url = "https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions.json";

                string json = "";
                using (WebClient wc = new WebClient())
                {
                    json = wc.DownloadString(url);
                }

                // نعمل Deserialize يدويًا بدون JSON library (لأن .NET 4.7.2)
                // نبحث عن major version فقط داخل النص

                string major = chromeVersion.Split('.')[0]; // مثل 142

                // نلاقي أول entry تحتوي على major
                string marker = $"\"{major}.";
                int index = json.IndexOf(marker);
                if (index == -1)
                    throw new Exception("لم أجد إصدار متوافق مع Chrome " + chromeVersion);

                // نطلع رقم النسخة بالكامل (مثلاً 142.0.7444.176)
                int start = json.LastIndexOf("\"version\":", index);
                if (start == -1)
                    throw new Exception("تعذر إصدار C.D من ملف JSON.");

                int quote1 = json.IndexOf("\"", start + 10);
                int quote2 = json.IndexOf("\"", quote1 + 1);

                string driverVersion = json.Substring(quote1 + 1, quote2 - quote1 - 1).Trim();

                Log("✓ سيتم تحميل النسخة: " + driverVersion);

                // نبني رابط تحميل zip حسب صيغة Chrome for Testing الجديدة
                string zipUrl = $"https://edgedl.me.gvt1.com/edgedl/chrome/chrome-for-testing/{driverVersion}/win64/chromedriver-win64.zip";

                Log("🔗 رابط التنزيل: " + zipUrl);

                string tempZip = Path.Combine(Path.GetTempPath(), "chromedriver_win64.zip");
                string extractDir = Path.Combine(Path.GetTempPath(), "chromedriver_extract");

                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(zipUrl, tempZip);
                }

                ZipFile.ExtractToDirectory(tempZip, extractDir);

                // الملف داخل المسار الجديد:
                // chromedriver-win64/chromedriver.exe
                string newExe = Path.Combine(extractDir, "chromedriver-win64", "chromedriver.exe");

                if (!File.Exists(newExe))
                    throw new FileNotFoundException("ملف chromedriver.exe غير موجود داخل حزمة Chrome for Testing.");

                string finalPath = Path.Combine(Application.StartupPath, "chromedriver.exe");

                // استبدال النسخة القديمة إن وجدت
                if (File.Exists(finalPath))
                    File.Delete(finalPath);

                File.Copy(newExe, finalPath, true);

                // تنظيف
                try
                {
                    File.Delete(tempZip);
                    Directory.Delete(extractDir, true);
                }
                catch { }

                Log("🎉 تم التثبيت بنجاح: " + finalPath);
                return finalPath;
            }
            catch (Exception ex)
            {
                Log("❌ فشل التنزيل تلقائيًا: " + ex.Message);
                MessageBox.Show("❌ فشل تنزيل C.D:\n" + ex.Message,
                    "خطأ في التحديث التلقائي", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private bool StartChromeDriver()
        {
            // لو فيه درايفر شغال فعلاً
            if (driver != null)
            {
                try
                {
                    var handles = driver.WindowHandles;
                    if (handles != null && handles.Count > 0)
                        return true;
                }
                catch
                {
                    // لو حصل Exception نكمّل ونعتبره مش شغال
                }
            }

            // نحاول نجيب إصدار Chrome المثبّت
            string chromeVersion = GetInstalledChromeVersion();
            if (!string.IsNullOrEmpty(chromeVersion))
                Log("ℹ️ إصدار Google Chrome المثبّت: " + chromeVersion);
            else
                Log("⚠️ لم أستطع تحديد إصدار Google Chrome (قد لا يكون مثبتًا).");

            // نحاول نكتشف chromedriver.exe (من فولدر البرنامج أو الكاش)
            string driverExePath = DetectChromeDriverPath();

            // لو لقينا درايفر، نشوف إصداره ونقارن مع Chrome
            if (!string.IsNullOrEmpty(driverExePath))
            {
                string driverVersion = GetChromeDriverVersion(driverExePath);

                if (!string.IsNullOrEmpty(driverVersion) && !string.IsNullOrEmpty(chromeVersion))
                {
                    int chromeMajor = 0, driverMajor = 0;
                    int.TryParse(chromeVersion.Split('.')[0], out chromeMajor);
                    int.TryParse(driverVersion.Split('.')[0], out driverMajor);

                    Log($"ℹ️ إصدار C.D الحالي: {driverVersion} (Chrome={chromeMajor}, Driver={driverMajor})");

                    if (chromeMajor > 0 && driverMajor > 0 && chromeMajor != driverMajor)
                    {
                        var askUpdate = MessageBox.Show(
                            $"تم العثور على C.D بالإصدار {driverVersion}\n" +
                            $"بينما إصدار Google Chrome هو {chromeVersion}.\n\n" +
                            "قد يؤدي هذا إلى رسالة خطأ من نوع:\n" +
                            "\"This version of C.D only supports Chrome version ...\".\n\n" +
                            "هل تريد أن أقوم بتنزيل إصدار C.D المناسب تلقائيًا الآن؟",
                            "عدم تطابق في الإصدارات",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (askUpdate == DialogResult.Yes)
                        {
                            string newPath = DownloadAndInstallChromeDriver(chromeVersion);
                            if (!string.IsNullOrEmpty(newPath))
                            {
                                driverExePath = newPath;
                            }
                            else
                            {
                                return false; // فشل التحديث
                            }
                        }
                    }
                }
            }
            else
            {
                // لا يوجد أي درايفر → نسأل لو يحب ننزله أوتوماتيك
                var askDownload = MessageBox.Show(
                    "لم أستطع العثور على ملف C.D.\n\n" +
                    "هل تريد أن أقوم بتنزيل إصدار C.D المناسب لإصدار Google Chrome المثبّت تلقائيًا؟",
                    "C.D غير موجود",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (askDownload == DialogResult.Yes)
                {
                    if (string.IsNullOrEmpty(chromeVersion))
                    {
                        MessageBox.Show(
                            "لم أستطع تحديد إصدار Google Chrome المثبّت على هذا الجهاز.\n" +
                            "برجاء تنزيل C.D يدويًا ووضعه بجانب البرنامج.",
                            "تعذر تحديد إصدار Chrome",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);

                        Log("❌ تعذر تحديد إصدار Google Chrome.");
                        return false;
                    }

                    string newPath = DownloadAndInstallChromeDriver(chromeVersion);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        driverExePath = newPath;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    Log("⚠️ المستخدم رفض تنزيل C.D تلقائيًا.");
                    return false;
                }
            }

            // لو بعد كل ده لسه مفيش path
            if (string.IsNullOrEmpty(driverExePath))
            {
                Log("⚠️ لا يوجد C.D.exe بعد محاولة التحديث/التحميل.");
                return false;
            }

            Log("✅ تم تحديد مسار C.D");

            // إعداد الـ ChromeOptions (نفس اللي كان عندك)
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--user-data-dir=" + chromeProfile);
            options.AddArgument("--profile-directory=Default");
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--app=https://web.whatsapp.com");
            options.AddExcludedArgument("enable-automation"); // إخفاء رسالة controlled by automated software

            // ✅✅✅ السطر الجديد لحل مشكلة الإشعارات في ويندوز 10/11 ✅✅✅
            // ده بيجبر كروم يستخدم إشعاراته الداخلية مش إشعارات الويندوز
            options.AddArgument("--disable-features=NativeNotifications");

            // الإشعارات حسب الإعدادات
            if (!Properties.Settings.Default.EnableNotifications)
            {
                options.AddArgument("--disable-notifications");
            }
            else
            {
                options.AddUserProfilePreference(
                    "profile.default_content_setting_values.notifications", 1);
            }

            try
            {
                string driverDir = Path.GetDirectoryName(driverExePath);
                string driverFileName = Path.GetFileName(driverExePath);

                ChromeDriverService service =
                    ChromeDriverService.CreateDefaultService(driverDir, driverFileName);

                service.HideCommandPromptWindow = true;

                // ✅ إحنا اللي محددين exe بنفسنا
                driver = new ChromeDriver(service, options);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "❌ فشل في تشغيل C.D:\n" + ex.Message,
                    "خطأ في C.D",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Log("❌ فشل تشغيل C.D: " + ex.Message);
                driver = null;
                return false;
            }

            Thread.Sleep(1000);
            return true;
        }


        private void RestartChromeDriver()
        {
            try
            {
                if (driver != null)
                    driver.Quit();
            }
            catch { }

            driver = null;

            if (!StartChromeDriver())
            {
                Log("⚠️ لم يتم تشغيل C.D. إلغاء العملية.");
                return;
            }

            OpenWhatsAppWeb();
            Thread.Sleep(2000);
        }


        private void OpenWhatsAppWeb()
        {
            // حماية إضافية لو حد نادى الدالة دي والـ driver مش جاهز
            if (!IsDriverRunning())
            {
                Log("⚠️ لا يمكن فتح واتساب لأن C.D غير جاهز (غالبًا مشكلة نسخة المتصفح).");
                return;
            }

            try
            {
                driver.Navigate().GoToUrl("https://web.whatsapp.com");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                wait.Until(driver =>
                {
                    try
                    {
                        return ((IJavaScriptExecutor)driver).ExecuteScript(@"
                    return document.querySelector('header') !== null ||
                           document.querySelector('[data-testid=chat-list]') !== null ||
                           document.querySelector('[data-ref]') !== null;
                ");
                    }
                    catch { return false; }
                });

                // ✅ حقن WPPConnect
                _ = InjectWppConnectAsync();
                Log("📦 تم");

                // ✅ توقيعك الشخصي
                InjectSignature();
                Log("✍️ تم");

                // ✅ متابعة تلقائية
                _ = StartSignatureLoopAsync();

                Log("✅ تم تحميل واتساب.");
            }
            catch (Exception ex)
            {
                Log("❌ فشل تحميل واتساب: " + ex.Message);
            }
        }

        private void OpenChat(string phoneNumber)
        {
            currentPhoneNumber = phoneNumber;

            Log($"🔍 فتح شات للرقم: {phoneNumber} - selectedSendMethod = {selectedSendMethod}");

            if (selectedSendMethod == 2)
            {
                // ⛔ لم نعد بحاجة لفتح الشات في WPPConnect
                Log("🚫 تم تجاهل فتح الشات.");
                return;
            }

            Log("➡️ دخل فرع الطريقة التقليدية");

            try
            {
                BringChromeToFront();
                driver.Navigate().GoToUrl($"https://web.whatsapp.com/send?phone={phoneNumber}");
                Log($"🔄 جاري فتح الشات مع الرقم: {phoneNumber}");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                wait.Until(d =>
                {
                    if (d.FindElements(By.CssSelector("span[data-icon='plus']")).Count > 0)
                        return true;

                    if (d.PageSource.Contains("phone number shared via url is invalid") ||
                        d.PageSource.Contains("doesn't have a WhatsApp account"))
                        throw new Exception("🚫 الرقم غير مرتبط بحساب واتساب.");

                    return false;
                });

                InjectSignature();
                Log($"✅ تم فتح الشات مع الرقم: {phoneNumber}");
            }
            catch (WebDriverTimeoutException)
            {
                Log("❌ فشل فتح الشات: استغرق وقتًا.");
            }
            catch (Exception ex)
            {
                Log("❌ فشل فتح الشات: " + ex.Message);
            }
        }


        private void SendFilesToChat(string folderPath)
        {
            try
            {
                string[] allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                string[] files = Directory.GetFiles(folderPath)
                    .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToArray();

                ProcessAndSendFiles(files);
            }
            catch (Exception ex)
            {
                Log("❌ فشل في إرسال الملفات من المسار: " + ex.Message);
            }
        }

        private void SendSpecificFilesToChat(string[] files)
        {
            string[] allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var filtered = files
                .Where(f => File.Exists(f) && allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToArray();

            ProcessAndSendFiles(filtered);
        }

        private void ProcessAndSendFiles(string[] files)
        {
            if (files.Length == 0)
            {
                Log("⚠️ لا توجد ملفات مناسبة للإرسال.");
                return;
            }

            // القائمة النهائية التي ستحتوي على مسارات الملفات الجاهزة للإرسال
            var processedList = new List<string>();

            foreach (var originalPath in files)
            {
                try
                {
                    string ext = Path.GetExtension(originalPath).ToLower();

                    // ✅ الحالة الأولى: PDF مع تفعيل خيار التحويل لصور
                    if (ext == ".pdf" && Properties.Settings.Default.ConvertPdfToImage)
                    {
                        var pages = ConvertPdfToJpeg_MultiPage(originalPath);

                        if (pages == null || pages.Count == 0)
                        {
                            Log("⚠️ لم يتم استخراج صفحات من PDF: " + originalPath);
                            continue;
                        }

                        foreach (var pageImg in pages)
                        {
                            try
                            {
                                string cleanedImage = RemoveTrialWatermark(pageImg);
                                string withWatermark = AddWatermarkToImage(cleanedImage);
                                string finalImage = ApplyLetterheadToImage(withWatermark);
                                processedList.Add(finalImage);
                            }
                            catch (Exception ex2)
                            {
                                Log("❌ خطأ فى معالجة صفحة PDF: " + pageImg + " → " + ex2.Message);
                            }
                        }
                    }
                    // ✅ الحالة الثانية: صورة مباشرة
                    else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif")
                    {
                        string cleanedImage = RemoveTrialWatermark(originalPath);
                        string withWatermark = AddWatermarkToImage(cleanedImage);
                        string finalImage = ApplyLetterheadToImage(withWatermark);
                        processedList.Add(finalImage);
                    }
                    // ✅ الحالة الثالثة: ملفات أخرى أو PDF بدون تحويل
                    else
                    {
                        string processedSingle = AddWatermarkToFile(originalPath);
                        processedSingle = ApplyHeaderFooter(processedSingle);
                        processedList.Add(processedSingle);
                    }
                }
                catch (Exception ex)
                {
                    Log("❌ خطأ فى معالجة الملف: " + originalPath + " → " + ex.Message);
                }
            }

            string[] processed = processedList.ToArray();

            if (processed.Length == 0)
            {
                Log("⚠️ لم يتم تجهيز أى ملفات بعد المعالجة.");
                return;
            }

            // إعادة تسمية ملفات PDF
            processed = RenamePdfAttachmentsForSending(processed);

            // =========================================================
            // 🚀 مرحلة الإرسال (تم التعديل لتطابق الطريقة 2)
            // =========================================================

            // ✅ طريقة 3: WebView2 (Pro)
            if (selectedSendMethod == 3)
            {
                // 1. التأكد من الجاهزية (داخل UI Thread)
                this.Invoke(new Action(() =>
                {
                    ManageWebViewState();
                }));

                // انتظار بسيط لو لسه بيحمل
                int attempts = 0;
                while (attempts < 20 && (webViewForm == null || !webViewForm.IsReady))
                {
                    Thread.Sleep(250);
                    attempts++;
                }

                if (webViewForm != null && webViewForm.IsReady)
                {
                    // 🛑 تم إلغاء كود CheckNumberExists عشان يبعت فوراً زي الطريقة 2

                    foreach (var file in processed)
                    {
                        try
                        {
                            // ✅ التوجيه عبر Invoke للإرسال
                            this.Invoke(new Action(() =>
                            {
                                // إرسال مباشر (Fire and Forget)
                                webViewForm.SendFile(currentPhoneNumber, file);
                            }));

                            Log("✅ (Pro) تم توجيه الملف للإرسال: " + Path.GetFileName(file));
                            Thread.Sleep(1500); // فاصل زمني بسيط
                        }
                        catch (Exception ex)
                        {
                            Log("❌ فشل توجيه الملف (Pro): " + ex.Message);
                        }
                    }
                    return; // 🛑 خروج نهائي
                }
                else
                {
                    Log("⚠️ تحذير: المتصفح الخلفي لم يعمل، سيتم محاولة الطرق البديلة.");
                }
            }

            // ✅ طريقة 2: WPPConnect (Selenium Direct)
            if (selectedSendMethod == 2)
            {
                foreach (var file in processed)
                {
                    try
                    {
                        SendFileOnlyViaWppConnect(file);
                        Log("✅ تم إرسال الملف Direct: " + Path.GetFileName(file));
                        Thread.Sleep(800);
                    }
                    catch (Exception ex)
                    {
                        Log("❌ فشل إرسال الملف Direct: " + ex.Message);
                    }
                }
                return; // 🛑 خروج
            }

            // ✅ طريقة 1: الطريقة القديمة (Clipboard Paste)
            try
            {
                SetFilesToClipboard(processed);
                var sim = new WindowsInput.InputSimulator();
                sim.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
                Log("📥 جاري اللصق...");
                Thread.Sleep(2000 + (processed.Length * 500));
                sim.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                Log("✅ تم إرسال الملفات (Clipboard).");
            }
            catch (Exception ex)
            {
                Log("❌ فشل إرسال الملفات Clipboard: " + ex.Message);
            }
        }

        private void SetFilesToClipboard(string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
                return;

            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, filePaths);
            Clipboard.SetDataObject(data, true);
        }


        private string[] RenamePdfAttachmentsForSending(string[] processedFiles)
        {
            // عدّ عدد ملفات الـ PDF فقط
            int pdfCount = 0;
            foreach (var f in processedFiles)
            {
                if (string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
                    pdfCount++;
            }

            // لو مفيش PDF مفيش حاجة نعملها
            if (pdfCount == 0)
                return processedFiles;

            int currentPdfIndex = 0;
            var output = new List<string>(processedFiles.Length);

            foreach (var f in processedFiles)
            {
                string ext = Path.GetExtension(f);

                if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(f);
                    continue;
                }

                currentPdfIndex++;

                string baseName = (pdfCount == 1)
                    ? "Results"
                    : $"Results {currentPdfIndex}";

                string renamed = CreateSendCopyWithName(f, baseName);
                output.Add(renamed);
            }

            return output.ToArray();
        }

        private string CreateSendCopyWithName(string sourcePath, string baseName)
        {
            string sendDir = Path.Combine(Path.GetTempPath(), "ReceiptBridgeSend");
            Directory.CreateDirectory(sendDir);

            string ext = Path.GetExtension(sourcePath);
            string destPath = Path.Combine(sendDir, baseName + ext);

            // Copy/Overwrite
            File.Copy(sourcePath, destPath, true);

            // ✅ اختياري لكنه مفيد: تحديث Title داخل الـ PDF لنفس الاسم
            if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
                TrySetPdfTitle(destPath, baseName);

            return destPath;
        }

        private void TrySetPdfTitle(string pdfPath, string title)
        {
            try
            {
                string dir = Path.GetDirectoryName(pdfPath);
                string tempPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(pdfPath) + "_meta.pdf");

                using (var reader = new iTextSharp.text.pdf.PdfReader(pdfPath))
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, fs))
                {
                    var info = reader.Info != null
                        ? new Dictionary<string, string>(reader.Info)
                        : new Dictionary<string, string>();

                    info["Title"] = title;
                    stamper.MoreInfo = info;
                }

                // استبدال الملف الأصلي بالنسخة المعدلة
                File.Delete(pdfPath);
                File.Move(tempPath, pdfPath);
            }
            catch (Exception ex)
            {
                Log("⚠️ تعذر تعديل Title داخل PDF: " + ex.Message);
                // ما نوقفش الإرسال لو الميتاداتا فشلت
            }
        }


        private void CopyFilesToClipboard(string[] filePaths)
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, filePaths);
            Clipboard.SetDataObject(data, true);
        }

        private void ClickAtElement(IWebElement element)
        {
            var location = element.Location;
            var size = element.Size;

            int centerX = location.X + size.Width / 2;
            int centerY = location.Y + size.Height / 2;

            SetCursorPos(centerX, centerY);
            Thread.Sleep(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN, centerX, centerY, 0, 0);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, centerX, centerY, 0, 0);
        }

        private void BringChromeToFront()
        {
            try
            {
                // 1. إعادة تعيين الـ Handle لو النافذة اتقفلت أو مش موجودة
                // عشان نضمن إنه يدور من جديد كل مرة لو حصل تغيير
                chromeWindowHandle = IntPtr.Zero;

                // 2. البحث عن عملية Chrome عنوانها يحتوي على "WhatsApp" حصراً
                Process[] procs = Process.GetProcessesByName("chrome");
                foreach (Process p in procs)
                {
                    // التعديل هنا: شلنا الشرط بتاع "Google Chrome" عشان ميلقطش المتصفح العادي
                    if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                        p.MainWindowTitle.Contains("WhatsApp"))
                    {
                        chromeWindowHandle = p.MainWindowHandle;
                        break;
                    }
                }

                // لو ملقيناش حاجة نخرج
                if (chromeWindowHandle == IntPtr.Zero)
                {
                    // محاولة أخيرة: استخدام Selenium نفسه لعمل Focus (لو الدرايفر شغال)
                    if (driver != null)
                    {
                        try { driver.SwitchTo().Window(driver.CurrentWindowHandle); } catch { }
                    }
                    return;
                }

                // 3. استعادة النافذة لو كانت Minimized
                if (IsIconic(chromeWindowHandle))
                {
                    ShowWindow(chromeWindowHandle, SW_RESTORE);
                }

                // 4. وضع النافذة في المقدمة
                SetForegroundWindow(chromeWindowHandle);
            }
            catch (Exception ex)
            {
                Log("❌ فشل فى إحضار واتس آب للأمام: " + ex.Message);
            }
        }

        private void InjectSignature()
        {
            string script = @"
        (function () {
            if (document.getElementById('dr-hassan-label')) return;

            const label = document.createElement('div');
            label.id = 'dr-hassan-label';
            label.textContent = 'Created by Dr.Hassan Abdelhamid';

            label.style.position = 'fixed';
            label.style.top = '2px';
            label.style.left = '72px';
            label.style.zIndex = '9999';
            label.style.fontSize = '14px';
            label.style.fontWeight = 'bold';
            label.style.background = 'linear-gradient(to right, #43e97b, #38f9d7)';
            label.style.webkitBackgroundClip = 'text';
            label.style.webkitTextFillColor = 'transparent';
            label.style.textShadow = '1px 1px 2px rgba(0,0,0,0.2)';
            label.style.pointerEvents = 'none';

            document.body.appendChild(label);
        })();
    ";

            ((IJavaScriptExecutor)driver).ExecuteScript(script);
        }


        private void Log(string message)
        {
            if (logView.InvokeRequired)
            {
                logView.Invoke(new Action(() =>
                    logView.Items.Insert(0, new ListViewItem($"{DateTime.Now:HH:mm:ss} - {message}"))
                ));
            }
            else
            {
                logView.Items.Insert(0, new ListViewItem($"{DateTime.Now:HH:mm:ss} - {message}"));
            }
        }

        private bool IsDriverRunning()
        {
            try
            {
                return driver != null && driver.WindowHandles.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                this.Hide();
                this.ShowInTaskbar = false;

                // إظهار تنبيه مرة واحدة (اختياري)
                if (!_trayHintShown && Properties.Settings.Default.EnableNotifications)
                {
                    trayIcon.BalloonTipTitle = "WhatsApp Sender";
                    trayIcon.BalloonTipText = "البرنامج مازال يعمل في الخلفية.\nاضغط مرتين على الأيقونة لعرض البرنامج.";
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;

                    trayIcon.ShowBalloonTip(3000);
                    _trayHintShown = true;
                }

                return;
            }

            DisableAutoCloseHook();

            try { driver?.Quit(); } catch { }

            base.OnFormClosing(e);
        }

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("psapi.dll", SetLastError = true)] static extern bool GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpFilename, int nSize);

        [DllImport("ntdll.dll", SetLastError = true)] private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, byte[] processInformation, int processInformationLength, ref int returnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const int EM_SETRECTNP = 0x00B4;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        private void CenterTextVertically(TextBox tb, int horizontalPadding = 6)
        {
            if (tb == null || !tb.IsHandleCreated) return;

            // EM_SETRECTNP بيشتغل عمليًا مع الـ Multiline فقط
            if (!tb.Multiline) return;

            int lineHeight = TextRenderer.MeasureText("A", tb.Font).Height;
            int top = Math.Max(0, (tb.ClientSize.Height - lineHeight) / 2);

            var rc = new RECT
            {
                Left = horizontalPadding,
                Top = top,
                Right = tb.ClientSize.Width - horizontalPadding,
                Bottom = tb.ClientSize.Height
            };

            SendMessage(tb.Handle, EM_SETRECTNP, IntPtr.Zero, ref rc);
            tb.Invalidate(); // مهم عشان يعيد رسم النص بالمكان الجديد
        }


        private void HookVerticalCentering(TextBox tb, int horizontalPadding = 6)
        {
            if (tb == null) return;

            EventHandler apply = (s, e) =>
            {
                // لازم Multiline عشان EM_SETRECTNP يشتغل
                if (tb.Multiline)
                    CenterTextVertically(tb, horizontalPadding);
            };

            tb.HandleCreated += apply;
            tb.Resize += apply;
            tb.FontChanged += apply;

            // ضمان إضافي بعد ظهور الفورم
            this.Shown += (s, e) => apply(null, EventArgs.Empty);

            // لو الـHandle جاهز بالفعل
            if (tb.IsHandleCreated)
                apply(null, EventArgs.Empty);
        }


        private string GetCommandLine(Process process)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                {
                    foreach (var @object in searcher.Get())
                    {
                        return @object["CommandLine"]?.ToString();
                    }
                }
            }
            catch { }

            return null;
        }


        private void chkDarkMode_CheckedChanged(object sender, EventArgs e)
        {
            // 1. حفظ الاختيار الجديد
            Properties.Settings.Default.DarkMode = chkDarkMode.Checked;
            Properties.Settings.Default.Save();

            // 2. تطبيق الألوان
            bool isDark = chkDarkMode.Checked;

            // ===== Palette ثابتة (Dark / Light) =====
            Color formBg = isDark ? Color.FromArgb(24, 24, 26) : Color.FromArgb(245, 246, 250);
            Color panelBg = isDark ? Color.FromArgb(32, 32, 35) : Color.White;
            Color cardBg = isDark ? Color.FromArgb(38, 38, 42) : Color.White;
            Color inputBg = isDark ? Color.FromArgb(28, 28, 30) : Color.FromArgb(250, 250, 252);
            Color textFg = isDark ? Color.Gainsboro : Color.FromArgb(30, 35, 40);
            Color mutedFg = isDark ? Color.FromArgb(170, 175, 180) : Color.FromArgb(110, 120, 130);

            this.BackColor = formBg;

            if (panelTop != null) panelTop.BackColor = panelBg;

            if (groupBoxLog != null)
            {
                groupBoxLog.BackColor = Color.Transparent;
                groupBoxLog.ForeColor = isDark ? Color.Gainsboro : Color.FromArgb(75, 85, 95);
            }

            if (logView != null)
            {
                logView.BackColor = isDark ? Color.FromArgb(22, 22, 24) : Color.White;
                logView.ForeColor = isDark ? Color.Gainsboro : Color.FromArgb(30, 35, 40);
            }

            // ✅ تلوين كل العناصر
            ApplyThemeRecursive(this, isDark, panelBg, cardBg, inputBg, textFg, mutedFg);

            // ✅ استثناء أزرار معينة لتظل بلونها المميز
            if (btnOpenWhatsApp != null) btnOpenWhatsApp.ForeColor = Color.White;
            if (btnSend != null) btnSend.ForeColor = Color.White;

            if (chkEnableNotifications != null) chkEnableNotifications.ForeColor = textFg;
            if (chkDarkMode != null) chkDarkMode.ForeColor = textFg;

            // إعادة رسم الهيدر والحدود لتحديث الألوان
            if (panelTop != null) panelTop.Invalidate();
        }

        private void ApplyThemeRecursive(Control root, bool isDark,
            Color panelBg, Color cardBg, Color inputBg, Color textFg, Color mutedFg)
        {
            foreach (Control c in root.Controls)
            {
                // ===== MenuStrip / ToolStrip =====
                if (c is MenuStrip ms)
                {
                    ms.BackColor = isDark ? panelBg : Color.White;
                    ms.ForeColor = isDark ? textFg : Color.FromArgb(30, 35, 40);
                    ms.Renderer = new ToolStripProfessionalRenderer(new AppMenuColorTable(isDark, panelBg, cardBg, textFg));
                }
                else if (c is ToolStrip ts)
                {
                    ts.BackColor = isDark ? panelBg : Color.White;
                    ts.ForeColor = isDark ? textFg : Color.FromArgb(30, 35, 40);
                    ts.Renderer = new ToolStripProfessionalRenderer(new AppMenuColorTable(isDark, panelBg, cardBg, textFg));
                }

                // ===== Panels (مبني على Tag مش على اللون) =====
                if (c is Panel p)
                {
                    if ((p.Tag as string) == "card")
                        p.BackColor = isDark ? cardBg : Color.White;
                    else if ((p.Tag as string) == "panel")
                        p.BackColor = isDark ? panelBg : Color.White;
                    else
                        p.BackColor = Color.Transparent;
                }
                else if (c is TableLayoutPanel tlp)
                {
                    tlp.BackColor = Color.Transparent;
                }
                else if (c is GroupBox gb)
                {
                    gb.BackColor = Color.Transparent;
                    gb.ForeColor = isDark ? Color.Gainsboro : Color.FromArgb(75, 85, 95);
                }
                else if (c is Label lbl)
                {
                    // subtitle/muted
                    bool isMuted = (lbl.Font != null && lbl.Font.Size <= 9F && lbl.Font.Style == FontStyle.Regular);
                    lbl.ForeColor = isMuted ? mutedFg : textFg;
                }
                else if (c is TextBox tb)
                {
                    tb.BackColor = inputBg;
                    tb.ForeColor = isDark ? Color.Gainsboro : Color.FromArgb(25, 30, 35);
                    tb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (c is ListView lv)
                {
                    lv.BackColor = isDark ? Color.FromArgb(22, 22, 24) : Color.White;
                    lv.ForeColor = isDark ? Color.Gainsboro : Color.FromArgb(30, 35, 40);
                }

                // ToolStrip dropdowns (القوائم المنسدلة)
                if (c.ContextMenuStrip != null)
                {
                    c.ContextMenuStrip.Renderer =
                        new ToolStripProfessionalRenderer(new AppMenuColorTable(isDark, panelBg, cardBg, textFg));
                    c.ContextMenuStrip.BackColor = isDark ? cardBg : Color.White;
                    c.ContextMenuStrip.ForeColor = isDark ? textFg : Color.FromArgb(30, 35, 40);
                }

                if (c.HasChildren)
                    ApplyThemeRecursive(c, isDark, panelBg, cardBg, inputBg, textFg, mutedFg);

                if (topMenuStrip != null)
                {
                    topMenuStrip.ForeColor = chkDarkMode.Checked
                        ? Color.FromArgb(200, 205, 210)
                        : Color.FromArgb(30, 35, 40);

                    if (settingsMenu != null)
                        ApplyToolStripTextColor(settingsMenu, chkDarkMode.Checked);
                }
            }
        }

        public void QuitDriver()
        {
            try
            {
                driver?.Quit();
            }
            catch { }
        }
        private string AddWatermarkToFile(string originalPath)
        {
            string extension = Path.GetExtension(originalPath).ToLower();

            if (extension == ".pdf")
            {
                if (Properties.Settings.Default.ConvertPdfToImage)
                {
                    string imagePath = ConvertPdfToJpeg(originalPath);

                    // ✅ تنظيف الصورة المحولة
                    imagePath = RemoveTrialWatermark(imagePath);

                    string imgExt = Path.GetExtension(imagePath).ToLower();
                    if (imgExt == ".jpg" || imgExt == ".jpeg" ||
                        imgExt == ".png" || imgExt == ".bmp" || imgExt == ".gif")
                    {
                        return AddWatermarkToImage(imagePath);
                    }
                    return AddWatermarkToPdf(originalPath);
                }
                else
                {
                    return AddWatermarkToPdf(originalPath);
                }
            }
            else if (extension == ".jpg" || extension == ".jpeg" ||
                     extension == ".png" || extension == ".bmp" || extension == ".gif")
            {
                // ✅ تنظيف الصورة الأصلية لو هي جاية كده
                string cleaned = RemoveTrialWatermark(originalPath);
                return AddWatermarkToImage(cleaned);
            }
            else
            {
                return originalPath;
            }
        }


        private string AddWatermarkToImage(string originalPath)
        {
            string watermarkPath = Path.Combine(Application.StartupPath, "Watermark", "logo.png");
            if (!File.Exists(watermarkPath))
            {
                Log("⚠️ لم يتم العثور على صورة العلامة المائية.");
                return originalPath;
            }

            try
            {
                using (Image baseImage = Image.FromFile(originalPath))
                using (Image watermark = Image.FromFile(watermarkPath))
                using (Graphics g = Graphics.FromImage(baseImage))
                {
                    int percent = Properties.Settings.Default.WatermarkSizePercent;
                    string position = Properties.Settings.Default.WatermarkPosition;
                    int offsetRight = Properties.Settings.Default.WatermarkOffsetRight;
                    int offsetLeft = Properties.Settings.Default.WatermarkOffsetLeft;
                    int offsetTop = Properties.Settings.Default.WatermarkOffsetTop;
                    int offsetBottom = Properties.Settings.Default.WatermarkOffsetBottom;
                    int opacity = Properties.Settings.Default.WatermarkOpacity;

                    int targetWidth = (int)(baseImage.Width * (percent / 100.0));
                    int targetHeight = (int)(watermark.Height * ((float)targetWidth / watermark.Width));
                    var resizedWatermark = new Bitmap(watermark, new Size(targetWidth, targetHeight));

                    int x = (baseImage.Width - targetWidth) / 2;
                    int y = (baseImage.Height - targetHeight) / 2;

                    switch (position)
                    {
                        case "أعلى يسار": x = 0; y = 0; break;
                        case "أعلى يمين": x = baseImage.Width - targetWidth; y = 0; break;
                        case "أسفل يسار": x = 0; y = baseImage.Height - targetHeight; break;
                        case "أسفل يمين": x = baseImage.Width - targetWidth; y = baseImage.Height - targetHeight; break;
                    }

                    x += offsetRight - offsetLeft;
                    y += offsetBottom - offsetTop;

                    var matrix = new System.Drawing.Imaging.ColorMatrix();
                    matrix.Matrix33 = opacity / 100f;

                    var attributes = new System.Drawing.Imaging.ImageAttributes();
                    attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

                    g.DrawImage(
                        resizedWatermark,
                        new Rectangle(x, y, resizedWatermark.Width, resizedWatermark.Height),
                        0, 0, resizedWatermark.Width, resizedWatermark.Height,
                        GraphicsUnit.Pixel,
                        attributes
                    );

                    string tempDir = Path.Combine(Path.GetTempPath(), "Watermarked");
                    Directory.CreateDirectory(tempDir);
                    string newPath = Path.Combine(tempDir, Path.GetFileName(originalPath));
                    baseImage.Save(newPath);
                    return newPath;
                }
            }
            catch (Exception ex)
            {
                Log("❌ فشل في إضافة العلامة المائية: " + ex.Message);
                return originalPath;
            }
        }

        private string AddWatermarkToPdf(string originalPath)
        {
            string watermarkPath = Path.Combine(Application.StartupPath, "Watermark", "logo.png");
            if (!File.Exists(watermarkPath))
            {
                Log("⚠️ لم يتم العثور على صورة العلامة المائية.");
                return originalPath;
            }

            try
            {
                int percent = Properties.Settings.Default.WatermarkSizePercent;
                string position = Properties.Settings.Default.WatermarkPosition;
                int offsetRight = Properties.Settings.Default.WatermarkOffsetRight;
                int offsetLeft = Properties.Settings.Default.WatermarkOffsetLeft;
                int offsetTop = Properties.Settings.Default.WatermarkOffsetTop;
                int offsetBottom = Properties.Settings.Default.WatermarkOffsetBottom;
                int opacity = Properties.Settings.Default.WatermarkOpacity;

                string tempDir = Path.Combine(Path.GetTempPath(), "Watermarked");
                Directory.CreateDirectory(tempDir);
                string newPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(originalPath) + "_wm.pdf");

                using (var reader = new iTextSharp.text.pdf.PdfReader(originalPath))
                using (var fs = new FileStream(newPath, FileMode.Create, FileAccess.Write))
                using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, fs))
                {
                    iTextSharp.text.Image watermarkImage = iTextSharp.text.Image.GetInstance(File.ReadAllBytes(watermarkPath));

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

                        PdfContentByte content = stamper.GetOverContent(i);
                        PdfGState gstate = new PdfGState { FillOpacity = opacity / 100f };

                        content.SaveState();
                        content.SetGState(gstate);
                        content.AddImage(watermarkImage);
                        content.RestoreState();
                    }
                }

                return newPath;
            }
            catch (Exception ex)
            {
                Log("❌ فشل في إضافة علامة مائية للـ PDF: " + ex.Message);
                return originalPath;
            }
        }
        private void btnWatermarkSettings_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new WatermarkSettingsForm())
            {
                settingsForm.ShowDialog();
            }
        }
        private string ApplyLetterheadToImage(string path)
        {
            if (!Properties.Settings.Default.EnableLetterhead) return path;

            string newPath = Path.Combine(Path.GetTempPath(), "Letterhead", Path.GetFileName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(newPath));

            using (Image original = Image.FromFile(path))
            {
                int width = original.Width;
                int originalHeight = original.Height;

                // نسخة نشتغل عليها
                using (Bitmap imageWithHeader = new Bitmap(original))
                {
                    // 1) رسم الهيدر (اختياري)
                    using (Graphics g = Graphics.FromImage(imageWithHeader))
                    {
                        if (File.Exists(Properties.Settings.Default.HeaderImagePath))
                        {
                            using (Image header = Image.FromFile(Properties.Settings.Default.HeaderImagePath))
                            {
                                int opacity = Properties.Settings.Default.HeaderOpacity;
                                float alpha = opacity / 100f;

                                int headerHeight = (int)(header.Height * (width / (float)header.Width));
                                int y = Properties.Settings.Default.HeaderOffsetTop;

                                var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha };
                                var attributes = new System.Drawing.Imaging.ImageAttributes();
                                attributes.SetColorMatrix(matrix);

                                g.DrawImage(
                                    header,
                                    new Rectangle(0, y, width, headerHeight),
                                    0, 0, header.Width, header.Height,
                                    GraphicsUnit.Pixel,
                                    attributes
                                );
                            }
                        }
                    }

                    // 2) الفوتر
                    if (File.Exists(Properties.Settings.Default.FooterImagePath))
                    {
                        using (Image footer = Image.FromFile(Properties.Settings.Default.FooterImagePath))
                        {
                            int footerOpacity = Properties.Settings.Default.FooterOpacity;
                            float alpha = footerOpacity / 100f;

                            int footerOffset = Properties.Settings.Default.FooterOffsetBottom;

                            // ارتفاع الفوتر على حسب عرض الصورة
                            int footerHeight = (int)(footer.Height * (width / (float)footer.Width));

                            // Buffer زيادة اختيارية من الإعدادات
                            int extendSetting = Math.Max(0, Properties.Settings.Default.FooterExtendHeight);

                            // مكان الفوتر الطبيعي داخل نفس ارتفاع الصورة
                            int footerTopY = originalHeight - footerHeight - footerOffset;
                            if (footerTopY < 0) footerTopY = 0;

                            int footerAreaHeight = Math.Min(footerHeight, originalHeight - footerTopY);
                            Rectangle footerArea = new Rectangle(0, footerTopY, width, Math.Max(0, footerAreaHeight));

                            // ✅ فحص هل الفوتر هيغطي بيانات فعلًا؟
                            bool overlapsContent = footerArea.Height > 0 && AreaContainsContent(imageWithHeader, footerArea);

                            // ✅ لو هيتغطى بيانات -> نطوّل أقل تطويل مطلوب فقط
                            if (overlapsContent)
                            {
                                int margin = 6; // مسافة أمان بسيطة بين آخر سطر والفوتر
                                int lastContentY = FindLastContentY(imageWithHeader);

                                int requiredNewHeight = (lastContentY + margin) + footerHeight + footerOffset;

                                // لو فعلاً محتاجين نزوّد ارتفاع
                                if (requiredNewHeight > originalHeight)
                                {
                                    int newHeight = requiredNewHeight + extendSetting;

                                    using (Bitmap finalImage = new Bitmap(width, newHeight))
                                    using (Graphics g = Graphics.FromImage(finalImage))
                                    {
                                        g.Clear(Color.White);
                                        g.DrawImage(imageWithHeader, 0, 0);

                                        var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha };
                                        var attributes = new System.Drawing.Imaging.ImageAttributes();
                                        attributes.SetColorMatrix(matrix);

                                        int newFooterY = newHeight - footerHeight - footerOffset;
                                        if (newFooterY < 0) newFooterY = 0;

                                        g.DrawImage(
                                            footer,
                                            new Rectangle(0, newFooterY, width, footerHeight),
                                            0, 0, footer.Width, footer.Height,
                                            GraphicsUnit.Pixel,
                                            attributes
                                        );

                                        finalImage.Save(newPath);
                                    }

                                    return newPath;
                                }
                            }

                            // ✅ مفيش بيانات هتتغطى -> ما نطوّلش، ارسم الفوتر عادي
                            using (Graphics g = Graphics.FromImage(imageWithHeader))
                            {
                                var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha };
                                var attributes = new System.Drawing.Imaging.ImageAttributes();
                                attributes.SetColorMatrix(matrix);

                                g.DrawImage(
                                    footer,
                                    footerArea,
                                    0, 0, footer.Width, footer.Height,
                                    GraphicsUnit.Pixel,
                                    attributes
                                );
                            }

                            imageWithHeader.Save(newPath);
                            return newPath;
                        }
                    }

                    // لو مفيش Footer أصلاً
                    imageWithHeader.Save(newPath);
                    return newPath;
                }
            }
        }

        private string ApplyLetterheadToPdf(string originalPath)
        {
            if (!Properties.Settings.Default.EnableLetterhead) return originalPath;

            string headerPath = Properties.Settings.Default.HeaderImagePath;
            string footerPath = Properties.Settings.Default.FooterImagePath;
            int headerOpacity = Properties.Settings.Default.HeaderOpacity;
            int footerOpacity = Properties.Settings.Default.FooterOpacity;
            int headerOffset = Properties.Settings.Default.HeaderOffsetTop;
            int footerOffset = Properties.Settings.Default.FooterOffsetBottom;

            if (!File.Exists(headerPath) && !File.Exists(footerPath))
                return originalPath;

            try
            {
                string outputPath = Path.Combine(
                    Path.GetTempPath(),
                    "Letterhead",
                    Path.GetFileNameWithoutExtension(originalPath) + "_lh.pdf"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                using (var reader = new iTextSharp.text.pdf.PdfReader(originalPath))
                using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, fs))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        var pageSize = reader.GetPageSizeWithRotation(i);
                        float width = pageSize.Width;
                        float height = pageSize.Height;

                        var content = stamper.GetOverContent(i);

                        // ✅ 0) امسح/غطّي السطر الأحمر في أسفل الصفحة قبل أي شيء
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

                return outputPath;
            }
            catch (Exception ex)
            {
                Log("❌ فشل في تطبيق Header/Footer على PDF: " + ex.Message);
                return originalPath;
            }
        }

        private void btnApplyLetterhead_Click(object sender, EventArgs e)
        {
            using (var form = new LetterheadSettingsForm())
            {
                form.ShowDialog();
            }
        }
        private void chkEnableNotifications_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.EnableNotifications = chkEnableNotifications.Checked;
            Properties.Settings.Default.Save();
            MessageBox.Show("⚙️ ستطبق الإعدادات عند فتح واتساب في المرة القادمة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task StartSignatureLoopAsync()
        {
            while (true)
            {
                try
                {
                    if (driver != null)
                    {
                        bool exists = (bool)((IJavaScriptExecutor)driver).ExecuteScript(@"
                    return !!document.getElementById('dr-hassan-label');
                ");

                        if (!exists)
                        {
                            InjectSignature();
                        }
                    }
                }
                catch { }

                await Task.Delay(5000); // تحقق كل 5 ثواني
            }
        }

        private string JsEscape(string s)
        {
            if (s == null) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "")
                .Replace("\n", "");
        }

        private void SendFileOnlyViaWppConnect(string filePath)
        {
            if (string.IsNullOrEmpty(currentPhoneNumber))
            {
                Log("❌ رقم الهاتف غير محدد. لن يتم الإرسال.");
                return;
            }

            string jid = currentPhoneNumber.EndsWith("@c.us")
                ? currentPhoneNumber
                : currentPhoneNumber + "@c.us";

            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLower();

            string mimeType = "application/octet-stream";
            if (extension == ".jpg" || extension == ".jpeg") mimeType = "image/jpeg";
            else if (extension == ".png") mimeType = "image/png";
            else if (extension == ".gif") mimeType = "image/gif";
            else if (extension == ".pdf") mimeType = "application/pdf";

            string wppType = (mimeType.StartsWith("image")) ? "image" : "document";

            string base64 = Convert.ToBase64String(File.ReadAllBytes(filePath));
            string base64Url = $"data:{mimeType};base64,{base64}";

            string jidJs = JsEscape(jid);
            string dataJs = JsEscape(base64Url);
            string nameJs = JsEscape(fileName);
            string typeJs = JsEscape(wppType);
            string mimeJs = JsEscape(mimeType);

            int maxRetries = 3;
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    // ✅ التعديل الجديد: الكشف عن وجود الرقم قبل الإرسال
                    string script = $@"
                    return (async () => {{
                        if (!window.WPP || !WPP.chat || !WPP.contact) return 'NOT_READY';
                        try {{
                            // 1. فحص هل الرقم مسجل في واتساب أصلاً؟
                            const exists = await WPP.contact.queryExists('{jidJs}');
                            if (!exists) return 'INVALID_NUMBER';

                            // 2. تحضير الشات (للأرقام الجديدة)
                            try {{ await WPP.chat.find('{jidJs}'); }} catch {{}}

                            // 3. إرسال الملف
                            await WPP.chat.sendFileMessage('{jidJs}', '{dataJs}', {{
                                type: '{typeJs}',
                                filename: '{nameJs}',
                                mimetype: '{mimeJs}',
                                createChat: true
                            }});
                            return 'SUCCESS';
                        }} catch (err) {{
                            return 'ERROR: ' + err;
                        }}
                    }})();";

                    object result = ((IJavaScriptExecutor)driver).ExecuteScript(script);
                    string resStr = result != null ? result.ToString() : "";

                    if (resStr == "SUCCESS")
                    {
                        Log($"✅ تم إرسال الملف: {fileName}");
                        return;
                    }
                    else if (resStr == "INVALID_NUMBER")
                    {
                        // 🛑 هنا بقى الميزة الجديدة: لو الرقم غلط هنوقف فوراً
                        Log($"❌ فشل الإرسال: الرقم {currentPhoneNumber} ليس لديه حساب واتساب.");
                        return; // خروج نهائي من الدالة (مفيش داعي نعيد المحاولة)
                    }
                    else if (resStr == "NOT_READY")
                    {
                        if (i < maxRetries)
                        {
                            Log($"⏳ واتساب يجهز... (محاولة {i})");
                            Thread.Sleep(2000);
                        }
                    }
                    else
                    {
                        // خطأ آخر (نت أو غيره)
                        if (i < maxRetries)
                        {
                            Log($"⏳ تعذر إرسال الملف (محاولة {i})، جاري الإعادة...");
                            Thread.Sleep(2000);
                        }
                        else
                        {
                            Log($"❌ خطأ تقني في الإرسال: {resStr}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠️ خطأ أثناء إرسال الملف (محاولة {i}): {ex.Message}");
                    Thread.Sleep(1000);
                }
            }

            Log($"❌ فشل إرسال الملف {fileName} نهائياً.");
        }


        private void InjectWppConnect()
        {
            string script = @"
        (() => {
            if (window.WPP) return;

            const s = document.createElement('script');
            s.src = 'https://raw.githubusercontent.com/wppconnect-team/wa-js/main/dist/wppconnect-wa.js';
            s.type = 'text/javascript';
            s.onload = () => console.log('✅ WPPConnect Loaded');
            document.head.appendChild(s);
        })();
    ";

            ((IJavaScriptExecutor)driver).ExecuteScript(script);
        }
        private async Task<string> DownloadWppScriptAsync()
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                return await client.GetStringAsync("https://cdn.jsdelivr.net/npm/@wppconnect/wa-js@latest/dist/wppconnect-wa.js");
            }
        }

        private async Task InjectWppConnectAsync()
        {
            try
            {
                string jsCode = await DownloadWppScriptAsync();

                // احقنه في الصفحة
                ((IJavaScriptExecutor)driver).ExecuteScript(jsCode);

                Log("✅ تم التحميل .");
            }
            catch (Exception ex)
            {
                Log("❌ فشل في التحميل: " + ex.Message);
            }
        }

        private void SendInitialMessageViaWppConnect()
        {
            if (string.IsNullOrEmpty(currentPhoneNumber))
            {
                Log("❌ رقم الهاتف غير محدد. لن يتم الإرسال.");
                return;
            }

            string jid = currentPhoneNumber.EndsWith("@c.us")
                ? currentPhoneNumber
                : currentPhoneNumber + "@c.us";

            // ✅ التعديل: المحاولة 3 مرات في حالة الفشل (عشان لو لسه فاتح)
            int maxRetries = 3;
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    string script = $@"
                    return (async () => {{
                        if (!window.WPP || !WPP.chat) return 'NOT_READY';
                        try {{
                            await WPP.chat.sendTextMessage('{jid}', '📌📋', {{ createChat: true }});
                            return 'SUCCESS';
                        }} catch (err) {{
                            return 'ERROR';
                        }}
                    }})();";

                    object result = ((IJavaScriptExecutor)driver).ExecuteScript(script);
                    string resStr = result != null ? result.ToString() : "";

                    if (resStr == "SUCCESS")
                    {
                        Log("✅ تم إرسال رسالة فتح الشات.");
                        return; // نجحنا، نخرج من الدالة
                    }
                    else
                    {
                        // لو فشل، نسجل ونستنى شوية
                        if (i < maxRetries)
                        {
                            Log($"⏳ محاولة ({i}) فشلت (الواتس يجهز)، جاري الإعادة...");
                            Thread.Sleep(1500); // استنى ثانية ونص
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠️ خطأ عابر في المحاولة {i}: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }

            Log("❌ فشل فتح الشات بعد كل المحاولات.");
        }

        private bool AreaContainsContent(Bitmap image, Rectangle area)
        {
            // قصّ المنطقة داخل حدود الصورة عشان ما يحصلش أخطاء
            Rectangle bounds = new Rectangle(0, 0, image.Width, image.Height);
            Rectangle r = Rectangle.Intersect(bounds, area);
            if (r.Width <= 0 || r.Height <= 0) return false;

            // تقدير لون/إضاءة الخلفية داخل المنطقة (90th percentile luminance)
            float bgLum = EstimateBackgroundLuminance(image, r, sampleStep: 6);

            // أي Pixel أغمق من الخلفية بفارق كافي = "Ink"
            const float delta = 18f;     // حساسية الكشف (كبرها = أقل حساسية)
            const int step = 3;

            int inkCount = 0;
            int total = 0;

            for (int y = r.Top; y < r.Bottom; y += step)
            {
                for (int x = r.Left; x < r.Right; x += step)
                {
                    Color p = image.GetPixel(x, y);
                    total++;

                    if (IsInkPixel(p, bgLum, delta))
                        inkCount++;
                }
            }

            if (total == 0) return false;

            // لازم يبقى فيه نسبة ink معقولة (مش Noise)
            double ratio = inkCount / (double)total;
            return ratio >= 0.002; // 0.2%
        }

        private bool IsInkPixel(Color c, float bgLum, float delta)
        {
            // تجاهل الشفافية لو موجودة
            if (c.A < 20) return false;

            // Luminance
            float lum = (0.2126f * c.R) + (0.7152f * c.G) + (0.0722f * c.B);

            // يعتبر "محتوى" لو أغمق من الخلفية بفارق delta
            return lum < (bgLum - delta);
        }

        private float EstimateBackgroundLuminance(Bitmap img, Rectangle r, int sampleStep = 6)
        {
            // ناخد عينات ونطلع 90th percentile (الخلفية غالبًا هي الأكثر)
            List<float> lums = new List<float>(4096);

            for (int y = r.Top; y < r.Bottom; y += sampleStep)
            {
                for (int x = r.Left; x < r.Right; x += sampleStep)
                {
                    Color c = img.GetPixel(x, y);
                    float lum = (0.2126f * c.R) + (0.7152f * c.G) + (0.0722f * c.B);
                    lums.Add(lum);
                }
            }

            if (lums.Count == 0) return 255f;

            lums.Sort();
            int idx = (int)(lums.Count * 0.90); // 90%
            if (idx >= lums.Count) idx = lums.Count - 1;
            return lums[idx];
        }

        private int FindLastContentY(Bitmap image)
        {
            Rectangle full = new Rectangle(0, 0, image.Width, image.Height);
            float bgLum = EstimateBackgroundLuminance(image, full, sampleStep: 10);
            const float delta = 18f;
            const int step = 4;

            for (int y = image.Height - 1; y >= 0; y -= 1)
            {
                int ink = 0;
                int total = 0;

                for (int x = 0; x < image.Width; x += step)
                {
                    Color p = image.GetPixel(x, y);
                    total++;
                    if (IsInkPixel(p, bgLum, delta)) ink++;
                }

                if (total > 0 && (ink / (double)total) >= 0.003) // 0.3% في صف واحد
                    return y;
            }

            return 0;
        }

        private bool TryDeleteChromeDriversFromPath()
        {
            DialogResult answer = MessageBox.Show(
                "تم اكتشاف نسخة قديمة من ChromeDriver قد لا تكون متوافقة مع Google Chrome.\n\n" +
                "هل تريد حذف أي نسخة chromedriver.exe قديمة تلقائيًا؟",
                "تعارض ChromeDriver",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return false;

            int deletedCount = 0;
            StringBuilder errors = new StringBuilder();

            try
            {
                // 1) البحث في PATH
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(';');

                    foreach (string dir in paths)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(dir)) continue;
                            if (!Directory.Exists(dir)) continue;

                            string file = Path.Combine(dir, "chromedriver.exe");
                            if (File.Exists(file))
                            {
                                try
                                {
                                    File.Delete(file);
                                    deletedCount++;
                                    Log("🧹 تم حذف chromedriver: " + file);
                                }
                                catch (Exception exDel)
                                {
                                    errors.AppendLine("لم يتم حذف: " + file + " → " + exDel.Message);
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 2) فولدر البرنامج نفسه
                string appDriver = Path.Combine(Application.StartupPath, "chromedriver.exe");
                if (File.Exists(appDriver))
                {
                    try
                    {
                        File.Delete(appDriver);
                        deletedCount++;
                        Log("🧹 تم حذف chromedriver من فولدر البرنامج.");
                    }
                    catch (Exception exDel)
                    {
                        errors.AppendLine("لم يتم حذف: " + appDriver + " → " + exDel.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.AppendLine("خطأ أثناء البحث: " + ex.Message);
            }

            if (deletedCount > 0)
            {
                MessageBox.Show("تم حذف " + deletedCount + " ملفات ChromeDriver.\n" +
                                (errors.Length > 0 ? "\nبعض الملفات لم تُحذف:\n" + errors : ""),
                                 "تم التنظيف",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Information);

                return true;
            }
            else
            {
                MessageBox.Show("لم يتم العثور على أي chromedriver.exe لحذفه.",
                                 "لا يوجد ملفات",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Information);
                return false;
            }
        }

        /// <summary>
        /// يحاول يلاقي ملف chromedriver.exe من فولدر البرنامج
        /// أو من كاش Selenium Manager:
        ///   %USERPROFILE%\.cache\selenium\chromedriver\win64\
        /// ويرجّع الـ path لو لقيه، أو null لو مش موجود.
        /// </summary>
        private string DetectChromeDriverPath()
        {
            try
            {
                // 1) أولوية لفولدر البرنامج نفسه
                string appDriver = Path.Combine(Application.StartupPath, "chromedriver.exe");
                if (File.Exists(appDriver))
                    return appDriver;

                // 2) نحاول نستخدم الكاش اللي Selenium Manager كان بيحط فيه الدرايفر
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string cacheRoot = Path.Combine(userProfile, ".cache", "selenium", "chromedriver", "win64");

                if (Directory.Exists(cacheRoot))
                {
                    // نجيب كل الفولدرات اللي جوا win64 (كل فولدر = نسخة)
                    string[] versionDirs = Directory.GetDirectories(cacheRoot);

                    if (versionDirs != null && versionDirs.Length > 0)
                    {
                        // ناخد آخر فولدر بالترتيب الأبجدي (غالباً أحدث نسخة)
                        string latestDir = versionDirs
                            .OrderByDescending(d => d)
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(latestDir))
                        {
                            string driverPath = Path.Combine(latestDir, "chromedriver.exe");
                            if (File.Exists(driverPath))
                                return driverPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء محاولة اكتشاف C.D: " + ex.Message);
            }

            // لو ملقيناش حاجة
            return null;
        }

        // الدالة العامة اللى بيستخدمها AddWatermarkToFile
        // الدالة العامة اللى بيستخدمها AddWatermarkToFile
        private string ConvertPdfToJpeg(string pdfPath)
        {
            try
            {
                // نستخدم Ghostscript على أى ويندوز طالما المسار متضبط
                string gsPath = GetGhostscriptPath();
                if (string.IsNullOrEmpty(gsPath))
                {
                    Log("⚠️ خيار تحويل PDF إلى صورة مفعّل، " +
                        "لكن لم يتم تعيين مسار Ghostscript بعد. سيتم إرسال ملف الـ PDF كما هو.");
                    return pdfPath;
                }

                Log("ℹ️ سيتم استخدام Ghostscript لتحويل PDF إلى صورة عبر: " + gsPath);
                return ConvertPdfToJpeg_Ghostscript(pdfPath);
            }
            catch (Exception ex)
            {
                Log("❌ خطأ عام أثناء تحويل PDF لصورة: " + ex.Message);
                // لو حصل أى خطأ → نرجع PDF نفسه عشان مايحصلش كراش
                return pdfPath;
            }
        }

        private List<string> ConvertPdfToJpeg_MultiPage(string pdfPath)
        {
            var outputImages = new List<string>();

            try
            {
                if (!File.Exists(pdfPath)) return outputImages;

                string gsExe = GetGhostscriptPath();
                if (string.IsNullOrEmpty(gsExe) || !File.Exists(gsExe)) return outputImages;

                string tempDir = Path.Combine(Path.GetTempPath(), "PdfToImage");
                Directory.CreateDirectory(tempDir);

                string outputPattern = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(pdfPath) + "_page_%03d.jpg");

                // ✅ تعديل جوهري: تم تغيير الدقة من -r300 إلى -r203
                // 203 DPI هي الدقة القياسية للطابعات الحرارية، هذا يقلل حجم الملف للنصف ويسرع المعالجة جداً
                string args = "-dNOPAUSE -dBATCH -sDEVICE=jpeg -r203 -dJPEGQ=100 " +
                              $"-sOutputFile=\"{outputPattern}\" \"{pdfPath}\"";

                var psi = new ProcessStartInfo(gsExe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                }

                var files = Directory.GetFiles(tempDir, Path.GetFileNameWithoutExtension(pdfPath) + "_page_*.jpg")
                                     .OrderBy(f => f)
                                     .ToList();

                outputImages.AddRange(files);
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ فى تحويل PDF لصور متعددة: " + ex.Message);
            }

            return outputImages;
        }


        // =================== محرك Pdfium (Win 10/11) ===================
        private string ConvertPdfToJpeg_Pdfium(string pdfPath)
        {
            if (!File.Exists(pdfPath))
            {
                Log("⚠️ ملف PDF غير موجود: " + pdfPath);
                return pdfPath;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfToImage");
            Directory.CreateDirectory(tempDir);

            using (var document = PdfiumViewer.PdfDocument.Load(pdfPath))
            {
                int pageIndex = 0;
                int targetWidth = 1240;
                int targetHeight = 1754;

                using (var rendered = document.Render(pageIndex, targetWidth, targetHeight, true))
                using (Bitmap bmp = new Bitmap(rendered))
                {
                    bmp.SetResolution(150f, 150f);

                    string outputPath = Path.Combine(
                        tempDir,
                        Path.GetFileNameWithoutExtension(pdfPath) + "_img.jpg");

                    bmp.Save(outputPath, ImageFormat.Jpeg);

                    Log("🖼️ [Pdfium] تم تحويل PDF إلى صورة: " + Path.GetFileName(outputPath));
                    return outputPath;
                }
            }
        }

        // =================== محرك Ghostscript (Win 7) ===================
        private string ConvertPdfToJpeg_Ghostscript(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return pdfPath;

            string gsExe = GetGhostscriptPath();
            if (string.IsNullOrEmpty(gsExe) || !File.Exists(gsExe)) return pdfPath;

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfToImage");
            Directory.CreateDirectory(tempDir);

            string rawJpeg = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(pdfPath) + "_gs_raw.jpg");

            // ✅ التعديل هنا: -r300 وجودة JPEGQ=100
            string args = string.Format(
                "-dNOPAUSE -dBATCH -sDEVICE=jpeg -r300 -dFirstPage=1 -dLastPage=1 -dJPEGQ=100 " +
                "-sOutputFile=\"{0}\" \"{1}\"",
                rawJpeg, pdfPath);

            try
            {
                var psi = new ProcessStartInfo(gsExe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null) proc.WaitForExit(20000);
                }
            }
            catch { return pdfPath; }

            if (!File.Exists(rawJpeg)) return pdfPath;

            // بما أن الدقة زادت، قد لا نحتاج لإعادة التحجيم (Resize) إلا لو أردت توحيد المقاس
            // سأرجع الصورة عالية الجودة مباشرة
            return rawJpeg;
        }

        // دالة جديدة لإضافة هوامش بيضاء حول الصورة
        private string AddPaddingToImage(string imagePath, int paddingPixels)
        {
            try
            {
                using (Image original = Image.FromFile(imagePath))
                {
                    // الأبعاد الجديدة = الأبعاد القديمة + الهوامش من كل الجهات
                    int newWidth = original.Width + (paddingPixels * 2);
                    int newHeight = original.Height + (paddingPixels * 2);

                    using (Bitmap newBitmap = new Bitmap(newWidth, newHeight))
                    {
                        // ضبط الدقة لتكون مثل الأصلية (مهم جداً للطباعة)
                        newBitmap.SetResolution(original.HorizontalResolution, original.VerticalResolution);

                        using (Graphics g = Graphics.FromImage(newBitmap))
                        {
                            // خلفية بيضاء
                            g.Clear(Color.White);

                            // إعدادات جودة عالية
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;

                            // رسم الصورة الأصلية في المنتصف
                            g.DrawImage(original, paddingPixels, paddingPixels, original.Width, original.Height);
                        }

                        string dir = Path.GetDirectoryName(imagePath);
                        string fileName = Path.GetFileNameWithoutExtension(imagePath);
                        string ext = Path.GetExtension(imagePath);
                        string newPath = Path.Combine(dir, fileName + "_padded" + ext);

                        newBitmap.Save(newPath, ImageFormat.Jpeg);
                        return newPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("⚠️ فشل إضافة هوامش للصورة: " + ex.Message);
                return imagePath;
            }
        }

        private string GetGhostscriptPath()
        {
            try
            {
                // نقرأ من ملف الإعدادات لو موجود
                string configPath = Path.Combine(Application.StartupPath, GhostscriptPathConfigFile);

                if (File.Exists(configPath))
                {
                    string exePath = File.ReadAllText(configPath).Trim();

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        Log("✔ استخدام Ghostscript من الملف: " + exePath);
                        return exePath;
                    }
                    else
                    {
                        Log("⚠️ المسار الموجود فى ghostscript.path.txt غير صالح: " + exePath);
                    }
                }
                else
                {
                    Log("ℹ️ ملف ghostscript.path.txt غير موجود بعد، استخدم قائمة الإعدادات لتحديد المسار.");
                }
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ أثناء قراءة مسار Ghostscript من الملف: " + ex.Message);
            }

            return null;
        }

        // ===================== Auto Close D:\PDF\ID Windows =====================

        private void EnableAutoCloseHook()
        {
            if (winEventHookHandle != IntPtr.Zero)
                return; // شغال بالفعل

            winEventDelegate = new WinEventDelegate(WinEventCallback);

            winEventHookHandle = SetWinEventHook(
                EVENT_OBJECT_CREATE,
                EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT);

            Log("🟢 تم تفعيل إغلاق فولدر المريض");
        }

        private void DisableAutoCloseHook()
        {
            if (winEventHookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(winEventHookHandle);
                winEventHookHandle = IntPtr.Zero;
                winEventDelegate = null;
                Log("⚪ تم إيقاف إغلاق فولدر المريض");
            }
        }

        // ده بينادى أوتوماتيك أول ما أى نافذة جديدة تتخلق / تظهر
        // ده بينادى أوتوماتيك أول ما أى نافذة جديدة تتخلق / تظهر
        private void WinEventCallback(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (!autoClosePdfFoldersEnabled)
                return;

            // نتأكد إن الحدث على النافذة نفسها
            if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW)
                return;

            // نهتم بس بأحداث إنشاء/إظهار النافذة
            if (eventType != EVENT_OBJECT_CREATE && eventType != EVENT_OBJECT_SHOW)
                return;

            try
            {
                // أول فلتر سريع: لازم تكون نافذة Explorer
                var className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string cls = className.ToString();

                // Win10: CabinetWClass
                // Win7 : CabinetWClass أو ExploreWClass
                if (cls != "CabinetWClass" && cls != "ExploreWClass")
                    return;

                // ❗ المهم: نسيب Explorer يلحق يغيّر العنوان لاسم الفولدر
                Task.Run(() =>
                {
                    try
                    {
                        // ننتظر لحظة صغيرة (ربع ثانية كفاية)
                        Thread.Sleep(250);

                        var title = new StringBuilder(512);
                        GetWindowText(hwnd, title, title.Capacity);
                        string windowTitle = title.ToString();

                        if (IsPatientIdTitle(windowTitle))
                        {
                            // نقفلها فوراً بعد ما العنوان يبقى ID المريض
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            Log("📁 تم إغلاق فولدر المريض تلقائيًا: " + windowTitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("⚠️ خطأ في WinEventCallback/Task: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ في WinEventCallback (خارجي): " + ex.Message);
            }
        }

        private bool IsPatientIdTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return false;

            title = title.Trim();

            // أمثلة عناوين محتملة:
            // "1025120210015"
            // "1025120210015 - Windows Explorer"
            // "1025120210015 - مستكشف Windows"
            // "1025120210015 - أي حاجة"

            // ناخد الجزء اللي في البداية لحد أول حرف مش رقم
            int i = 0;
            while (i < title.Length && char.IsDigit(title[i]))
                i++;

            if (i == 0)
                return false; // العنوان مش بادئ برقَم أصلاً

            string idPart = title.Substring(0, i);

            // نعتبره ID مريض لو طوله معقول (زي IDs الطويلة عندك)
            return idPart.Length >= 8;
        }

        // ================== Receipt Bridge: تهيئة الـ FileSystemWatcher ==================

        private void InitializeReceiptBridgeFromSettings()
        {
            try
            {
                StopReceiptWatcher();

                if (!Properties.Settings.Default.ReceiptBridge_Enabled)
                {
                    Log("ℹ️ كوبرى الإيصالات غير مفعَّل من الإعدادات.");
                    return;
                }

                string folder = Properties.Settings.Default.ReceiptBridge_InputFolder;
                string printerName = Properties.Settings.Default.ReceiptBridge_ThermalPrinter;

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    Log("⚠️ مجلد الإيصالات غير مضبوط أو غير موجود. يرجى مراجعته من إعدادات إرسال الإيصالات.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    Log("⚠️ لم يتم اختيار طابعة إيصالات. يرجى مراجعته من إعدادات إرسال الإيصالات.");
                    return;
                }

                receiptWatcher = new FileSystemWatcher(folder, "*.pdf");
                receiptWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
                receiptWatcher.Created += ReceiptWatcher_Created;
                receiptWatcher.EnableRaisingEvents = true;

                Log("🟢 تم تفعيل كوبرى الإيصالات لمجلد: " + folder);
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء تهيئة كوبرى الإيصالات: " + ex.Message);
            }
        }

        private void StopReceiptWatcher()
        {
            try
            {
                if (receiptWatcher != null)
                {
                    receiptWatcher.EnableRaisingEvents = false;
                    receiptWatcher.Created -= ReceiptWatcher_Created;
                    receiptWatcher.Dispose();
                    receiptWatcher = null;
                }
            }
            catch
            {
                // تجاهل أى خطأ فى الإيقاف
            }
        }

        private void ToggleSendReceiptsToWhatsApp(bool enabled, bool fromTray)
        {
            // لو الكوبرى نفسه مش مفعّل
            if (enabled && !Properties.Settings.Default.ReceiptBridge_Enabled)
            {
                MessageBox.Show(
                    this,
                    "لا يمكن استخدام هذا الخيار قبل تفعيل كوبرى إرسال الإيصالات من \"إعدادات إرسال الإيصالات\".",
                    "تنبيه",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

                // رجّع العلامة في المكان اللى ضغط منه
                if (fromTray && traySendReceiptsMenuItem != null)
                    traySendReceiptsMenuItem.Checked = false;

                if (!fromTray && sendReceiptsToWhatsAppMenuItem != null)
                    sendReceiptsToWhatsAppMenuItem.Checked = false;

                return;
            }

            // مزامنة المينيو الرئيسية مع الـ Tray
            if (sendReceiptsToWhatsAppMenuItem != null)
                sendReceiptsToWhatsAppMenuItem.Checked = enabled;

            if (traySendReceiptsMenuItem != null)
                traySendReceiptsMenuItem.Checked = enabled;

            Properties.Settings.Default.ReceiptBridge_SendToWhatsApp = enabled;
            Properties.Settings.Default.Save();

            Log(enabled
                ? "📲 سيتم إرسال الإيصالات على واتس آب عند استقبال إيصال جديد."
                : "🚫 تم إيقاف إرسال الإيصالات على واتس آب.");
        }

        private void TogglePrintReceiptsOnPrinter(bool enabled, bool fromTray)
        {
            // لو الكوبرى نفسه مش مفعّل
            if (enabled && !Properties.Settings.Default.ReceiptBridge_Enabled)
            {
                MessageBox.Show(
                    this,
                    "لا يمكن استخدام هذا الخيار قبل تفعيل كوبرى إرسال الإيصالات من \"إعدادات إرسال الإيصالات\".",
                    "تنبيه",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

                if (fromTray && trayPrintReceiptsMenuItem != null)
                    trayPrintReceiptsMenuItem.Checked = false;

                if (!fromTray && printReceiptsOnPrinterMenuItem != null)
                    printReceiptsOnPrinterMenuItem.Checked = false;

                return;
            }

            if (printReceiptsOnPrinterMenuItem != null)
                printReceiptsOnPrinterMenuItem.Checked = enabled;

            if (trayPrintReceiptsMenuItem != null)
                trayPrintReceiptsMenuItem.Checked = enabled;

            Properties.Settings.Default.ReceiptBridge_PrintOnPrinter = enabled;
            Properties.Settings.Default.Save();

            Log(enabled
                ? "🖨️ سيتم طباعة الإيصالات على الطابعة الحرارية من الكوبرى."
                : "🚫 تم إيقاف طباعة الإيصالات على الطابعة الحرارية من الكوبرى.");
        }


        private void ReceiptWatcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (receiptLock)
            {
                if (processedReceiptFiles.Contains(e.FullPath))
                    return;

                processedReceiptFiles.Add(e.FullPath);
            }

            // معالجة فى الخلفية عشان ما نعلّقش الـ UI
            Task.Run(() => HandleNewReceiptPdf(e.FullPath));
        }

        private void HandleNewReceiptPdf(string pdfPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pdfPath)) return;
                WaitForFileReady(pdfPath);
                if (!System.IO.File.Exists(pdfPath)) return;

                Log("🧾 تم التقاط إيصال جديد: " + pdfPath);

                bool sendToWhatsApp = Properties.Settings.Default.ReceiptBridge_SendToWhatsApp;
                bool printOnPrinter = Properties.Settings.Default.ReceiptBridge_PrintOnPrinter;
                string paperSize = Properties.Settings.Default.ReceiptBridge_PaperSize;

                if (!sendToWhatsApp && !printOnPrinter) return;

                // 1) PDF -> Images
                var rawPages = ConvertPdfToJpeg_MultiPage(pdfPath);
                if (rawPages == null || rawPages.Count == 0) return;

                var pagesForThermal = new System.Collections.Generic.List<string>();

                foreach (var rawPage in rawPages)
                {
                    string cropped = AutoCropReceiptImage(rawPage);
                    if (!string.IsNullOrEmpty(cropped) && System.IO.File.Exists(cropped))
                        pagesForThermal.Add(cropped);
                    else
                        pagesForThermal.Add(rawPage);
                }

                // 2) Read patientId (Barcode)
                string patientId = null;
                for (int i = pagesForThermal.Count - 1; i >= 0; i--)
                {
                    string id = TryReadBarcodeFromImage(pagesForThermal[i]);
                    if (!string.IsNullOrEmpty(id)) { patientId = id; break; }
                }

                // ✅ صحّي الـ Worker فورًا (بدون أي انتظار للطباعة)
                try { resultsLinkWorker?.WakeUpNow(); } catch { }

                // 3) Lookup folderUrl from DB
                string folderUrl = null;
                ResultsLinkSettings rl = null;

                try
                {
                    rl = ResultsLinkSettings.Load();

                    // ✅ الشرط الجديد: إذا كان الخيار مفعلاً بشكل عام، وخيار الواتس مفعل (اختياري)، والـ QR مفعل
                    if (rl != null && rl.Enabled && rl.SendLinkOnWhatsApp && !string.IsNullOrWhiteSpace(patientId))
                    {
                        folderUrl = ResultsLinkQueueWorker.LookupFolderUrl(rl, patientId);
                    }
                }
                catch { }

                // 4) Add QR only if link exists AND user enabled QR option
                // ✅✅ التعديل هنا: إضافة شرط rl.AddQrCodeToReceipt
                if (!string.IsNullOrWhiteSpace(folderUrl) && rl != null && rl.AddQrCodeToReceipt)
                {
                    AddQrToLastReceiptPage(pagesForThermal, folderUrl, rl.ReceiptQrCaption);
                    Log("✅ تم إضافة QR Code للإيصال.");
                }
                else
                {
                    Log("ℹ️ لن يتم إضافة QR Code (إما اللينك غير جاهز أو الخيار معطل).");
                }

                // ========================================================================
                // 🚀 الطباعة السريعة
                // ========================================================================
                if (printOnPrinter && paperSize != "A4")
                {
                    Log("🖨️ جاري إرسال الأمر للطابعة فوراً...");

                    int printerPixelWidth = (paperSize == "58mm") ? 384 : 576;
                    string merged = CombineReceiptPagesIntoOne(pagesForThermal, printerPixelWidth);

                    if (!string.IsNullOrEmpty(merged) && System.IO.File.Exists(merged))
                        FastPrintReceiptImage(merged);
                    else
                        foreach (var page in pagesForThermal) FastPrintReceiptImage(page);
                }

                // لو مش محتاجين واتساب ولا A4 نخرج
                if (!sendToWhatsApp && paperSize != "A4") return;

                // تجهيز نسخة الواتساب و A4
                var pagesForWhatsApp = new System.Collections.Generic.List<string>();
                foreach (var p in pagesForThermal)
                {
                    string padded = AddPaddingToImage(p, 40);
                    if (!string.IsNullOrEmpty(padded) && System.IO.File.Exists(padded))
                        pagesForWhatsApp.Add(padded);
                    else
                        pagesForWhatsApp.Add(p);
                }

                // طباعة A4
                if (printOnPrinter && paperSize == "A4")
                {
                    Log("🖨️ جاري الطباعة (A4)...");
                    string merged = CombineReceiptPagesIntoOne(pagesForWhatsApp);
                    if (!string.IsNullOrEmpty(merged) && System.IO.File.Exists(merged))
                        FastPrintReceiptImage(merged);
                }

                // إرسال الواتساب
                if (sendToWhatsApp)
                {
                    if (string.IsNullOrEmpty(patientId))
                    {
                        Log("⚠️ لم يتم العثور على باركود، لن يتم الإرسال للواتس.");
                        return;
                    }

                    string rawPhone = LookupPatientPhoneById(patientId);
                    if (!string.IsNullOrWhiteSpace(rawPhone))
                    {
                        string fullPhone = BuildFullPhoneFromLocal(rawPhone);
                        Log("📞 جاري الإرسال للواتس: " + fullPhone);

                        // ✅ لازم نمرر folderUrl هنا (بدون إعادة قراءة باركود من الصور)
                        SendReceiptImagesViaWhatsAppSafe(fullPhone, pagesForWhatsApp, folderUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("❌ خطأ معالجة الإيصال: " + ex.Message);
            }
        }


        private string GetResultsLinkSqlConnectionString(ResultsLinkSettings s)
        {
            // ✅ السيرفر من BASE.ini -> [lastlog] -> CORTOBA-PC
            // ✅ اليوزر/الباس ثابتين زي ما طلبت: sa / 12345678
            if (s == null) throw new Exception("ResultsLinkSettings غير موجودة");

            string basePath = s.BaseIniPath;

            if (string.IsNullOrWhiteSpace(basePath) || !System.IO.File.Exists(basePath))
            {
                basePath = BaseIniSqlConnectionBuilder.FindBaseIniPath();
                if (string.IsNullOrWhiteSpace(basePath))
                    throw new Exception("لم يتم العثور على ملف BASE. افتح إعدادات لينك النتائج واختر ملف BASE.");

                s.BaseIniPath = basePath;
                try { s.Save(); } catch { }
            }

            return BaseIniSqlConnectionBuilder.BuildSqlConnectionStringOrThrow(basePath, "sa", "12345678");
        }

        private void SendReceiptImagesViaWhatsAppSafe(string fullPhone, List<string> images, string folderUrl)
        {
            Task.Run(async () =>
            {
                await SendReceiptImagesViaWhatsAppInternal(fullPhone, images, folderUrl);
            });
        }


        private async Task SendReceiptImagesViaWhatsAppInternal(string fullPhone, List<string> images, string folderUrl)
        {
            try
            {
                // =========================================================================
                // ✅ التعديل الجديد: دعم الطريقة 3 (WebView2 Pro) مع Invoke
                // =========================================================================
                if (selectedSendMethod == 3)
                {
                    // 1. التأكد من تشغيل المتصفح (UI Thread)
                    this.Invoke(new Action(() => ManageWebViewState()));

                    // انتظار الجاهزية
                    int attempts = 0;
                    while (attempts < 40 && (webViewForm == null || !webViewForm.IsReady))
                    {
                        await Task.Delay(250);
                        attempts++;
                    }

                    if (webViewForm == null || !webViewForm.IsReady)
                    {
                        Log("❌ WebView2 غير جاهز. تأكد من فتح واتساب مرة واحدة على الأقل.");
                        return;
                    }

                    // 2. تحديث الرقم
                    currentPhoneNumber = fullPhone;

                    // 3. إرسال النص (اللينك) عبر Invoke
                    try
                    {
                        var rl = ResultsLinkSettings.Load();
                        if (rl != null && rl.Enabled && rl.SendLinkOnWhatsApp && !string.IsNullOrWhiteSpace(folderUrl))
                        {
                            string msg = (rl.WhatsAppMessagePrefix ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(msg)) msg = "📌 لينك النتائج:";
                            string finalMsg = msg + "\n" + folderUrl;

                            this.Invoke(new Action(() =>
                            {
                                _ = webViewForm.SendTextWppAsync(fullPhone, finalMsg);
                            }));

                            Log($"✅ (Pro) تم إرسال لينك النتائج.");
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("⚠️ (Pro) فشل إرسال اللينك: " + ex.Message);
                    }

                    // 4. إرسال الصور عبر Invoke
                    foreach (var img in images)
                    {
                        if (!System.IO.File.Exists(img)) continue;

                        this.Invoke(new Action(() =>
                        {
                            webViewForm.SendFile(fullPhone, img);
                        }));

                        Log($"📨 (Pro) تم توجيه صفحة الإيصال للإرسال: {System.IO.Path.GetFileName(img)}");
                        await Task.Delay(2000); // وقت كافي للمعالجة
                    }

                    Log("📲 (Pro) تم الانتهاء من إرسال الإيصال.");
                    return; // 🛑 خروج
                }

                // =========================================================================
                // ⛔ الكود القديم (Selenium - Method 2)
                // =========================================================================
                await EnsureDriverRunningAsync();

                if (!IsDriverRunning())
                {
                    Log("❌ ChromeDriver غير جاهز.");
                    return;
                }

                if (selectedSendMethod != 2) selectedSendMethod = 2;
                currentPhoneNumber = fullPhone;

                // إرسال اللينك
                try
                {
                    var rl = ResultsLinkSettings.Load();
                    if (rl != null && rl.Enabled && rl.SendLinkOnWhatsApp && !string.IsNullOrWhiteSpace(folderUrl))
                    {
                        string msg = (rl.WhatsAppMessagePrefix ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(msg)) msg = "📌 لينك النتائج:";
                        string final = msg + "\n" + folderUrl;
                        SendTextViaWppConnect(fullPhone, final);
                        await Task.Delay(600);
                        Log("✅ تم إرسال لينك النتائج (Direct).");
                    }
                }
                catch { }

                // إرسال الصور
                foreach (var img in images)
                {
                    SendFileOnlyViaWppConnect(img);
                    Log("📨 تم إرسال صفحة: " + img);
                    await Task.Delay(800);
                }

                Log("📲 تم إرسال جميع صفحات الإيصال.");
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء إرسال الصور: " + ex.Message);
            }
        }

        private void SendTextViaWppConnect(string fullPhone, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullPhone) || string.IsNullOrWhiteSpace(text))
                    return;

                if (driver == null) return;

                string jid = fullPhone.EndsWith("@c.us") ? fullPhone : fullPhone + "@c.us";

                // ✅ Escape شامل: backslash + single quote + CR/LF + tab
                string safe = text
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\r", "")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");

                string js = $@"
(async () => {{
    if (!window.WPP || !WPP.chat) {{
        console.warn('❌ WPP غير جاهز');
        return;
    }}
    try {{
        await WPP.chat.sendTextMessage('{jid}', '{safe}', {{ createChat: true }});
        console.log('✅ تم إرسال رسالة نصية');
    }} catch (err) {{
        console.error('❌ فشل إرسال النص:', err);
    }}
}})();";

                ((OpenQA.Selenium.IJavaScriptExecutor)driver).ExecuteScript(js);

                Log("✅ تم طلب إرسال رسالة نصية على واتساب.");
            }
            catch (Exception ex)
            {
                Log("❌ خطأ داخل SendTextViaWppConnect: " + ex.Message);
            }
        }

        private void AddQrToLastReceiptPage(System.Collections.Generic.List<string> pages, string url, string caption)
        {
            if (pages == null || pages.Count == 0) return;
            if (string.IsNullOrWhiteSpace(url)) return;

            string last = pages[pages.Count - 1];
            if (!System.IO.File.Exists(last)) return;

            try
            {
                using (var bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(last))
                {
                    int qrSize = Math.Max(160, bmp.Width / 4);
                    var qr = RenderQrBitmap(url, qrSize, qrSize);

                    int extraHeight = qr.Height + 70; // مساحة النص
                    using (var outBmp = new System.Drawing.Bitmap(bmp.Width, bmp.Height + extraHeight))
                    using (var g = System.Drawing.Graphics.FromImage(outBmp))
                    {
                        g.Clear(System.Drawing.Color.White);
                        g.DrawImage(bmp, 0, 0);

                        int qrX = (outBmp.Width - qr.Width) / 2;
                        int qrY = bmp.Height + 10;
                        g.DrawImage(qr, qrX, qrY);

                        // caption
                        if (!string.IsNullOrWhiteSpace(caption))
                        {
                            using (var f = new System.Drawing.Font("Tahoma", 12, System.Drawing.FontStyle.Bold))
                            using (var br = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                            {
                                var rect = new System.Drawing.RectangleF(10, qrY + qr.Height + 8, outBmp.Width - 20, 50);
                                var sf = new System.Drawing.StringFormat
                                {
                                    Alignment = System.Drawing.StringAlignment.Center,
                                    LineAlignment = System.Drawing.StringAlignment.Near
                                };
                                g.DrawString(caption, f, br, rect, sf);
                            }
                        }

                        string dir = System.IO.Path.GetDirectoryName(last);
                        string name = System.IO.Path.GetFileNameWithoutExtension(last);
                        string newPath = System.IO.Path.Combine(dir, name + "_qr.jpg");

                        outBmp.Save(newPath, System.Drawing.Imaging.ImageFormat.Jpeg);

                        pages[pages.Count - 1] = newPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("⚠️ فشل إضافة QR على الإيصال: " + ex.Message);
            }
        }

        private System.Drawing.Bitmap RenderQrBitmap(string text, int width, int height)
        {
            // ZXing موجود عندك بالفعل (بتستخدمه في قراءة الباركود)
            var writer = new ZXing.BarcodeWriter
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 1
                }
            };
            return writer.Write(text);
        }


        private string AutoCropReceiptImage(string imagePath)
        {
            try
            {
                using (var original = (Bitmap)Image.FromFile(imagePath))
                {
                    int width = original.Width;
                    int height = original.Height;

                    int top = 0;
                    int bottom = height - 1;
                    int left = 0;
                    int right = width - 1;

                    bool IsWhite(Color c)
                    {
                        return c.R > 245 && c.G > 245 && c.B > 245;
                    }

                    // أعلى
                    for (int y = 0; y < height; y++)
                    {
                        bool allWhite = true;
                        for (int x = 0; x < width; x++)
                        {
                            if (!IsWhite(original.GetPixel(x, y)))
                            {
                                allWhite = false;
                                break;
                            }
                        }
                        if (!allWhite)
                        {
                            top = y;
                            break;
                        }
                    }

                    // أسفل
                    for (int y = height - 1; y >= 0; y--)
                    {
                        bool allWhite = true;
                        for (int x = 0; x < width; x++)
                        {
                            if (!IsWhite(original.GetPixel(x, y)))
                            {
                                allWhite = false;
                                break;
                            }
                        }
                        if (!allWhite)
                        {
                            bottom = y;
                            break;
                        }
                    }

                    // يسار
                    for (int x = 0; x < width; x++)
                    {
                        bool allWhite = true;
                        for (int y = top; y <= bottom; y++)
                        {
                            if (!IsWhite(original.GetPixel(x, y)))
                            {
                                allWhite = false;
                                break;
                            }
                        }
                        if (!allWhite)
                        {
                            left = x;
                            break;
                        }
                    }

                    // يمين
                    for (int x = width - 1; x >= 0; x--)
                    {
                        bool allWhite = true;
                        for (int y = top; y <= bottom; y++)
                        {
                            if (!IsWhite(original.GetPixel(x, y)))
                            {
                                allWhite = false;
                                break;
                            }
                        }
                        if (!allWhite)
                        {
                            right = x;
                            break;
                        }
                    }

                    int cropWidth = right - left + 1;
                    int cropHeight = bottom - top + 1;

                    // لو القص غريب نرجّع الأصل
                    if (cropWidth <= 0 || cropHeight <= 0 ||
                        cropWidth > width || cropHeight > height)
                    {
                        Log("⚠️ لم يتم القص لأن الحدود غير منطقية، سيتم استخدام الصورة الأصلية.");
                        return imagePath;
                    }

                    Rectangle cropRect = new Rectangle(left, top, cropWidth, cropHeight);
                    using (var cropped = new Bitmap(cropRect.Width, cropRect.Height))
                    {
                        using (Graphics g = Graphics.FromImage(cropped))
                        {
                            g.DrawImage(original, new Rectangle(0, 0, cropped.Width, cropped.Height),
                                cropRect, GraphicsUnit.Pixel);
                        }

                        string dir = Path.GetDirectoryName(imagePath);
                        string fileName = Path.GetFileNameWithoutExtension(imagePath);
                        string ext = Path.GetExtension(imagePath);

                        string newPath = Path.Combine(dir, fileName + "_cropped" + ext);
                        cropped.Save(newPath, System.Drawing.Imaging.ImageFormat.Jpeg);

                        return newPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ أثناء قص صورة الإيصال: " + ex.Message);
                return imagePath;
            }
        }

        private string TryReadBarcodeFromImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    Log("⚠️ لا يمكن قراءة الباركود لأن ملف الصورة غير موجود: " + imagePath);
                    return null;
                }

                var reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        TryInverted = true,   // الشكل الجديد الموصى به
                        PossibleFormats = new List<BarcodeFormat>
                {
                    BarcodeFormat.CODE_128,
                    BarcodeFormat.QR_CODE,
                    BarcodeFormat.CODE_39,
                    BarcodeFormat.EAN_13,
                    BarcodeFormat.EAN_8,
                    BarcodeFormat.PDF_417,
                    BarcodeFormat.CODE_93
                }
                    }
                };

                using (var bmp = (Bitmap)Image.FromFile(imagePath))
                {
                    var result = reader.Decode(bmp);   // دى دلوقتى شغالة عادى مع Bitmap
                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        return result.Text.Trim();
                    }
                }

                Log("⚠️ لم يتم العثور على باركود فى صورة الإيصال.");
                return null;
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ أثناء قراءة الباركود: " + ex.Message);
                return null;
            }
        }

        private string BuildRealLabConnectionStringFromIni()
        {
            try
            {
                // غالباً BASE.ini موجود جنب Real Lab (أحياناً جنب برنامجنا)
                // جرّبنا أكتر من مكان شائع
                string baseIniPath1 = Path.Combine(Application.StartupPath, "BASE.ini");
                string baseIniPath2 = Path.Combine(@"D:\real lab system\bin", "BASE.ini"); // زي الصورة اللي عندك
                string baseIniPath = File.Exists(baseIniPath1) ? baseIniPath1 : baseIniPath2;

                if (!File.Exists(baseIniPath))
                {
                    Log("⚠️ BASE.ini غير موجود. حطّه جنب البرنامج أو عدّل المسار.");
                    return null;
                }

                string[] lines = File.ReadAllLines(baseIniPath);

                // default catalog
                string initialCatalog = "Patients";

                // server from [lastlog] try1
                string try1Value = null;
                bool inLastLog = false;

                foreach (var raw in lines)
                {
                    string line = raw.Trim();

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inLastLog = line.Equals("[lastlog]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inLastLog) continue;

                    // try1=....
                    if (line.StartsWith("try1", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf('=');
                        if (idx > -1)
                            try1Value = line.Substring(idx + 1).Trim();
                        break;
                    }
                }

                string serverName = ".";

                // try1 = التاريخ##نوع القاعدة##اسم السيرفر##رقم##اليوزر##الباسورد
                if (!string.IsNullOrWhiteSpace(try1Value))
                {
                    string[] parts = try1Value.Split(new string[] { "##" }, StringSplitOptions.None);
                    if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                        serverName = parts[2].Trim();
                }

                if (string.IsNullOrWhiteSpace(serverName))
                    serverName = ".";

                // ✅ المطلوب: user/pass ثابتين
                string dbUser = "sa";
                string dbPass = "12345678";

                string cs = $"Data Source={serverName};Initial Catalog={initialCatalog};User ID={dbUser};Password={dbPass};";

                Log("🟢 تم تكوين اتصال Real Lab (السيرفر: " + serverName + ", القاعدة: " + initialCatalog + ")");
                return cs;
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء تكوين ConnectionString من BASE.ini: " + ex.Message);
                return null;
            }
        }


        private string LookupPatientPhoneById(string patientId)
        {
            try
            {
                string cs = BuildRealLabConnectionStringFromIni();
                if (string.IsNullOrWhiteSpace(cs))
                {
                    Log("⚠️ تعذر تكوين اتصال بقاعدة بيانات Real Lab.");
                    return null;
                }

                using (var conn = new SqlConnection(cs))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();

                    cmd.CommandText = @"
        SELECT patientphone, patienttel, SMSMob
        FROM patientinfo
        WHERE patientid = @id";

                    cmd.Parameters.AddWithValue("@id", patientId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        string patientPhone = reader["patientphone"] as string;
                        string patientTel = reader["patienttel"] as string;
                        string smsMob = reader["SMSMob"] as string;

                        if (!string.IsNullOrEmpty(patientPhone)) patientPhone = patientPhone.Trim();
                        if (!string.IsNullOrEmpty(patientTel)) patientTel = patientTel.Trim();
                        if (!string.IsNullOrEmpty(smsMob)) smsMob = smsMob.Trim();

                        // =========================================================
                        // ✅ التعديل المطلوب: عكس الأولويات
                        // الرقم الأساسي (رقم 1) أصبح الآن patientTel
                        // =========================================================

                        // 1. الأولوية الأولى: patientTel
                        string primaryChoice = !string.IsNullOrWhiteSpace(patientTel) ? patientTel : null;

                        // 2. الأولوية الثانية: patientPhone (ولو فاضي نشوف SMSMob)
                        string secondaryChoice = !string.IsNullOrWhiteSpace(patientPhone)
                                        ? patientPhone
                                        : (!string.IsNullOrWhiteSpace(smsMob) ? smsMob : null);

                        // لو الرقم "الثاني" (اللي خليناه أساسي) موجود نستخدمه.. لو مش موجود نرجع للقديم
                        return primaryChoice ?? secondaryChoice;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ أثناء جلب رقم المريض من قاعدة البيانات: " + ex.Message);
                return null;
            }
        }

        // بناء رقم الواتساب الكامل من رقم محلى
        private string BuildFullPhoneFromLocal(string rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
                return null;

            // نحتفظ بالأرقام فقط
            string digits = Regex.Replace(rawPhone, "[^0-9]", "");
            if (string.IsNullOrEmpty(digits))
                return null;

            // لو الرقم يبدأ بـ 0 نشيله
            if (digits.StartsWith("0") && digits.Length > 1)
                digits = digits.Substring(1);

            // كود الدولة من TextBox أو من قيمة افتراضية
            string code = "20"; // مصر
            if (txtCountryCode != null)
            {
                string txt = txtCountryCode.Text.Trim();
                if (!string.IsNullOrEmpty(txt) && txt.All(char.IsDigit))
                    code = txt;
            }

            return code + digits; // بدون + ولا @
        }

        // ================== إرسال إيصال الواتساب من الكوبرى ==================

        private void SendReceiptImageViaWhatsAppSafe(string fullPhone, string imagePath)
        {
            try
            {
                // نشغّل عملية الإرسال كلها فى Thread خلفى
                Task.Run(async () =>
                {
                    await SendReceiptImageViaWhatsAppInternalAsync(fullPhone, imagePath);
                });
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ فى استدعاء إرسال واتساب (Safe): " + ex.Message);
            }
        }

        private async Task SendReceiptImageViaWhatsAppInternalAsync(string fullPhone, string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullPhone))
                {
                    Log("⚠️ رقم الهاتف للإرسال غير صالح.");
                    return;
                }

                if (!System.IO.File.Exists(imagePath))
                {
                    Log("⚠️ ملف صورة الإيصال غير موجود للإرسال على واتساب: " + imagePath);
                    return;
                }

                // =========================================================================
                // ✅ التعديل الجديد: دعم الطريقة 3 (WebView2 Pro) للإيصال الفردي
                // =========================================================================
                if (selectedSendMethod == 3)
                {
                    // 1. تشغيل وتجهيز المتصفح
                    this.Invoke(new Action(() => ManageWebViewState()));

                    if (webViewForm == null || !webViewForm.IsReady)
                    {
                        bool ready = await EnsureWebViewReadyAsync(5000);
                        if (!ready) { Log("❌ WebView2 غير جاهز للإرسال الفردي."); return; }
                    }

                    // 2. ضبط الرقم
                    currentPhoneNumber = fullPhone;

                    // 3. الإرسال
                    string res = await webViewForm.SendFileWppAsync(fullPhone, imagePath);
                    Log($"✅ (Pro) تم إرسال إيصال واتساب تلقائياً: {res}");
                    return; // 🛑 خروج
                }

                // =========================================================================
                // ⛔ الكود القديم (Selenium)
                // =========================================================================
                await EnsureDriverRunningAsync();

                if (!IsDriverRunning())
                {
                    Log("❌ لا يمكن الإرسال على واتساب لأن المتصفح غير جاهز.");
                    return;
                }

                if (selectedSendMethod != 2)
                {
                    Log("ℹ️ سيتم استخدام طريقة WPPConnect (2) للإرسال التلقائى.");
                    selectedSendMethod = 2;
                }

                currentPhoneNumber = fullPhone;

                // إرسال صورة الإيصال (Selenium)
                SendFileOnlyViaWppConnect(imagePath);

                Log("✅ تم إرسال إيصال واتساب تلقائياً إلى: " + fullPhone);
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء إرسال الإيصال عبر واتساب: " + ex.Message);
            }
        }

        private void WaitForFileReady(string path)
        {
            const int maxAttempts = 20;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (stream.Length > 0)
                        {
                            return;
                        }
                    }
                }
                catch (IOException)
                {
                    // الملف مازال يُكتب عليه
                }
                catch (UnauthorizedAccessException)
                {
                }

                Thread.Sleep(500);
            }
        }

        // ================== Receipt Bridge: الطباعة السريعة على الطابعة الحرارية ==================

        private void FastPrintReceiptImage(string imagePath)
        {
            try
            {
                string printerName = Properties.Settings.Default.ReceiptBridge_ThermalPrinter;
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    Log("⚠️ لم يتم ضبط طابعة الإيصالات، لن يتم الطباعة.");
                    return;
                }

                if (!File.Exists(imagePath))
                {
                    Log("⚠️ ملف صورة الإيصال غير موجود: " + imagePath);
                    return;
                }

                Image img = Image.FromFile(imagePath);

                System.Drawing.Printing.PrintDocument doc = new System.Drawing.Printing.PrintDocument
                {
                    PrinterSettings = { PrinterName = printerName },
                    DocumentName = "WhatsAppAutoSender Receipt"
                };

                receiptPrintImage = img;
                receiptImagePathForPrint = imagePath;

                try
                {
                    // نطبّق مقاس الورق المختار من إعدادات الكوبرى (يضبط الـ PaperSize)
                    ApplyReceiptBridgePaperSize(doc);
                }
                catch { }

                // ===================================================================
                // التعديل الجديد: التحكم في نقطة بداية الطباعة (Origin) حسب النوع
                // ===================================================================
                string paperSizeSetting = Properties.Settings.Default.ReceiptBridge_PaperSize;

                if (paperSizeSetting == "A4")
                {
                    // في حالة A4 نحترم هوامش الطابعة (لأن الليزر والإنكجيت لا تطبع للحافة)
                    doc.OriginAtMargins = true;
                }
                else
                {
                    // في حالة 58mm أو 80mm (حراري) نلغي الهوامش تماماً
                    // ونبدأ الطباعة من الحافة الفعلية للورقة (0,0)
                    doc.OriginAtMargins = false;
                    doc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                }
                // ===================================================================

                doc.PrintPage += ReceiptPrintDocument_PrintPage;
                doc.EndPrint += (s, e) =>
                {
                    try
                    {
                        receiptPrintImage?.Dispose();
                    }
                    catch { }
                    finally
                    {
                        receiptPrintImage = null;
                        receiptImagePathForPrint = null;
                    }
                };

                Log("🖨️ إرسال إيصال للطابعة الحرارية: " + printerName);
                doc.Print();
            }
            catch (Exception ex)
            {
                Log("❌ خطأ أثناء الطباعة على الطابعة الحرارية: " + ex.Message);
            }
        }

        private void ApplyReceiptBridgePaperSize(System.Drawing.Printing.PrintDocument doc)
        {
            string selected = Properties.Settings.Default.ReceiptBridge_PaperSize;
            if (string.IsNullOrWhiteSpace(selected) || selected == "Auto")
                return;

            var pageSettings = doc.DefaultPageSettings;
            var sizes = doc.PrinterSettings.PaperSizes.Cast<PaperSize>();

            PaperSize chosen = null;

            if (selected == "A4")
            {
                // نحاول نجيب A4 من قائمة المقاسات
                chosen = sizes.FirstOrDefault(p => p.Kind == PaperKind.A4);
            }
            else if (selected == "58mm" || selected == "80mm")
            {
                int mm = selected == "58mm" ? 58 : 80;

                // عرض المقاس المطلوب بوحدة 1/100 بوصة
                int desiredWidth = (int)Math.Round(mm / 25.4f * 100f);

                // نختار أقرب مقاس موجود فى تعريف الطابعة
                chosen = sizes
                    .OrderBy(p => Math.Abs(p.Width - desiredWidth))
                    .FirstOrDefault();

                // لو الطابعة مش راجعة مقاسات مناسبة، نعمل مقاس Custom
                if (chosen == null)
                {
                    var current = pageSettings.PaperSize;
                    chosen = new PaperSize(selected, desiredWidth, current.Height);
                }
            }

            if (chosen != null)
            {
                pageSettings.Margins = new Margins(0, 0, 0, 0);
                pageSettings.PaperSize = chosen;
                doc.DefaultPageSettings = pageSettings;
            }
        }

        // دالة لتنظيف القديم وتشغيل الجديد
        private void SwitchToMethod3()
        {
            // 1. تغيير الإعداد وحفظه
            selectedSendMethod = 3;
            Properties.Settings.Default.SendMethod = 3;
            Properties.Settings.Default.Save();

            // 2. إغلاق متصفح الطريقة 2 (Selenium) لو كان شغال
            if (driver != null)
            {
                Log("🔄 جاري إغلاق متصفح الطريقة 2 للإنتقال إلى الطريقة 3...");
                try
                {
                    QuitDriver(); // بيقفل الكروم درايفر
                }
                catch (Exception ex)
                {
                    Log("⚠️ خطأ أثناء إغلاق متصفح 2: " + ex.Message);
                }
            }

            // 3. تشغيل وتجهيز متصفح الطريقة 3 (Pro)
            ManageWebViewState();

            Log("✅ تم التحويل إلى الطريقة 3 (WebView2 Pro).");
        }


        private void ReceiptPrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (receiptPrintImage == null)
            {
                e.HasMorePages = false;
                return;
            }

            Graphics g = e.Graphics;
            string paperSizeSetting = Properties.Settings.Default.ReceiptBridge_PaperSize;

            float targetWidth = 285f;
            float startX = 0;
            float startY = 0;

            if (paperSizeSetting == "A4")
            {
                float marginBuffer = 20f;
                targetWidth = e.MarginBounds.Width - (marginBuffer * 2);
                startX = e.MarginBounds.Left + marginBuffer;
                startY = e.MarginBounds.Top + marginBuffer;
                if (targetWidth <= 0) targetWidth = e.PageBounds.Width - 100;
            }
            else if (paperSizeSetting == "58mm")
            {
                targetWidth = 190f;
            }
            else if (paperSizeSetting == "80mm")
            {
                targetWidth = 285f;
            }
            else // Auto
            {
                targetWidth = e.PageBounds.Width > 0 ? e.PageBounds.Width : 285f;
            }

            float scaleFactor = targetWidth / receiptPrintImage.Width;
            float finalWidth = targetWidth;
            float finalHeight = receiptPrintImage.Height * scaleFactor;

            // ✅✅ التعديل الأهم للسرعة: إلغاء كل إعدادات الجودة العالية
            // الطابعة الحرارية لا تحتاج HighQualityBicubic وتسبب بطء شديد
            // نستخدم NearestNeighbor للحصول على طباعة فورية ونصوص حادة (Crisp)
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.CompositingQuality = CompositingQuality.HighSpeed;

            g.DrawImage(receiptPrintImage, startX, startY, finalWidth, finalHeight);

            e.HasMorePages = false;
        }

        // تدمج كل صفحات الإيصال فى صورة واحدة طويلة للطباعة على رول
        // ✅ دالة الدمج الجديدة: تقبل عرض محدد (forcedWidth) لتجهيز الصورة بمقاس الطابعة فوراً
        private string CombineReceiptPagesIntoOne(List<string> pageImages, int forcedWidth = 0)
        {
            try
            {
                if (pageImages == null || pageImages.Count == 0)
                    return null;

                var bitmaps = pageImages
                    .Where(File.Exists)
                    .Select(p => new Bitmap(p))
                    .ToList();

                if (bitmaps.Count == 0)
                    return null;

                // إذا تم تحديد عرض إجباري (للطابعة الحرارية) نستخدمه، وإلا نستخدم عرض أكبر صورة
                int targetWidth = (forcedWidth > 0) ? forcedWidth : bitmaps.Max(b => b.Width);

                var normalized = new List<Bitmap>();
                foreach (var bmp in bitmaps)
                {
                    if (bmp.Width != targetWidth)
                    {
                        // تغيير حجم الصورة لتناسب العرض المطلوب
                        int newHeight = (int)(bmp.Height * ((float)targetWidth / bmp.Width));
                        var resized = new Bitmap(targetWidth, newHeight);
                        using (var g = Graphics.FromImage(resized))
                        {
                            // ✅ NearestNeighbor هو الأسرع والأوضح للنصوص على الطابعات الحرارية
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.DrawImage(bmp, 0, 0, targetWidth, newHeight);
                        }
                        bmp.Dispose();
                        normalized.Add(resized);
                    }
                    else
                    {
                        normalized.Add(bmp);
                    }
                }

                int totalHeight = normalized.Sum(b => b.Height);

                var final = new Bitmap(targetWidth, totalHeight);
                using (var g = Graphics.FromImage(final))
                {
                    g.Clear(Color.White);

                    // ✅ إعدادات السرعة القصوى للرسم
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.None;
                    g.SmoothingMode = SmoothingMode.None;
                    g.CompositingQuality = CompositingQuality.HighSpeed;

                    int offsetY = 0;
                    foreach (var bmp in normalized)
                    {
                        g.DrawImage(bmp, 0, offsetY, bmp.Width, bmp.Height);
                        offsetY += bmp.Height;
                    }
                }

                // تنظيف الذاكرة
                foreach (var bmp in normalized)
                    bmp.Dispose();

                string outputDir = Path.Combine(Path.GetTempPath(), "ReceiptBridgeMerged");
                Directory.CreateDirectory(outputDir);

                string mergedPath = Path.Combine(
                    outputDir,
                    Path.GetFileNameWithoutExtension(pageImages[0]) + "_merged.jpg"
                );

                // حفظ الصورة النهائية
                final.Save(mergedPath, ImageFormat.Jpeg);
                final.Dispose();

                return mergedPath;
            }
            catch (Exception ex)
            {
                Log("⚠️ خطأ أثناء دمج صفحات الإيصال فى صورة واحدة: " + ex.Message);
                return null;
            }
        }
        private string ApplyHeaderFooter(string path)
        {
            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif")
                return ApplyLetterheadToImage(path);
            else if (ext == ".pdf")
                return ApplyLetterheadToPdf(path);

            return path;
        }

        // دالة لإزالة الشريط الأحمر أو الكتابة من أسفل الصورة
        // دالة لإزالة الشريط الأحمر (Trial Mode) أو السطر الأخير من أسفل الصورة (لو موجود فقط)
        private string RemoveTrialWatermark(string imagePath)
        {
            try
            {
                // أقل ارتفاع ممكن نغطيه (لو الشريط صغير جدًا)
                const int minCleanHeight = 15;

                // أقصى ارتفاع نسمح بيه عشان ما نمسحش بيانات بالخطأ
                const int maxCleanHeight = 140;

                // هنفحص آخر جزء من الصورة فقط
                const int scanHeight = 220;

                // ✅ ده المهم: نطلع فوق بداية الشريط كام بيكسل عشان نمسح الـ anti-aliasing
                const int extraTopPadding = 6;   // جرّب 6..10 حسب حالتك

                string tempDir = Path.Combine(Path.GetTempPath(), "CleanedImages");
                Directory.CreateDirectory(tempDir);

                string newPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(imagePath) + "_clean.jpg");

                using (Image image = Image.FromFile(imagePath))
                using (Bitmap bitmap = new Bitmap(image))
                {
                    int topY = FindTrialWatermarkTopY(bitmap, scanHeight);

                    // ✅ مفيش Watermark/Trial band واضح -> رجّع نفس الصورة (ما نغطيش بيانات)
                    if (topY < 0)
                        return imagePath;

                    // ✅ زوّد تغطية فوق بداية الشريط عشان ما يفضلش أثر أحمر خفيف
                    topY = Math.Max(0, topY - extraTopPadding);

                    int cleanHeight = bitmap.Height - topY;

                    // ✅ حماية من المبالغة
                    cleanHeight = Math.Max(minCleanHeight, Math.Min(maxCleanHeight, cleanHeight));

                    int y = Math.Max(0, bitmap.Height - cleanHeight);

                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.FillRectangle(Brushes.White, new Rectangle(0, y, bitmap.Width, cleanHeight));
                    }

                    bitmap.Save(newPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                return newPath;
            }
            catch (Exception ex)
            {
                Log("⚠️ فشل إزالة شريط التجربة: " + ex.Message);
                return imagePath;
            }
        }

        private int FindTrialWatermarkTopY(Bitmap bmp, int scanHeight)
        {
            int h = bmp.Height;
            int y0 = Math.Max(0, h - scanHeight);

            int bottom = -1;
            int top = -1;

            int gap = 0;
            const int allowedGapRows = 4;

            // 1) نحدد الباند الأحمر العريض من تحت لفوق
            for (int y = h - 1; y >= y0; y--)
            {
                if (RowHasTrialRedBand(bmp, y))
                {
                    if (bottom == -1) bottom = y;
                    top = y;
                    gap = 0;
                }
                else
                {
                    if (bottom != -1)
                    {
                        gap++;
                        if (gap >= allowedGapRows)
                            break;
                    }
                }
            }

            if (bottom == -1 || top == -1)
                return -1;

            int bandHeight = bottom - top + 1;
            if (bandHeight < 6)
                return -1;

            // 2) ✅ Refinement: طلع فوق بداية الشريط ولقّط أي أحمر خفيف (anti-alias)
            top = RefineTrialTopUpwards(bmp, top, y0);

            return top;
        }

        private int RefineTrialTopUpwards(Bitmap bmp, int detectedTopY, int minY)
        {
            // هنطلع فوق الـ top شوية صفوف، لو لقينا أي “أحمر خفيف” نطلع له كمان
            const int maxLookUp = 14;          // قد إيه نطلع لفوق
            const double anyRedRowRatio = 0.004; // حساسية أخف من الباند العريض

            int top = detectedTopY;

            for (int i = 1; i <= maxLookUp; i++)
            {
                int y = detectedTopY - i;
                if (y < minY) break;

                if (RowHasAnyTrialRed(bmp, y, anyRedRowRatio))
                    top = y;
                else
                    break; // أول صف مفيهوش الأحمر الخفيف.. وقف
            }

            return top;
        }

        private bool RowHasTrialRedBand(Bitmap bmp, int y)
        {
            int w = bmp.Width;

            const int step = 3;

            int redCount = 0;
            int total = 0;

            for (int x = 0; x < w; x += step)
            {
                Color c = bmp.GetPixel(x, y);
                total++;

                if (IsTrialRedPixel(c))
                    redCount++;
            }

            if (total == 0) return false;

            double ratio = redCount / (double)total;

            // ✅ “باند عريض” = نسبة محترمة من عرض الصفحة أحمر
            return ratio >= 0.010; // 1.0% (تقدر تخليها 0.008 لو الشريط أضعف)
        }

        private bool RowHasAnyTrialRed(Bitmap bmp, int y, double ratioThreshold)
        {
            int w = bmp.Width;

            const int step = 4;

            int redCount = 0;
            int total = 0;

            for (int x = 0; x < w; x += step)
            {
                Color c = bmp.GetPixel(x, y);
                total++;

                if (IsTrialRedPixel(c))
                    redCount++;
            }

            if (total == 0) return false;

            double ratio = redCount / (double)total;
            return ratio >= ratioThreshold;
        }

        private bool IsTrialRedPixel(Color c)
        {
            if (c.A < 20) return false;

            int r = c.R, g = c.G, b = c.B;

            // الأحمر لازم يكون المسيطر
            if (r <= g || r <= b) return false;

            int maxGB = Math.Max(g, b);
            int diff = r - maxGB;

            // يسمح بالأحمر الغامق لكن لازم فرق واضح
            if (r < 55) return false;
            if (diff < 20) return false;

            // يمنع البرتقالي/الأصفر (لو G قريب جدًا من R)
            if (g > (int)(r * 0.90)) return false;

            float hue = c.GetHue();           // 0..360
            float sat = c.GetSaturation();    // 0..1
            float bri = c.GetBrightness();    // 0..1

            bool hueOk = (hue <= 24f || hue >= 336f);
            bool satOk = sat >= 0.20f;        // أوسع شوية للأحمر الفاتح/المتدرّج
            bool briOk = bri >= 0.08f;        // يسمح بغمقان شديد

            return hueOk && satOk && briOk;
        }

        private void CoverTrialLineInPdf(iTextSharp.text.pdf.PdfContentByte content, float pageWidth)
        {
            // ✅ ارتفاع الشريط بالأرقام "Points" (مش Pixels)
            // 22pt تقريبًا تساوي 18~25px حسب التحويل/العرض، وتغطي الـ anti-aliasing كمان
            const float stripHeightPt = 22f;

            content.SaveState();
            content.SetColorFill(iTextSharp.text.BaseColor.WHITE);

            // من أسفل الصفحة (y=0) بعرض الصفحة بالكامل
            content.Rectangle(0, 0, pageWidth, stripHeightPt);
            content.Fill();

            content.RestoreState();
        }

    }


    internal class AppMenuColorTable : ProfessionalColorTable
    {
        private readonly bool _dark;
        private readonly Color _panelBg;
        private readonly Color _cardBg;
        private readonly Color _textFg;

        public AppMenuColorTable(bool dark, Color panelBg, Color cardBg, Color textFg)
        {
            _dark = dark;
            _panelBg = panelBg;
            _cardBg = cardBg;
            _textFg = textFg;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => _dark ? _panelBg : Color.White;
        public override Color MenuStripGradientEnd => _dark ? _panelBg : Color.White;

        public override Color ToolStripDropDownBackground => _dark ? _cardBg : Color.White;

        public override Color ImageMarginGradientBegin => _dark ? _cardBg : Color.White;
        public override Color ImageMarginGradientMiddle => _dark ? _cardBg : Color.White;
        public override Color ImageMarginGradientEnd => _dark ? _cardBg : Color.White;

        public override Color MenuItemSelected => _dark ? Color.FromArgb(60, 60, 66) : Color.FromArgb(230, 235, 245);
        public override Color MenuItemBorder => _dark ? Color.FromArgb(90, 90, 96) : Color.FromArgb(200, 205, 215);

        public override Color MenuItemPressedGradientBegin => _dark ? Color.FromArgb(55, 55, 60) : Color.FromArgb(220, 225, 235);
        public override Color MenuItemPressedGradientEnd => _dark ? Color.FromArgb(55, 55, 60) : Color.FromArgb(220, 225, 235);

    }
}
