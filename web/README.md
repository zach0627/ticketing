# 線上購票系統前端

React、TypeScript、Vite、React Router 與 TanStack Query。後端在 [`../src`](../src)，系統規則與部署說明見[根目錄 README](../README.md)。

## 開發與驗證

```bash
cp .env.example .env
npm ci
npm run dev      # http://localhost:5173
npm test         # 純函式、依賴邊界與 React 流程測試
npm run lint
npm run build    # 應用程式與 React 測試的型別檢查，再產生 dist/
```

前端與 API 使用不同 origin，本機直接驗證 CORS。環境變數 `VITE_API_BASE_URL` 指向 API（本機為 `http://localhost:5080/api/v1`）；`VITE_GOOGLE_CLIENT_ID` 是 Google 登入的公開 client ID。`.env` 不進版控，未設定 Google client ID 時仍可使用 Email 登入。

## 目錄與責任

```text
src/
  app/
    router.tsx          路由與頁面動態載入
    providers.tsx       QueryClient、Google Identity、Auth 的組裝
    AppLayout.tsx       頁面外框與路由狀態重置
    components/         SiteHeader、SiteFooter
    pages/              購票指南、找不到頁面
    styles/             tokens、基礎樣式、共用版面
  features/
    catalog/            活動瀏覽、搜尋、篩選、詳情
    auth/               登入、註冊、帳號與登入狀態
    booking/            選位、保留、付款及操作恢復
    orders/             訂單列表、訂單明細、電子票券
    admin/              後台查詢、暫停售票、資料重置
  shared/
    api/                HTTP、ProblemDetails、分頁契約、操作 UUID
    ui/                 通用元件與各自的樣式
    utils/format.ts     日期與金額格式
```

每個功能再依責任切分：

| 目錄 | 責任 | 依賴限制 |
|---|---|---|
| `pages/` | 組裝 hooks、元件，顯示 loading／error | 不直接呼叫 API 或 React Query |
| `components/` | 用 props 呈現內容與傳遞使用者操作 | 不執行資料查詢；獨立互動可使用專用 hook |
| `hooks/` | 查詢生命週期、表單、導航與使用者流程 | 不含 JSX 或 CSS，不反向依賴頁面與呈現元件 |
| `api/` | 端點、查詢定義及外部儲存 adapter | HTTP 統一經 `shared/api/http.ts` |
| `model/` | 各自的 DTO、型別與純計算 | 不依賴 React、路由或 HTTP 執行期程式 |
| `styles/` | 該功能原有的各頁／元件 CSS | 由擁有者匯入，避免所有樣式集中到全域檔案 |
| `index.ts` | 必要的跨功能公開入口 | 不匯出頁面，維持路由延後載入 |

Auth 另有 `context/` 保存登入狀態的公開 context／provider、`guards/` 保存路由守衛。Provider 組裝 context，session 恢復與清除快取由 `useAuthSession` 負責。Admin 的路由守衛也獨立放在 `guards/`。

跨功能使用公開入口，例如 booking 使用 catalog 的 `catalogQueries`／DTO、auth 的 `useAuth`、orders 的訂單契約；不穿透其他功能的內部檔案。`shared` 不依賴任何 feature。路由表是唯一可以跨功能直接動態匯入 `pages/` 的地方。

## 流程邊界

- `HomePage → useCatalog → catalogQueries`：URL 保存查詢條件，`useCatalogSearch` 獨立處理中文組字、延遲提交與外部 URL 同步。
- `SeatSelectionPage → useSeatSelection → useSeatMap / useReservation`：選位狀態與事件集中管理；`SeatPicker`、`SelectionSummary`、`ReservationButton` 只接受資料及操作 callback。
- `HoldPage → useHoldDetails → useCheckout / useCountdown`：保留讀取、關聯活動與到期更新集中在 hook；保留狀態、倒數、座位明細和付款收據各自呈現。
- 登入與註冊各有表單元件及 hook，共用 `useAuthReturn` 接續安全返回路徑和選位草稿。Google 憑證交換由 `useGoogleSignIn` 處理。
- 訂單查詢與分頁由 `useOrders`／`useOrderDetail` 負責；後台的資料與篩選由 `useAdminDashboard`、寫入與回饋由 `useAdminActions` 負責。

座位狀態、保留是否到期、授權及購票限制仍由 API 最終裁決。HTTP 統一處理 Bearer、ProblemDetails 及受保護請求的 401；登入端點的 401 保留在表單，不觸發全域導頁。

## 規則、證據與代價

| 保護的規則 | 放置位置 | 驗證 | 代價 |
|---|---|---|---|
| 呈現不承擔資料存取，依賴不形成循環 | 各功能責任目錄、公開入口 | `architecture.test.mjs` | 多個小型模組，需維護公開匯出 |
| 組字過程不提交半成品或覆蓋新輸入 | `useCatalogSearch` | `CatalogSearch.test.tsx` | 保存輸入草稿與最後一次提交值 |
| 不跨區、超量或使用過時選位新增保留 | `selection`、`useSeatSelection` | `selection.test.mjs`、`seatSelection.test.tsx` | 本地預檢，伺服器仍須驗證 |
| 不把未知結果的重試當成新購買 | `useReservation`、`useCheckout`、`pendingOperations` | `bookingFlows.test.tsx`、`recovery.test.mjs` | 保存使用者／資源／key／payload，結果未明時鎖定操作 |
| 保留、訂單、後台查詢保有原有更新行為 | 頁面 hooks、`catalogQueries` | `pageControllers.test.tsx`、`countdown.test.mjs` | 管理查詢生命週期與到期檢查 |
| 登入後接續原流程並保留錯誤回饋 | Auth hooks、`useAuthReturn` | `authFlows.test.tsx` | 維護返回狀態與表單狀態 |

這次重構保留 API 路徑、payload、query key、重試策略、sessionStorage key、CSS 宣告及活動素材，不增加狀態管理或 UI 套件。架構測試由既有 `npm test` 與 GitHub CI 持續執行。
