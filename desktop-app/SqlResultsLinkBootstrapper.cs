using System;
using System.Data.SqlClient;

namespace WhatsApp_Auto_Sender
{
    public static class SqlResultsLinkBootstrapper
    {
        public static void EnsureInstalled(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("SqlConnectionString غير مضبوط.");

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                string sql = @"
-- =============================
-- 1) جداولنا الأساسية
-- =============================

IF OBJECT_ID('dbo.WA_ResultLinkQueue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WA_ResultLinkQueue
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PatientId VARCHAR(13) NOT NULL,
        RequestDate SMALLDATETIME NULL,
        CreatedAt DATETIME NOT NULL DEFAULT(GETDATE()),
        Status TINYINT NOT NULL DEFAULT(0), -- 0=Pending,1=Processing,2=Done,3=Failed
        Attempts INT NOT NULL DEFAULT(0),
        LastError NVARCHAR(4000) NULL
    );
END

IF OBJECT_ID('dbo.WA_PatientDriveLinks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WA_PatientDriveLinks
    (
        PatientId VARCHAR(13) NOT NULL PRIMARY KEY,
        FolderId NVARCHAR(200) NOT NULL,
        FolderUrl NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT(GETDATE())
    );
END

-- =============================
-- 2) جدول متابعة المرضى الجدد (30 يوم)
-- =============================
IF OBJECT_ID('dbo.WA_PatientWatch', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WA_PatientWatch
    (
        PatientId VARCHAR(13) NOT NULL PRIMARY KEY,
        CreatedAt DATETIME NOT NULL DEFAULT(GETDATE()),
        WatchUntil DATETIME NOT NULL,              -- هنخليها GETDATE()+30 يوم
        LastSignature NVARCHAR(64) NULL,           -- بصمة ملفات فولدر المريض (hash)
        LastSyncedAt DATETIME NULL,
        LastError NVARCHAR(4000) NULL
    );
END

-- =============================
-- 3) Trigger: التريجر السحري (المعدل)
--    - يكتب WhatsApp & Print Service
--    - يفعل زرار الإيميل (moreinfo2)
--    - يضيف المريض للمتابعة
-- =============================

IF OBJECT_ID('dbo.TR_WA_PatientInfo_Insert', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_WA_PatientInfo_Insert;
GO

CREATE TRIGGER dbo.TR_WA_PatientInfo_Insert
ON dbo.patientinfo
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- (A) تظبيط الإيميل + تفعيل زرار الإرسال
    UPDATE dbo.patientinfo
    SET 
        email = 'WhatsApp & Print Service', -- التعديل الجديد هنا
        moreinfo2 = '0000001000'            -- كود تفعيل الزرار
    FROM dbo.patientinfo p
    INNER JOIN inserted i ON p.patientid = i.patientid
    WHERE 
       -- بنطبق ده بس لو الإيميل فاضي أو مش مظبوط
       (i.email IS NULL OR LTRIM(RTRIM(i.email)) = '' OR i.email NOT LIKE '%@%');

    -- (B) WA_PatientWatch: متابعة 30 يوم
    INSERT INTO dbo.WA_PatientWatch (PatientId, WatchUntil)
    SELECT i.patientid, DATEADD(DAY, 30, GETDATE())
    FROM inserted i
    WHERE i.patientid IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WA_PatientWatch w WHERE w.PatientId = i.patientid);

    -- (C) WA_ResultLinkQueue: إنشاء اللينك
    INSERT INTO dbo.WA_ResultLinkQueue (PatientId, RequestDate)
    SELECT i.patientid, GETDATE()
    FROM inserted i
    WHERE i.patientid IS NOT NULL;
END
GO
";

                // تقسيم السكريبت وتنفيذه
                var parts = sql.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    using (var cmd = new SqlCommand(part, con))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void Uninstall(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("SqlConnectionString غير مضبوط.");

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();

                string sql = @"
            -- 1. حذف التريجر (الزناد)
            IF OBJECT_ID('dbo.TR_WA_PatientInfo_Insert', 'TR') IS NOT NULL
                DROP TRIGGER dbo.TR_WA_PatientInfo_Insert;

            -- 2. حذف جداول البرنامج الخاصة
            
            -- جدول طابور الروابط
            IF OBJECT_ID('dbo.WA_ResultLinkQueue', 'U') IS NOT NULL
                DROP TABLE dbo.WA_ResultLinkQueue;

            -- جدول روابط الدرايف
            IF OBJECT_ID('dbo.WA_PatientDriveLinks', 'U') IS NOT NULL
                DROP TABLE dbo.WA_PatientDriveLinks;

            -- جدول المتابعة
            IF OBJECT_ID('dbo.WA_PatientWatch', 'U') IS NOT NULL
                DROP TABLE dbo.WA_PatientWatch;

            -- (الجدول الناقص) جدول طابور الرفع
            IF OBJECT_ID('dbo.WA_ResultUploadQueue', 'U') IS NOT NULL
                DROP TABLE dbo.WA_ResultUploadQueue;
        ";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.CommandTimeout = 120;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}