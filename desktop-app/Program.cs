using System;
using System.Threading;
using System.Windows.Forms;

namespace WhatsAppAutoSender
{
    static class Program
    {
        public static MainForm mainFormInstance;

        [STAThread]
        static void Main()
        {
            // 1. إعداد صياد الأخطاء ليمسك أي خطأ غير متوقع
            Application.ThreadException += new ThreadExceptionEventHandler(GlobalThreadExceptionHandler);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalDomainExceptionHandler);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "WhatsAppAutoSenderInstance", out createdNew))
            {
                if (createdNew)
                {
                    try
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);

                        mainFormInstance = new MainForm();

                        Application.ApplicationExit += (s, e) =>
                        {
                            try { mainFormInstance?.QuitDriver(); } catch { }
                        };

                        Application.Run(mainFormInstance);
                    }
                    catch (Exception ex)
                    {
                        // لو الخطأ حصل أثناء تشغيل الفورم
                        MessageBox.Show("حدث خطأ قاتل تسبب في إغلاق البرنامج:\n" + ex.Message + "\n" + ex.StackTrace,
                            "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        try { mainFormInstance?.QuitDriver(); } catch { }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "⚠️ برنامج WhatsApp Auto Sender يعمل بالفعل.\n" +
                        "لا يمكنك فتح أكثر من نسخة في نفس الوقت.",
                        "تنبيه",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
        }

        // دالة لعرض الأخطاء التي تحدث في الـ UI Threads
        private static void GlobalThreadExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("حدث خطأ غير متوقع (Thread):\n" + e.Exception.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // دالة لعرض الأخطاء التي تحدث في الخلفية (Background Threads)
        private static void GlobalDomainExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show("حدث خطأ قاتل في النظام (Domain):\n" + (ex != null ? ex.Message : "Unknown Error"),
                            "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }
    }
}