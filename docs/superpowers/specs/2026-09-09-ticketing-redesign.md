# 線上購票系統介面改版

狀態：implemented。使用者授權重做介面、活動圖文、commit、部署前後端及驗證 Azure；全程不使用 sub agent。

## 視覺與元件

參考 [遠大售票](https://ticketplus.com.tw/) 與 [KKTIX](https://kktix.com/) 的實際畫面，再依使用者最新回饋，以遠大的元件比例為主要依據。

- 白色導覽與內容、淺灰首頁底色、藍色操作與選取狀態。正文 16px，輔助資訊 14px，座位圖中的數字與圖例可用 12px；品牌副標除外。
- 自託管 Roboto 400／500／700，中文與英文共用 `Roboto, sans-serif`，與參考網站採相同字型設定。
- 卡片採三欄、8:5 圖片、2px 淺灰框、8px 圓角、16px 內距；標題 18px／500／1.4，最多兩行，日期與票價同列。手機標題 16px，窄螢幕單欄，平板雙欄。
- 首頁置中精選輪播及兩側預覽、白色搜尋區、分類分頁、三欄固定對齊篩選、結果列及卡片。清除篩選不改變欄寬。
- 下拉選單共用 Radix Select：一致的白底選單、選中勾號、藍色邊框與焦點、鍵盤方向鍵／Enter／Escape、視窗邊界定位及捲動。
- 中文搜尋保留本地輸入草稿；組字期間不更新網址，完成後等待 200ms 再搜尋。外部清除與網址條件同步。
- 活動詳情使用完整 3:2 圖像、活動資訊、場次票價、購票須知、側欄與手機底部購票入口；導覽標示目前閱讀段落。
- 購票三步驟：選位、確認與付款、取得票券。座位圖有文字、符號和獨立橫向捲動；手機保留底部摘要及操作。
- 登入、註冊、帳號、訂單、票券、後台、空狀態、錯誤、404 與指南使用相同設計。票券必須保留「票券無實際入場效力」。

## 素材與資料

重新產生 12 場演唱會與 3 場運動賽事圖像，包含兩組虛構 K-pop 團體、華語流行、搖滾、R&B 等類型。原始 PNG 及提示保存於 `docs/assets/event-covers/`；網站使用有版本的 WebP，共約 2.3 MB。標題以 HTML 呈現。

seed 與 `RefreshEventPresentation` migration 同步更新 15 場活動的名稱、演出者、類型、介紹和圖片。Migration 以 Id 及 Code 定位，只更新呈現欄位；不改活動 ID、日期、票價、座位、訂單、保留、冪等資料。不重新 seed 或 reset 線上資料。舊 PNG 保留以供舊前端快取使用。

## 架構與取捨

沿用 React、Router、TanStack Query 與既有 HTTP 層。`app` 組合路由及共用外框，`features` 管理各使用案例，`shared` 提供 API 工具與跨頁元件。路由依功能延後載入。

CSS 按功能與元件共同管理：全域只組合 tokens、base、版面與共用元件；catalog、auth、booking、orders、admin 各自載入樣式。小型呈現元件包括 EventCard、CatalogSearch、CatalogToolbar、EventInformation、EventTabs、SeatPicker、SeatGrid。保留與付款恢復抽至 useReservation／useCheckout，選位、篩選、倒數及輪詢策略另有純函式。

不遷移整套 UI 框架或 Tailwind；採用 Radix 的專用 Select 來處理容易出錯的選單焦點、選取與定位。新增的測試依賴只用於開發，正式 bundle 不包含測試執行器。

API 契約、授權、後端四層與 SQL 防超賣協定維持原設計。pending 操作綁定買家、操作與資源，未知結果沿用原 key／payload；換帳號不恢復他人的操作。背景分頁不輪詢，閒置十分鐘停止，重新操作時恢復。

## 驗收與部署

- 320、390、768、1024、1440px 實際檢查；頁面不橫向溢出，座位圖和管理表格可在自身容器捲動。
- 分類、中文搜尋、清除、排序、輪播、下拉開啟／選取／關閉、活動段落導覽、登入回跳、手選／連號、保留、付款失敗後成功、取消、訂單與登出。
- 前端純函式／元件測試、lint、build；.NET build、Domain／Application／真 SQL Server API 測試。
- Migration 整合測試以現有已付款訂單、有效保留驗證 Up／Down 均不改交易資料。
- GitHub CI 綠燈後，Deploy web／Deploy API 部署相同 SHA；新素材上線後才套用雲端 metadata migration，再核對健康檢查、15 圖與 API 圖文、SPA 深層路由及正式畫面。
