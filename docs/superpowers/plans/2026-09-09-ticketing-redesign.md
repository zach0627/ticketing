# 線上購票系統介面改版執行清單

Goal：完成一致的白底售票介面、RWD、購票體驗並上線。

Architecture：沿用 app／features／shared 與唯一 HTTP 層；呈現元件、catalog 篩選與選位狀態分開。API 契約及後端四層不變。直接在本 task 執行，依使用者要求不分派 agent。

Tech stack：React 19、TypeScript、React Router、TanStack Query、CSS tokens。

- [x] 共用 tokens、頁首頁尾、圖示、載入／錯誤、404 與導頁體驗。
- [x] catalog 搜尋條件與測試、首頁主視覺、卡片、活動頁與購票入口。
- [x] 選位元件、票區與摘要、倒數／付款、訂單／票券、帳號及管理頁。
- [x] 新行為測試、建置與 lint；真瀏覽器桌機／手機視覺與購票驗證。
- [x] 檢查元件責任、API 邊界、安全／冪等、更新畫面文件。
- 交付：commit、push 後由 GitHub CI／Deploy web／Deploy API 留存部署結果；新圖上線後套用 metadata migration，核對正式入口與 API。
