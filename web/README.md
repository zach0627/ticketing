# web — 前端

線上購票系統的前端：React 19 ＋ TypeScript ＋ Vite ＋ React Router ＋ TanStack Query。
後端在 [`../src`](../src)，整體說明看 [根目錄 README](../README.md)。

## 啟動

```bash
cp .env.example .env      # 設定 API 位址與 Google Client ID
npm ci
npm run dev               # http://localhost:5173
```

前端與 API 是**不同 origin**，本機就走真的跨網域 ＋ CORS，不用 dev proxy 把問題藏起來——
部署後才發現 CORS 沒設對是最沒必要的意外。

```bash
npm run build             # 型別檢查 ＋ 產生 dist/
npm run lint              # Oxlint
```

## 結構

```
src/
  app/         router、版面、全域 provider
  features/    依功能切：catalog、auth、booking、orders、admin
  shared/api   唯一碰 fetch 的地方
  shared/ui    共用元件
  shared/utils 格式化與倒數計時
```

- `shared/api/http.ts` 是唯一碰 `fetch` 的地方：統一帶 `Authorization`、統一把
  ProblemDetails 解成 `ApiError`、統一處理 401。各功能只呼叫 `apiGet`／`apiPost`／`apiPatch`。
  401 不在這裡導頁——`/auth/*` 自己回的 401 是「帳密錯」，導走會變成無限循環。
- **座位狀態與到期時間一律以後端回應為準。** 倒數計時只是顯示，過期與否由 API 判定；
  前端不維護自己的一份真相，否則兩邊會不一致。
- 需要登入的路由包在 `RequireAuth`／`RequireAdmin`，但那只是使用者體驗——
  真正的授權在後端，前端擋不住的東西後端一定要擋。

## 環境變數

| 變數 | 用途 |
|---|---|
| `VITE_API_BASE_URL` | 後端 API 位址（例：`http://localhost:5080/api/v1`） |
| `VITE_GOOGLE_CLIENT_ID` | Google 登入用。**這是公開值**，本來就會出現在前端原始碼與每一個網路請求裡；沒有它時 Google 按鈕不顯示，Email 登入照常可用 |

`.env` 不進版控，`.env.example` 進。
