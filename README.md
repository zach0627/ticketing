# 線上購票系統

劃位購票網站：15 場虛構活動（12 場演唱會、3 場運動賽事）、2,880 個座位，含註冊登入、選座保留、模擬付款、訂單查詢與管理後台。

後端刻意做完整的分層與一致性保證：**Clean Architecture 四專案、Repository／DAO、DTO ＋ 對應層、IoC、資料庫交易、稽核、冪等**。功能範圍收斂，工程責任完整。

> **票券無實際入場效力。** 本站不處理真實金流，付款為模擬流程。

## 目前進度

| 階段 | 內容 | 狀態 |
|---|---|---|
| 1 | 骨架：方案、七個專案、中央套件管理、CI | ✅ |
| 2 | Domain：實體、規則、Repository 介面 | ✅ |
| 3 | Infrastructure：EF、migration、seed | ✅ |
| 4 | 公開瀏覽（第一條垂直切片） | ✅ |
| 5 | 註冊登入（密碼 ＋ Google） | ✅ |
| 6 | 保留與付款 | ✅ |
| 7 | 管理後台與背景通知 | ✅ |
| 8 | Azure 部署 | ⬜ |

## 架構

```
Ticketing.Domain          零套件。實體、值物件、Repository 介面、IUnitOfWork
   ↑
Ticketing.Application     使用案例、DTO、對應器、對外部服務的介面
   ↑
Ticketing.Infrastructure  EF Core、Repository 實作、DAO、查詢投影、JWT、Google、Channel
   ↑
Ticketing.Api             Controller、ProblemDetails、驗證授權、CORS、限流、組裝
```

依賴方向只朝內，**由編譯器保證**：在 `Ticketing.Application` 寫 `using Microsoft.EntityFrameworkCore;` 會得到 `error CS0234`，因為它根本沒有那個參照。`tests/*/ArchitectureGuardTests.cs` 另外用測試釘住「Domain 零套件」與「Application 不碰 EF」。

## 開發

需求：.NET 10 SDK、Node 22+、Docker。

```bash
dotnet build                                   # 整個方案
dotnet test                                    # 三層測試
dotnet run --project src/Ticketing.Api --urls http://localhost:5080
cd web && cp .env.example .env && npm run dev   # http://localhost:5173
```

第一次跑之前要設一個 JWT 簽章金鑰，否則 API 會**刻意**啟動失敗（少了金鑰的服務不該起得來）：

```bash
cd src/Ticketing.Api && dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

資料庫（本機 Docker SQL Server，另建 `Ticketing` 資料庫）與 migration／seed 指令見 `AGENTS.md`。

## 測試

| 層 | 證明什麼 | 工具 |
|---|---|---|
| `Ticketing.Domain.Tests` | 規則對 | xUnit，秒級，不需要 Docker |
| `Ticketing.Application.Tests` | 流程對 | xUnit ＋ NSubstitute ＋ FakeTimeProvider |
| `Ticketing.Api.Tests` | 資料對 | xUnit ＋ WebApplicationFactory ＋ Testcontainers（真 SQL Server） |

開發機是 Apple Silicon，SQL Server 容器在 arm64 上是 Microsoft 官方未支援的模擬環境。**併發正確性的權威結果以 CI 的 x86-64 runner 為準**（`.github/workflows/ci.yml` 的 `integration` job），本機執行只作為快速回饋。

## 機密

repo 內沒有任何機密。本機設定走 `dotnet user-secrets`（存在 `~/.microsoft/usersecrets/`，在 repo 之外），雲端走 App Service 應用程式設定與 Managed Identity。三道防線：`.gitignore`、GitHub Push Protection、CI 的 gitleaks job。

Google Client ID 是公開值，會出現在前端原始碼，這是設計上正確的。
