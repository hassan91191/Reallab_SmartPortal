using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

// ✅ التعريف ده لازم يكون في الأول خالص
using IOFile = System.IO.File;

namespace WhatsAppAutoSender
{
    public class JsNotificationData
    {
        public string title { get; set; }
        public string body { get; set; }
        public string internalId { get; set; }
        public string iconData { get; set; }
    }

    public partial class WppBrowserForm : Form
    {
        // ==========================================
        // 1. المتغيرات (Fields)
        // ==========================================
        private WebView2 webView;
        private readonly string userDataFolder = @"C:\WhatsAppWebView2Session";
        private bool _allowRealClose = false;
        private readonly bool _enableNotifications;
        private static string _wppJsCache = null;
        private static readonly SemaphoreSlim _wppInjectLock = new SemaphoreSlim(1, 1);
        private readonly Action<string> _log;
        private CustomToastForm _currentToast = null;
        public bool IsReady { get; private set; } = false;

        // ✅ المتغير ده لازم يكون هنا (جوه الكلاس)
        private FormWindowState _targetState = FormWindowState.Normal;

        // ثوابت لأوامر الويندوز
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        // ==========================================
        // 2. Constructor & Initialization
        // ==========================================
        public WppBrowserForm(bool enableNotifications, Action<string> log = null)
        {
            InitializeComponent();
            _enableNotifications = enableNotifications;
            _log = log;

            this.Text = "WhatsApp";
            this.Size = new Size(1000, 700);

            // تعيين الأيقونة (لأنظمة ويندوز 10/11)
            this.Icon = Properties.Resources.browsers_icon;

            _log?.Invoke("🧩 بدء تشغيل WppBrowserForm...");

            InitializeWebView();
        }

        // إجبار الأيقونة لويندوز 7
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ForceTaskbarIcon();
        }

        private void ForceTaskbarIcon()
        {
            try
            {
                var icon = Properties.Resources.browsers_icon;
                if (icon != null)
                {
                    SendMessage(this.Handle, WM_SETICON, (IntPtr)ICON_SMALL, icon.Handle);
                    SendMessage(this.Handle, WM_SETICON, (IntPtr)ICON_BIG, icon.Handle);
                }
            }
            catch { }
        }

