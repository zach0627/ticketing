# 公開 README 整理設計

## 目標

把根目錄 README 整理成可快速瀏覽的公開作品頁。讀者不需要知道專案的開發歷程或私人設計文件，也能在短時間內理解產品用途、主要功能、架構邊界、啟動方式與驗證方式。

## 內容順序

1. 專案名稱、簡介、誠信聲明、CI 狀態與線上網址。
2. 三張實際畫面：活動列表、活動詳情、選座。
3. 功能說明：使用者流程、管理功能與關鍵一致性保證。
4. 簡明架構圖：Domain、Application、Infrastructure、Api 與 Web 的責任和依賴方向。
5. 快速開始：必要工具、非機密設定、資料庫 migration、seed、API 與前端啟動方式。
6. 驗證指令：後端 build/test 與前端 lint/build。

## 編輯原則

- 保留「票券無實際入場效力」，並說明活動、演出者與場館皆為虛構。
- 不使用面試導向措辭，不提私人筆記庫位置，不保留階段進度表。
- 技術細節只保留能解釋系統品質的內容；部署權限、雲端成本取捨與內部驗收案例不在 README 展開。
- 三張畫面已足以呈現產品視覺與核心選座流程；其餘能力用簡潔功能清單描述。
- 快速開始中的命令必須能由乾淨 checkout 依序執行，機密值只用 `YOUR_VALUE_HERE` 或由本機產生。
- 圖片與文件使用 repository-relative 連結，確保 GitHub 可直接顯示。

## 相關修改

- 根目錄 `README.md`：依上述結構重寫與濃縮。
- `web/README.md`：移除 Vite 範本內容，改為前端專案的啟動與結構說明。
- `web/index.html`：補正繁體中文語系、產品名稱與描述。
- `.gitignore`：忽略本機 agent／工具狀態。
- `AGENTS.md`：移除公開 repository 不需要的面試與私人路徑措辭。
- `docs/screenshots/`：納入三張已檢查的實際線上畫面。

## 驗收

- README 沒有面試導向措辭、私人筆記庫路徑或禁止使用的產品命名。
- 三張圖片存在，Markdown 連結可解析。
- 快速開始與 `AGENTS.md`、專案設定一致。
- `dotnet build`、適用的測試、`npm run lint` 與 `npm run build` 成功。
- 提交前檢查 staged diff 與敏感資訊；push 後確認 GitHub Actions 與 Azure 線上站點。
