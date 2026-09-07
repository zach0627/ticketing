# AGENTS.md — 線上購票系統

劃位購票網站，面試作品。設計文件在 Obsidian vault `簡易購票網站開發/`（00～22），**程式碼的唯一權威規格在那裡**，本檔只是摘要。

## 最高原則：功能收斂，工程責任完整

產品範圍刻意小，但**不減免**一致性、安全、故障恢復與驗收。不接受「為了簡單所以省掉」。
每個機制都要能回答四件事：保護什麼規則、放在哪一層、用什麼測試證明、增加什麼代價。

## 命名（對外一律專業）

- 對外名稱是**線上購票系統**（英文 `ticketing`）。README、網站 title、OpenAPI 標題、Google OAuth App name、Azure 資源都用它。
- **「簡易」「demo」「示範」不得出現在**程式碼、註解、UI 文案、錯誤訊息、設定鍵、seed 資料、log 事件名。那些字只存在於設計文件，是範圍訊號，不是產品的一部分。
- 例外：票券頁必須保留「票券無實際入場效力」，這是誠信標示。

## 架構與依賴方向

```
Domain（零套件） ← Application ← Infrastructure ← Api
```

- 依賴方向錯了是編譯錯誤，不是風格問題。
- Domain 不掛任何 EF attribute；EF 對應只寫在 `Infrastructure/Persistence/Configurations/`。
- Application 不出現 `using Microsoft.EntityFrameworkCore`、不寫 SQL、不認識 `HttpContext`。
- Controller 不開交易、不碰 `DbContext`、不寫規則；只呼叫 `IXxxService` 或 `IXxxQueries`。
- 有聚合、有規則的走 **Repository**（介面在 Domain）；純資料表或跨表批次走 **DAO**（介面在 `Application/Abstractions/`）。
- 不建 `GenericRepository<T>`。不安裝 AutoMapper、MediatR（商業授權）。
- 對應層用 **Mapperly**，只走 Entity → DTO 與 `ProjectToXxx()`，不做反向。`RMG012`／`RMG020` 已在 `.editorconfig` 設為 error。
- 介面只給「跨專案邊界」或「測試需要替身」的東西；新增介面要能寫出這一句理由。
- 一個型別一個檔案。**套件版本只寫在 `Directory.Packages.props`。**

## 併發與交易（核心）

- 防超賣＝**條件 UPDATE ＋ 唯一索引 ＋ 交易**。座位條件更新只寫在 `SeatRepository`，鎖定存取在 `SqlBookingWriteGate`。
- 購票寫入固定順序：場次共享 gate → 買家更新 gate → 取 now／查冪等 → 重新載入驗證 → 寫入。
- 不用程序內 lock、不用 `sp_getapplock`。
- 併發測試必須用 Testcontainers 的**真 SQL Server**，不用 InMemory／SQLite。

## 指令

```bash
dotnet build
dotnet test tests/Ticketing.Domain.Tests          # 秒級，不需要 Docker
dotnet test tests/Ticketing.Application.Tests     # 秒級，NSubstitute 替身
dotnet test tests/Ticketing.Api.Tests             # 需要 Docker

dotnet run --project src/Ticketing.Api --urls http://localhost:5080
cd web && npm run dev                             # http://localhost:5173

# migration 在 Infrastructure，啟動專案是 Api，兩個都要指定
dotnet ef migrations add <Name> \
  --project src/Ticketing.Infrastructure --startup-project src/Ticketing.Api --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/Ticketing.Infrastructure --startup-project src/Ticketing.Api

dotnet run --project src/Ticketing.Api -- seed
dotnet run --project src/Ticketing.Api -- promote-admin <email>
```

**不在 API 啟動時自動 migrate 或 seed。**

## 安全

- 機密一律 `dotnet user-secrets`（本機）或 App Service 設定（雲端）。**不寫進程式、文件、git、log 或對話**，一律 `YOUR_VALUE_HERE`。
- `appsettings.Development.json` 會進 git，只放非機密設定。
- 錯誤走 `BookingRuleException` → `ApiExceptionHandler` → ProblemDetails，含 `code` 與 `traceId`。別人的資源回 404，不是 403。

## 不做

真金流、寄信、簡訊、退款、微服務、Kubernetes、Event Sourcing、Redis、MediatR、FluentValidation（暫）。
候位室、refresh token、OpenTelemetry 等擴充提案在設計文件 17，**完成階段 0～9 之前不碰**。

## 開發節奏

一次推進一個 Roadmap 階段（設計文件 11），每階段結束用設計文件 12 第 8 節的模板回報。
架構或一致性策略要改，先寫 ADR（設計文件 12 第 6 節）再改程式。