        private async void InitializeWebView()
        {
            try
            {
                // مسار البروفايل الآمن
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string safeUserDataFolder = Path.Combine(appDataPath, "WhatsAppAutoSender_Profile");
                Directory.CreateDirectory(safeUserDataFolder);
                _log?.Invoke($"📁 Safe Profile Path: {safeUserDataFolder}");

                // تحديد نوع الويندوز والمسار المناسب
                string browserExecutableFolder = null;

                if (IsWindows7OrLower())
                {
                    _log?.Invoke("🖥️ Windows 7 detected: Using local Fixed Runtime.");
                    string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CoreFiles7W");
                    string tempPath = Path.GetTempPath();
                    string safeRuntimePath = Path.Combine(tempPath, "WppCore");

                    if (!Directory.Exists(sourcePath))
                        throw new Exception($"مجلد الملفات الأصلية غير موجود:\n{sourcePath}");

                    if (!Directory.Exists(safeRuntimePath) || !IOFile.Exists(Path.Combine(safeRuntimePath, "msedgewebview2.exe")))
                    {
                        _log?.Invoke("📦 Copying WebView2 files...");
                        CopyDirectory(sourcePath, safeRuntimePath);
                    }

                    safeRuntimePath = safeRuntimePath.TrimEnd('\\');
                    browserExecutableFolder = safeRuntimePath;
                    Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", safeRuntimePath);
                }
                else
                {
                    _log?.Invoke("🖥️ Windows 10/11 detected: Using System Runtime.");
                    Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", null);
                }

                var options = new CoreWebView2EnvironmentOptions();

                webView = new Microsoft.Web.WebView2.WinForms.WebView2();
                webView.Dock = DockStyle.Fill;
                this.Controls.Add(webView);

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder, safeUserDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                webView.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
                webView.NavigationCompleted += WebView_NavigationCompleted;

                webView.Source = new Uri("https://web.whatsapp.com");
                IsReady = true;
                _log?.Invoke("✅ WebView2 Started Successfully.");
            }
            catch (Exception ex)
            {
                _log?.Invoke("❌ Fatal Error: " + ex.ToString());
                MessageBox.Show($"خطأ في تهيئة المتصفح:\n{ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // 3. إدارة النافذة (OnLoad / OnVisibleChanged)
        // ==========================================

        // ✅ الحل الجذري لويندوز 7 باستخدام Timer
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                var settings = Properties.Settings.Default;

                // 1. تثبيت الوضع Normal أولاً
                this.WindowState = FormWindowState.Normal;
                this.StartPosition = FormStartPosition.Manual;

                // 2. استرجاع المكان والحجم
                if (settings.BrowserSize.Width > 100 && settings.BrowserSize.Height > 100)
                {
                    this.Location = settings.BrowserLocation;
                    this.Size = settings.BrowserSize;

                    if (!IsOnScreen(this.Location, this.Size))
                        this.StartPosition = FormStartPosition.CenterScreen;
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                }

                // 3. تخزين الحالة المطلوبة
                _targetState = settings.BrowserWindowState;

                // 4. لو المطلوب Maximize، نستخدم التايمر لتأخير التنفيذ
                if (_targetState == FormWindowState.Maximized)
                {
                    System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                    t.Interval = 300; // 300ms كافية
                    t.Tick += (s, args) =>
                    {
                        t.Stop();
                        t.Dispose();
                        this.WindowState = FormWindowState.Maximized;
                        this.Activate();
                    };
                    t.Start();
                }
            }
            catch
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }

        // ✅ الحفاظ على الـ Maximize عند الإظهار والإخفاء
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (this.Visible && _targetState == FormWindowState.Maximized)
            {
                if (this.WindowState != FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                // حفظ الحالة الحالية
                if (this.WindowState == FormWindowState.Minimized)
                    Properties.Settings.Default.BrowserWindowState = _targetState;
                else
                    Properties.Settings.Default.BrowserWindowState = this.WindowState;

                if (this.WindowState == FormWindowState.Normal)
                {
                    Properties.Settings.Default.BrowserSize = this.Size;
                    Properties.Settings.Default.BrowserLocation = this.Location;
                }
                else
                {
                    Properties.Settings.Default.BrowserSize = this.RestoreBounds.Size;
                    Properties.Settings.Default.BrowserLocation = this.RestoreBounds.Location;
                }
                Properties.Settings.Default.Save();
            }
            catch { }

            // منع الإغلاق الكامل إلا عند الخروج النهائي
            if (e.CloseReason == CloseReason.UserClosing && !_allowRealClose)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public async Task ShutdownPersistSessionAsync()
        {
            try
            {
                _allowRealClose = true;
                if (webView?.CoreWebView2 != null)
                {
                    try { await webView.CoreWebView2.TrySuspendAsync(); } catch { }
                }
                try { this.Close(); } catch { }
                try { webView?.Dispose(); } catch { }
                webView = null;
            }
            catch { }
        }

        // ==========================================
        // 4. دوال مساعدة (WPP / Scripts)
        // ==========================================

        private bool IsOnScreen(Point location, Size size)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(new Rectangle(location, size)))
                    return true;
            }
            return false;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                IOFile.Copy(file, destFile, true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private static bool IsWindows7OrLower()
        {
            Version v = Environment.OSVersion.Version;
            return (v.Major < 6) || (v.Major == 6 && v.Minor <= 1);
        }

        public async Task<string> ExecuteScriptAsync(string jsCode)
        {
            if (webView != null && webView.CoreWebView2 != null)
                return await webView.ExecuteScriptAsync(jsCode);
            return "null";
        }

        // ==========================================
        // 5. حقن الإسكريبتات (Injection)
        // ==========================================

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            await InjectWppConnect();
            await InjectSignature();
            await InjectNotificationInterceptor();
        }

