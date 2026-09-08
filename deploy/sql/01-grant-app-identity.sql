/*
  把 App Service 的 managed identity 授權進 ticketing 資料庫。

  為什麼需要這一步：
    Azure SQL 這台伺服器設成 **Entra-only 驗證**，沒有 SQL 帳號密碼可用。
    App Service 用 system-assigned managed identity 拿 token 連線，
    但「能通過驗證」不等於「有權限」——資料庫這邊還是要有一個對應的 user。

  為什麼不是 db_datareader + db_datawriter：
    那兩個內建角色的權限是「所有表」，而且未來新增的表會自動涵蓋進去。
    這裡改用自訂角色，權限寫在同一個地方看得到，而且可以針對單一張表再收緊。

  為什麼 app 沒有 DDL 權限：
    migration 由開發者／CI 用**自己的**身分套用，不是 app 啟動時自己跑
    （Program.cs 沒有 db.Migrate()）。所以 runtime 身分不需要 ALTER/CREATE，
    也就不該有——這樣就算 app 被打穿，攻擊者也改不了 schema。

  執行方式：
    Azure Portal → SQL database → Query editor，用「你自己的」Entra 帳號登入後貼上執行。
    （必須是 Entra admin 身分；App Service 自己沒有權限建立自己。）
    整份是單一批次、沒有 GO，Query editor 可以直接跑。
    重複執行是安全的。
*/

-- App Service 的資源名稱。system-assigned managed identity 的顯示名稱與它同名。
-- 換環境就改這一行。
DECLARE @AppIdentity sysname = N'api-ticketing-zach0627';
DECLARE @sql nvarchar(max);

-- 1) 為 managed identity 建立 contained user（沒有密碼，token 驗證）
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @AppIdentity)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@AppIdentity) + N' FROM EXTERNAL PROVIDER;';
    EXEC sys.sp_executesql @sql;
END

-- 2) 最小權限角色
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ticketing_runtime' AND type = 'R')
    CREATE ROLE ticketing_runtime;

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO ticketing_runtime;

SET @sql = N'ALTER ROLE ticketing_runtime ADD MEMBER ' + QUOTENAME(@AppIdentity) + N';';
EXEC sys.sp_executesql @sql;

-- 3) 稽核表對 app 而言是 append-only。
--    AdminDao 對 AdminAudits 只有 SELECT 與 Add()（沒有任何 update／delete 路徑），
--    連「重置」都刻意不刪它——因為重置的去重證據就存在這裡。
--    DENY 的優先權高於 GRANT，所以就算之後有人不小心加了 UPDATE 的程式碼，
--    資料庫這一層也會擋下來。
DENY UPDATE, DELETE ON OBJECT::dbo.AdminAudits TO ticketing_runtime;

-- 4) 驗收：印出實際生效的權限
SELECT  m.name       AS member_name,
        m.type_desc  AS member_type,
        r.name       AS role_name
FROM    sys.database_role_members rm
JOIN    sys.database_principals r ON r.principal_id = rm.role_principal_id
JOIN    sys.database_principals m ON m.principal_id = rm.member_principal_id
WHERE   r.name = N'ticketing_runtime';

SELECT  p.permission_name,
        p.state_desc,
        p.class_desc,
        ISNULL(OBJECT_NAME(p.major_id), N'(schema dbo)') AS target
FROM    sys.database_permissions p
JOIN    sys.database_principals dp ON dp.principal_id = p.grantee_principal_id
WHERE   dp.name = N'ticketing_runtime'
ORDER BY p.state_desc DESC, p.permission_name;
