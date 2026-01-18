namespace WhatsApp_Auto_Sender
{
    partial class ResultsLinkSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.grpDb = new System.Windows.Forms.GroupBox();
            this.btnInitDb = new System.Windows.Forms.Button();
            this.btnTestDb = new System.Windows.Forms.Button();
            this.chkUseManualSql = new System.Windows.Forms.CheckBox();
            this.labelSqlServer = new System.Windows.Forms.Label();
            this.txtSqlServer = new System.Windows.Forms.TextBox();
            this.labelSqlDb = new System.Windows.Forms.Label();
            this.txtSqlDb = new System.Windows.Forms.TextBox();
            this.labelSqlUser = new System.Windows.Forms.Label();
            this.txtSqlUser = new System.Windows.Forms.TextBox();
            this.labelSqlPass = new System.Windows.Forms.Label();
            this.txtSqlPass = new System.Windows.Forms.TextBox();
            this.grpGoogle = new System.Windows.Forms.GroupBox();
            this.lblRootFolderStatus = new System.Windows.Forms.Label();
            this.btnGoogleLogin = new System.Windows.Forms.Button();
            this.grpUpload = new System.Windows.Forms.GroupBox();
            this.numPoll = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.txtExportFolder = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.grpWhatsApp = new System.Windows.Forms.GroupBox();
            this.chkAddQrToReceipt = new System.Windows.Forms.CheckBox();
            this.txtReceiptCaption = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtMsgPrefix = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.chkSendLinkWhatsApp = new System.Windows.Forms.CheckBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnDefaults = new System.Windows.Forms.Button();
            this.btnUndoSql = new System.Windows.Forms.Button();
            this.grpDb.SuspendLayout();
            this.grpGoogle.SuspendLayout();
            this.grpUpload.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPoll)).BeginInit();
            this.grpWhatsApp.SuspendLayout();
            this.SuspendLayout();
            // 
            // chkEnabled
            // 
            this.chkEnabled.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkEnabled.AutoSize = true;
            this.chkEnabled.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.chkEnabled.ForeColor = System.Drawing.Color.DarkBlue;
            this.chkEnabled.Location = new System.Drawing.Point(390, 10);
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.Size = new System.Drawing.Size(176, 24);
            this.chkEnabled.TabIndex = 0;
            this.chkEnabled.Text = "تفعيل خدمة لينك النتائج";
            this.chkEnabled.UseVisualStyleBackColor = true;
            // 
            // grpDb
            // 
            this.grpDb.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpDb.Controls.Add(this.btnUndoSql);
            this.grpDb.Controls.Add(this.btnInitDb);
            this.grpDb.Controls.Add(this.btnTestDb);
            this.grpDb.Controls.Add(this.chkUseManualSql);
            this.grpDb.Controls.Add(this.labelSqlServer);
            this.grpDb.Controls.Add(this.txtSqlServer);
            this.grpDb.Controls.Add(this.labelSqlDb);
            this.grpDb.Controls.Add(this.txtSqlDb);
            this.grpDb.Controls.Add(this.labelSqlUser);
            this.grpDb.Controls.Add(this.txtSqlUser);
            this.grpDb.Controls.Add(this.labelSqlPass);
            this.grpDb.Controls.Add(this.txtSqlPass);
            this.grpDb.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpDb.Location = new System.Drawing.Point(12, 40);
            this.grpDb.Name = "grpDb";
            this.grpDb.Size = new System.Drawing.Size(576, 180);
            this.grpDb.TabIndex = 1;
            this.grpDb.TabStop = false;
            this.grpDb.Text = "1. إعدادات قاعدة البيانات (SQL)";
            // 
            // btnInitDb
            // 
            this.btnInitDb.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnInitDb.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnInitDb.Location = new System.Drawing.Point(130, 135);
            this.btnInitDb.Name = "btnInitDb";
            this.btnInitDb.Size = new System.Drawing.Size(150, 30);
            this.btnInitDb.TabIndex = 13;
            this.btnInitDb.Text = "تهيئة الداتابيز (Initialize)";
            this.btnInitDb.UseVisualStyleBackColor = true;
            this.btnInitDb.Click += new System.EventHandler(this.btnInitDb_Click);
            // 
            // btnTestDb
            // 
            this.btnTestDb.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnTestDb.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnTestDb.Location = new System.Drawing.Point(290, 135);
            this.btnTestDb.Name = "btnTestDb";
            this.btnTestDb.Size = new System.Drawing.Size(140, 30);
            this.btnTestDb.TabIndex = 12;
            this.btnTestDb.Text = "اختبار الاتصال (Test)";
            this.btnTestDb.UseVisualStyleBackColor = true;
            this.btnTestDb.Click += new System.EventHandler(this.btnTestDb_Click);
            // 
            // chkUseManualSql
            // 
            this.chkUseManualSql.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkUseManualSql.AutoSize = true;
            this.chkUseManualSql.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.chkUseManualSql.ForeColor = System.Drawing.Color.DarkRed;
            this.chkUseManualSql.Location = new System.Drawing.Point(295, 25);
            this.chkUseManualSql.Name = "chkUseManualSql";
            this.chkUseManualSql.Size = new System.Drawing.Size(251, 19);
            this.chkUseManualSql.TabIndex = 3;
            this.chkUseManualSql.Text = "إدخال بيانات السيرفر يدوياً (Manual Settings)";
            this.chkUseManualSql.UseVisualStyleBackColor = true;
            // 
            // labelSqlServer
            // 
            this.labelSqlServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSqlServer.AutoSize = true;
            this.labelSqlServer.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.labelSqlServer.Location = new System.Drawing.Point(465, 58);
            this.labelSqlServer.Name = "labelSqlServer";
            this.labelSqlServer.Size = new System.Drawing.Size(90, 15);
            this.labelSqlServer.TabIndex = 4;
            this.labelSqlServer.Text = "اسم السيرفر (IP):";
            // 
            // txtSqlServer
            // 
            this.txtSqlServer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSqlServer.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSqlServer.Location = new System.Drawing.Point(10, 55);
            this.txtSqlServer.Name = "txtSqlServer";
            this.txtSqlServer.Size = new System.Drawing.Size(449, 23);
            this.txtSqlServer.TabIndex = 5;
            // 
            // labelSqlDb
            // 
            this.labelSqlDb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSqlDb.AutoSize = true;
            this.labelSqlDb.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.labelSqlDb.Location = new System.Drawing.Point(465, 93);
            this.labelSqlDb.Name = "labelSqlDb";
            this.labelSqlDb.Size = new System.Drawing.Size(77, 15);
            this.labelSqlDb.TabIndex = 6;
            this.labelSqlDb.Text = "قاعدة البيانات:";
            // 
            // txtSqlDb
            // 
            this.txtSqlDb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSqlDb.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSqlDb.Location = new System.Drawing.Point(340, 90);
            this.txtSqlDb.Name = "txtSqlDb";
            this.txtSqlDb.Size = new System.Drawing.Size(119, 23);
            this.txtSqlDb.TabIndex = 7;
            this.txtSqlDb.Text = "Patients";
            // 
            // labelSqlUser
            // 
            this.labelSqlUser.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSqlUser.AutoSize = true;
            this.labelSqlUser.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.labelSqlUser.Location = new System.Drawing.Point(280, 93);
            this.labelSqlUser.Name = "labelSqlUser";
            this.labelSqlUser.Size = new System.Drawing.Size(58, 15);
            this.labelSqlUser.TabIndex = 8;
            this.labelSqlUser.Text = "المستخدم:";
            // 
            // txtSqlUser
            // 
            this.txtSqlUser.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSqlUser.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSqlUser.Location = new System.Drawing.Point(195, 90);
            this.txtSqlUser.Name = "txtSqlUser";
            this.txtSqlUser.Size = new System.Drawing.Size(80, 23);
            this.txtSqlUser.TabIndex = 9;
            this.txtSqlUser.Text = "sa";
            // 
            // labelSqlPass
            // 
            this.labelSqlPass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSqlPass.AutoSize = true;
            this.labelSqlPass.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.labelSqlPass.Location = new System.Drawing.Point(135, 93);
            this.labelSqlPass.Name = "labelSqlPass";
            this.labelSqlPass.Size = new System.Drawing.Size(57, 15);
            this.labelSqlPass.TabIndex = 10;
            this.labelSqlPass.Text = "كلمة السر:";
            // 
            // txtSqlPass
            // 
            this.txtSqlPass.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSqlPass.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSqlPass.Location = new System.Drawing.Point(10, 90);
            this.txtSqlPass.Name = "txtSqlPass";
            this.txtSqlPass.Size = new System.Drawing.Size(119, 23);
            this.txtSqlPass.TabIndex = 11;
            this.txtSqlPass.UseSystemPasswordChar = true;
            // 
            // grpGoogle
            // 
            this.grpGoogle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpGoogle.Controls.Add(this.lblRootFolderStatus);
            this.grpGoogle.Controls.Add(this.btnGoogleLogin);
            this.grpGoogle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpGoogle.Location = new System.Drawing.Point(12, 230);
            this.grpGoogle.Name = "grpGoogle";
            this.grpGoogle.Size = new System.Drawing.Size(576, 70);
            this.grpGoogle.TabIndex = 2;
            this.grpGoogle.TabStop = false;
            this.grpGoogle.Text = "2. الربط مع جوجل درايف";
            // 
            // lblRootFolderStatus
            // 
            this.lblRootFolderStatus.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblRootFolderStatus.AutoSize = true;
            this.lblRootFolderStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblRootFolderStatus.ForeColor = System.Drawing.Color.DimGray;
            this.lblRootFolderStatus.Location = new System.Drawing.Point(210, 32);
            this.lblRootFolderStatus.Name = "lblRootFolderStatus";
            this.lblRootFolderStatus.Size = new System.Drawing.Size(159, 15);
            this.lblRootFolderStatus.TabIndex = 1;
            this.lblRootFolderStatus.Text = "حالة المجلد: غير جاهز (Not Set)";
            // 
            // btnGoogleLogin
            // 
            this.btnGoogleLogin.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnGoogleLogin.Location = new System.Drawing.Point(10, 24);
            this.btnGoogleLogin.Name = "btnGoogleLogin";
            this.btnGoogleLogin.Size = new System.Drawing.Size(180, 30);
            this.btnGoogleLogin.TabIndex = 0;
            this.btnGoogleLogin.Text = "تسجيل الدخول (Sign in)";
            this.btnGoogleLogin.UseVisualStyleBackColor = true;
            this.btnGoogleLogin.Click += new System.EventHandler(this.btnGoogleLogin_Click);
            // 
            // grpUpload
            // 
            this.grpUpload.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpUpload.Controls.Add(this.numPoll);
            this.grpUpload.Controls.Add(this.label8);
            this.grpUpload.Controls.Add(this.txtExportFolder);
            this.grpUpload.Controls.Add(this.label7);
            this.grpUpload.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpUpload.Location = new System.Drawing.Point(12, 310);
            this.grpUpload.Name = "grpUpload";
            this.grpUpload.Size = new System.Drawing.Size(576, 95);
            this.grpUpload.TabIndex = 3;
            this.grpUpload.TabStop = false;
            this.grpUpload.Text = "3. إعدادات رفع الملفات (Upload)";
            // 
            // numPoll
            // 
            this.numPoll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.numPoll.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.numPoll.Location = new System.Drawing.Point(365, 58);
            this.numPoll.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numPoll.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPoll.Name = "numPoll";
            this.numPoll.Size = new System.Drawing.Size(80, 23);
            this.numPoll.TabIndex = 5;
            this.numPoll.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numPoll.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label8.Location = new System.Drawing.Point(450, 60);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(115, 15);
            this.label8.TabIndex = 4;
            this.label8.Text = "معدل الفحص (ثواني):";
            // 
            // txtExportFolder
            // 
            this.txtExportFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtExportFolder.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtExportFolder.Location = new System.Drawing.Point(10, 28);
            this.txtExportFolder.Name = "txtExportFolder";
            this.txtExportFolder.Size = new System.Drawing.Size(430, 23);
            this.txtExportFolder.TabIndex = 3;
            this.txtExportFolder.Text = "D:\\PDF";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label7.Location = new System.Drawing.Point(450, 31);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(108, 15);
            this.label7.TabIndex = 2;
            this.label7.Text = "مسار التصدير (PDF):";
            // 
            // grpWhatsApp
            // 
            this.grpWhatsApp.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpWhatsApp.Controls.Add(this.chkAddQrToReceipt);
            this.grpWhatsApp.Controls.Add(this.txtReceiptCaption);
            this.grpWhatsApp.Controls.Add(this.label5);
            this.grpWhatsApp.Controls.Add(this.txtMsgPrefix);
            this.grpWhatsApp.Controls.Add(this.label4);
            this.grpWhatsApp.Controls.Add(this.chkSendLinkWhatsApp);
            this.grpWhatsApp.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpWhatsApp.Location = new System.Drawing.Point(12, 415);
            this.grpWhatsApp.Name = "grpWhatsApp";
            this.grpWhatsApp.Size = new System.Drawing.Size(576, 180);
            this.grpWhatsApp.TabIndex = 4;
            this.grpWhatsApp.TabStop = false;
            this.grpWhatsApp.Text = "4. رسائل الواتس آب";
            // 
            // chkAddQrToReceipt
            // 
            this.chkAddQrToReceipt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkAddQrToReceipt.AutoSize = true;
            this.chkAddQrToReceipt.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.chkAddQrToReceipt.ForeColor = System.Drawing.Color.DarkGreen;
            this.chkAddQrToReceipt.Location = new System.Drawing.Point(270, 145);
            this.chkAddQrToReceipt.Name = "chkAddQrToReceipt";
            this.chkAddQrToReceipt.Size = new System.Drawing.Size(280, 19);
            this.chkAddQrToReceipt.TabIndex = 5;
            this.chkAddQrToReceipt.Text = "طباعة QR Code يحتوي على لينك النتائج في الإيصال";
            this.chkAddQrToReceipt.UseVisualStyleBackColor = true;
            // 
            // txtReceiptCaption
            // 
            this.txtReceiptCaption.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtReceiptCaption.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtReceiptCaption.Location = new System.Drawing.Point(10, 105);
            this.txtReceiptCaption.Name = "txtReceiptCaption";
            this.txtReceiptCaption.Size = new System.Drawing.Size(400, 23);
            this.txtReceiptCaption.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label5.Location = new System.Drawing.Point(420, 108);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(157, 15);
            this.label5.TabIndex = 3;
            this.label5.Text = "النص أسفل الـ QR في الإيصال:";
            // 
            // txtMsgPrefix
            // 
            this.txtMsgPrefix.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMsgPrefix.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtMsgPrefix.Location = new System.Drawing.Point(10, 50);
            this.txtMsgPrefix.Multiline = true;
            this.txtMsgPrefix.Name = "txtMsgPrefix";
            this.txtMsgPrefix.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtMsgPrefix.Size = new System.Drawing.Size(400, 45);
            this.txtMsgPrefix.TabIndex = 2;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label4.Location = new System.Drawing.Point(420, 53);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(160, 15);
            this.label4.TabIndex = 1;
            this.label4.Text = "نص الرسالة قبل اللينك (Prefix):";
            // 
            // chkSendLinkWhatsApp
            // 
            this.chkSendLinkWhatsApp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkSendLinkWhatsApp.AutoSize = true;
            this.chkSendLinkWhatsApp.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkSendLinkWhatsApp.Location = new System.Drawing.Point(295, 25);
            this.chkSendLinkWhatsApp.Name = "chkSendLinkWhatsApp";
            this.chkSendLinkWhatsApp.Size = new System.Drawing.Size(253, 19);
            this.chkSendLinkWhatsApp.TabIndex = 0;
            this.chkSendLinkWhatsApp.Text = "إرسال لينك النتائج تلقائياً على واتساب بعد الرفع";
            this.chkSendLinkWhatsApp.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSave.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnSave.ForeColor = System.Drawing.Color.DarkGreen;
            this.btnSave.Location = new System.Drawing.Point(120, 630);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(120, 35);
            this.btnSave.TabIndex = 5;
            this.btnSave.Text = "حفظ الإعدادات";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnCancel.ForeColor = System.Drawing.Color.DimGray;
            this.btnCancel.Location = new System.Drawing.Point(12, 630);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 35);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "إلغاء";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnDefaults
            // 
            this.btnDefaults.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefaults.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnDefaults.ForeColor = System.Drawing.Color.DarkBlue;
            this.btnDefaults.Location = new System.Drawing.Point(250, 630);
            this.btnDefaults.Name = "btnDefaults";
            this.btnDefaults.Size = new System.Drawing.Size(130, 35);
            this.btnDefaults.TabIndex = 7;
            this.btnDefaults.Text = "استعادة الافتراضي";
            this.btnDefaults.UseVisualStyleBackColor = true;
            this.btnDefaults.Click += new System.EventHandler(this.btnDefaults_Click);
            // 
            // btnUndoSql
            // 
            this.btnUndoSql.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUndoSql.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.btnUndoSql.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnUndoSql.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Bold);
            this.btnUndoSql.ForeColor = System.Drawing.Color.Maroon;
            this.btnUndoSql.Location = new System.Drawing.Point(10, 19);
            this.btnUndoSql.Name = "btnUndoSql";
            this.btnUndoSql.Size = new System.Drawing.Size(130, 25);
            this.btnUndoSql.TabIndex = 99;
            this.btnUndoSql.Text = "Undo SQL Changes";
            this.btnUndoSql.UseVisualStyleBackColor = false;
            this.btnUndoSql.Click += new System.EventHandler(this.btnUndoSql_Click);
            // 
            // ResultsLinkSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ClientSize = new System.Drawing.Size(600, 680);
            this.Controls.Add(this.btnDefaults);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.grpWhatsApp);
            this.Controls.Add(this.grpUpload);
            this.Controls.Add(this.grpGoogle);
            this.Controls.Add(this.grpDb);
            this.Controls.Add(this.chkEnabled);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ResultsLinkSettingsForm";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "إعدادات لينك النتائج (Google Drive Link)";
            this.grpDb.ResumeLayout(false);
            this.grpDb.PerformLayout();
            this.grpGoogle.ResumeLayout(false);
            this.grpGoogle.PerformLayout();
            this.grpUpload.ResumeLayout(false);
            this.grpUpload.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPoll)).EndInit();
            this.grpWhatsApp.ResumeLayout(false);
            this.grpWhatsApp.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox chkEnabled;

        private System.Windows.Forms.GroupBox grpDb;
        private System.Windows.Forms.Button btnTestDb;
        private System.Windows.Forms.Button btnInitDb;

        private System.Windows.Forms.CheckBox chkUseManualSql;
        private System.Windows.Forms.Label labelSqlServer;
        private System.Windows.Forms.TextBox txtSqlServer;
        private System.Windows.Forms.Label labelSqlDb;
        private System.Windows.Forms.TextBox txtSqlDb;
        private System.Windows.Forms.Label labelSqlUser;
        private System.Windows.Forms.TextBox txtSqlUser;
        private System.Windows.Forms.Label labelSqlPass;
        private System.Windows.Forms.TextBox txtSqlPass;

        private System.Windows.Forms.GroupBox grpGoogle;
        private System.Windows.Forms.Button btnGoogleLogin;
        private System.Windows.Forms.Label lblRootFolderStatus;

        private System.Windows.Forms.GroupBox grpWhatsApp;
        private System.Windows.Forms.CheckBox chkSendLinkWhatsApp;
        private System.Windows.Forms.CheckBox chkAddQrToReceipt;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtMsgPrefix;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtReceiptCaption;

        private System.Windows.Forms.GroupBox grpUpload;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txtExportFolder;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown numPoll;

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnDefaults;
        private System.Windows.Forms.Button btnUndoSql;
    }
}