        private async Task InjectWppConnect()
        {
            if (webView == null || webView.CoreWebView2 == null) return;

            string already = await ExecuteScriptAsync("(()=>{ try { return (!!window.WPP).toString(); } catch(e){ return 'false'; } })();");
            if (already.Trim().Trim('"').ToLower() == "true") return;

            await _wppInjectLock.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(_wppJsCache))
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var client = new HttpClient())
                    {
                        _wppJsCache = await client.GetStringAsync("https://cdn.jsdelivr.net/npm/@wppconnect/wa-js@latest/dist/wppconnect-wa.js");
                    }
                }
                await webView.ExecuteScriptAsync(_wppJsCache);
            }
            finally { _wppInjectLock.Release(); }
        }

        private async Task InjectSignature()
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
                label.style.pointerEvents = 'none';
                document.body.appendChild(label);
            })();";
            await webView.ExecuteScriptAsync(script);
        }

        private async Task InjectNotificationInterceptor()
        {
            string script = @"
                if (!window.NotificationInterceptorInstalled) {
                    // المخزن اللي هنحفظ فيه أوامر فتح الشات
                    window.NotificationCallbacks = {};
                    
                    window.OriginalNotification = window.Notification;
                    
                    window.Notification = function(title, options) {
                        const uniqueId = 'notif_' + Date.now() + '_' + Math.floor(Math.random() * 1000);
                        
                        // 1. إرسال البيانات للـ C# (زي ما هي)
                        const fetchIcon = async () => {
                            let iconBase64 = null;
                            if (options && options.icon) {
                                try {
                                    const response = await fetch(options.icon);
                                    const blob = await response.blob();
                                    iconBase64 = await new Promise((resolve) => {
                                        const reader = new FileReader();
                                        reader.onloadend = () => resolve(reader.result);
                                        reader.readAsDataURL(blob);
                                    });
                                } catch (e) { }
                            }
                            return iconBase64;
                        };

                        (async () => {
                            const imgData = await fetchIcon();
                            var payload = { 
                                title: title, 
                                body: (options && options.body) ? options.body : '',
                                internalId: uniqueId,
                                iconData: imgData 
                            };
                            window.chrome.webview.postMessage(JSON.stringify(payload));
                        })();

                        // 2. إنشاء الإشعار الوهمي الذكي
                        const fakeNotification = { 
                            close: function() {},
                            
                            // خدعة 1: لو واتساب استخدم addEventListener
                            addEventListener: function(type, listener) {
                                if (type === 'click') {
                                    window.NotificationCallbacks[uniqueId] = listener;
                                }
                            }
                        };

                        // خدعة 2: لو واتساب استخدم .onclick المباشرة
                        Object.defineProperty(fakeNotification, 'onclick', {
                            set: function(handler) { 
                                window.NotificationCallbacks[uniqueId] = handler; 
                            },
                            get: function() { 
                                return window.NotificationCallbacks[uniqueId]; 
                            }
                        });
                        
                        return fakeNotification;
                    };

                    window.Notification.permission = 'granted';
                    window.Notification.requestPermission = async function() { return 'granted'; };
                    window.NotificationInterceptorInstalled = true;
                }
            ";
            await webView.ExecuteScriptAsync(script);
        }

        // ==========================================
        // 6. استقبال الإشعارات
        // ==========================================

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var data = JsonConvert.DeserializeObject<JsNotificationData>(json);

                if (data != null && _enableNotifications)
                {
                    this.Invoke(new Action(() =>
                    {
                        if (_currentToast != null && !_currentToast.IsDisposed)
                            _currentToast.CloseImmediate();

                        string key = data.internalId;
                        Image userPic = null;

                        if (!string.IsNullOrEmpty(data.iconData))
                        {
                            try
                            {
                                string cleanBase64 = data.iconData.Substring(data.iconData.IndexOf(",") + 1);
                                byte[] bytes = Convert.FromBase64String(cleanBase64);
                                using (var ms = new MemoryStream(bytes)) userPic = Image.FromStream(ms);
                            }
                            catch { }
                        }

                        _currentToast = new CustomToastForm(data.title, data.body, userPic, (val) =>
                        {
                            // 1. استعادة النافذة
                            this.Show();
                            if (this.WindowState == FormWindowState.Minimized)
                                this.WindowState = (_targetState == FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal;

                            this.BringToFront();
                            this.Activate();

                            // 2. المحاكاة
                            TriggerNativeClick(key);
                        });

                        _currentToast.Show();
                    }));
                }
            }
            catch { }
        }

        private async void TriggerNativeClick(string internalId)
        {
            if (string.IsNullOrWhiteSpace(internalId)) return;

            // كود الجافا سكريبت: ركز على النافذة ونفذ الدالة المحفوظة
            string js = $@"
            (async () => {{
                // 1. التركيز
                window.focus();
                
                // 2. محاولة تنفيذ الدالة المحفوظة (سواء كانت من addEventListener أو onclick)
                const callback = window.NotificationCallbacks['{internalId}'];
                
                if (typeof callback === 'function') {{
                    // بنمرر event وهمي عشان لو واتساب محتاجه
                    callback({{ target: {{}} }}); 
                    
                    // تنظيف الذاكرة
                    delete window.NotificationCallbacks['{internalId}'];
                }}
            }})();";

            await webView.ExecuteScriptAsync(js);
        }

        private void CoreWebView2_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
            {
                e.State = CoreWebView2PermissionState.Allow;
                e.Handled = true;
            }
        }

        // ==========================================
        // 7. دوال الإرسال (Public API)
        // ==========================================

        public async Task<bool> EnsureWppReadyAsync(int timeoutMs = 15000)
        {
            await InjectWppConnect();
            int waited = 0;
            while (waited < timeoutMs)
            {
                string res = await ExecuteScriptAsync("(()=>{ try { return (!!window.WPP && !!WPP.chat).toString(); } catch(e){ return 'false'; } })();");
                if (res.Trim().Trim('"').ToLower() == "true") return true;
                await Task.Delay(250);
                waited += 250;
            }
            return false;
        }

        public async Task<string> SendTextWppAsync(string phoneOrJid, string text)
        {
            if (string.IsNullOrWhiteSpace(phoneOrJid) || string.IsNullOrWhiteSpace(text)) return "INVALID_ARGS";

            string jid = phoneOrJid.EndsWith("@c.us") ? phoneOrJid : (phoneOrJid + "@c.us");

            // تنظيف النص
            string safe = JsEscape(text);

            if (!await EnsureWppReadyAsync()) return "WPP_NOT_READY";

            string js = $@"
            (async () => {{
                try {{
                    if (!window.WPP || !window.WPP.chat || !window.WPP.contact) return 'WPP_NOT_READY';

                    // 1. فحص الرقم
                    const exists = await WPP.contact.queryExists('{jid}');
                    if (!exists) return 'INVALID_NUMBER';

                    // 2. تحضير الشات
                    try {{ await WPP.chat.find('{jid}'); }} catch (e) {{ }}

                    // 3. انتظار التزامن (يمنع الشبح)
                    await new Promise(r => setTimeout(r, 500));

                    // 4. الإرسال
                    await WPP.chat.sendTextMessage('{jid}', '{safe}', {{ createChat: true }});
                    return 'SUCCESS';
                }} catch (e) {{ return 'ERROR: ' + e; }}
            }})();";

            return await ExecuteScriptAsync(js);
        }

        public async void SendFile(string phone, string filePath)
        {
            await SendFileWppAsync(phone, filePath);
        }

        public async Task<string> SendFileWppAsync(string phone, string filePath)
        {
            try
            {
                if (!IOFile.Exists(filePath)) return "FILE_NOT_FOUND";
                if (!await EnsureWppReadyAsync()) return "WPP_NOT_READY";

                string fileName = Path.GetFileName(filePath);
                string extension = Path.GetExtension(filePath).ToLower();

                string mimeType = "application/octet-stream";
                if (extension == ".jpg" || extension == ".jpeg") mimeType = "image/jpeg";
                else if (extension == ".png") mimeType = "image/png";
                else if (extension == ".pdf") mimeType = "application/pdf";

                // تحديد نوع الملف زي الطريقة 2
                string wppType = (mimeType.StartsWith("image")) ? "image" : "document";

                byte[] bytes = IOFile.ReadAllBytes(filePath);
                string base64Url = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";

                string jid = phone.EndsWith("@c.us") ? phone : phone + "@c.us";

                // استخدام JsEscape لضمان سلامة النصوص
                string safeName = JsEscape(fileName);
                string safeMime = JsEscape(mimeType);
                string safeType = JsEscape(wppType);

                string js = $@"
                (async () => {{
                    try {{
                        if (!window.WPP || !window.WPP.chat || !window.WPP.contact) return 'WPP_NOT_READY';

                        // 1. فحص وجود الرقم
                        const exists = await WPP.contact.queryExists('{jid}');
                        if (!exists) return 'INVALID_NUMBER';

                        // 2. تحضير الشات (Find)
                        try {{ await WPP.chat.find('{jid}'); }} catch (e) {{ }}

                        // 3. (السر هنا) انتظار قصير عشان واتساب يلحق يربط الشات بالرقم (يمنع الشات الشبح)
                        await new Promise(r => setTimeout(r, 500));

                        // 4. الإرسال
                        await WPP.chat.sendFileMessage('{jid}', '{base64Url}', {{
                            type: '{safeType}',
                            filename: '{safeName}',
                            mimetype: '{safeMime}',
                            createChat: true
                        }});
                        return 'SUCCESS';
                    }} catch (err) {{ return 'ERROR: ' + err; }}
                }})();";

                return await ExecuteScriptAsync(js);
            }
            catch (Exception ex) { return "ERROR_CSHARP: " + ex.Message; }
        }

        public async Task<bool> CheckNumberExistsAsync(string phone)
        {
            try
            {
                if (!IsReady) return false;

                string jid = phone.EndsWith("@c.us") ? phone : phone + "@c.us";

                // كود جافا سكريبت للتحقق من الرقم عبر WPPConnect
                string js = $@"
        (async () => {{
            try {{
                const result = await WPP.contact.queryExists('{jid}');
                return result ? 'true' : 'false';
            }} catch (err) {{ return 'false'; }}
        }})();";

                string res = await ExecuteScriptAsync(js);
                return res != null && res.Trim().Trim('"') == "true";
            }
            catch
            {
                return false;
            }
        }

        private string JsEscape(string s)
        {
            if (s == null) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "")
                .Replace("\n", "\\n"); // الحفاظ على السطور الجديدة
        }
    }
}