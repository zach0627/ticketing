# Public README Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將公開 repository 的文件整理成精簡作品頁，完成驗證、提交、推送並確認 Azure 部署。

**Architecture:** 根目錄 README 作為產品入口，只保留線上展示、畫面、功能、架構、快速開始與驗證方式。前端 README 專注於 web 子專案，網站 metadata 與產品命名一致；部署沿用 push `main` 後觸發的既有 GitHub Actions。

**Tech Stack:** Markdown、Mermaid、.NET 10、React 19、TypeScript 6、Vite 8、GitHub Actions、Azure App Service、Azure Static Web Apps

## Global Constraints

- 對外名稱一律使用「線上購票系統」或 `ticketing`。
- 對外內容不得出現 `.github/naming-blocklist.txt` 所列的範圍字眼。
- 必須保留「票券無實際入場效力」。
- 機密只使用本機 user-secrets 或佔位值，不得寫入 repository、log 或回報。
- 不新增套件、不更動應用程式行為、不建立 sub-agent。

---

### Task 1: 完成公開文件與網站 metadata

**Files:**
- Modify: `README.md`
- Modify: `web/README.md`
- Modify: `web/index.html`
- Modify: `.gitignore`
- Modify: `AGENTS.md`
- Create: `docs/screenshots/01-home.png`
- Create: `docs/screenshots/02-event.png`
- Create: `docs/screenshots/03-seats.png`

**Interfaces:**
- Consumes: `AGENTS.md` 的命名、安全、架構與啟動規則；`.github/workflows/*.yml` 的實際驗證與部署流程。
- Produces: GitHub 可直接瀏覽的公開作品頁與一致的前端子專案說明。

- [x] **Step 1: 用現況掃描確認需要移除的公開內容**

Run:

```bash
rg -n "面試|Obsidian|開發進度|刻意的取捨|目前進度" README.md AGENTS.md web/README.md
```

Expected: 至少命中目前尚未整理完的 README 或 AGENTS 文字，證明後續掃描可偵測問題。

- [x] **Step 2: 重寫根目錄 README 的公開結構**

將 `README.md` 固定為下列章節順序：

```markdown
# 線上購票系統
產品簡介、誠信聲明、CI 與線上版

## 畫面
活動列表、活動詳情、選座三張圖

## 功能
使用者功能、管理功能、系統保證

## 架構
Web 與 Clean Architecture 四層的 Mermaid 圖、簡短技術棧

## 快速開始
工具需求、user-secrets、migration、seed、API 與 web 啟動

## 驗證
後端 build/test 與前端 lint/build
```

快速開始明確列出這些本機設定鍵：

```text
ConnectionStrings:Ticketing
Jwt:SigningKey
Seed:Admin:Email
Seed:Admin:Password
```

所有範例值使用 `YOUR_VALUE_HERE`；JWT 金鑰使用 `openssl rand -base64 48` 在本機產生。

- [x] **Step 3: 收斂相關文件與 metadata**

確認 `web/README.md` 只說明前端啟動、指令、目錄結構與兩個 `VITE_*` 設定；`web/index.html` 使用 `zh-Hant-TW`、產品 title 與誠信描述；`.gitignore` 忽略 `.omc/`、`.claude/`；`AGENTS.md` 只說完整設計位於 repository 外的私人筆記庫，不保留面試或實體 vault 路徑。

- [x] **Step 4: 驗證圖片與 Markdown 路徑**

Run:

```bash
test -s docs/screenshots/01-home.png
test -s docs/screenshots/02-event.png
test -s docs/screenshots/03-seats.png
rg -o 'docs/screenshots/[^)]+' README.md | while read -r image; do test -s "$image"; done
```

Expected: exit 0，三張 PNG 都存在且 README 引用可解析。

---

### Task 2: 執行完整驗證並提交

**Files:**
- Verify: `README.md`
- Verify: `web/README.md`
- Verify: `web/index.html`
- Verify: `.gitignore`
- Verify: `AGENTS.md`
- Verify: `docs/screenshots/*.png`
- Verify: `docs/superpowers/plans/2026-09-09-public-readme.md`

**Interfaces:**
- Consumes: Task 1 的完整文件變更。
- Produces: 一個通過本機守門、可安全推送的 git commit。

- [x] **Step 1: 執行命名、私密路徑與格式守門**

Run:

```bash
grep -v '^#' .github/naming-blocklist.txt | grep -v '^$' > /tmp/ticketing-readme-blocklist
if grep -rniEf /tmp/ticketing-readme-blocklist src web/src README.md .github \
     --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude=naming-blocklist.txt; then
  exit 1
fi
if rg -n "面試|Obsidian vault|簡易購票網站開發" README.md AGENTS.md web/README.md; then
  exit 1
fi
git diff --check
```

Expected: exit 0，沒有命名違規、公開的私人 vault 路徑或 whitespace error。

- [x] **Step 2: 驗證後端**

Run:

```bash
dotnet build --configuration Release
dotnet test tests/Ticketing.Domain.Tests --no-build --configuration Release
dotnet test tests/Ticketing.Application.Tests --no-build --configuration Release
```

Expected: build 0 warnings / 0 errors，Domain 與 Application 測試 0 failed。API 整合測試由 CI 的 x86-64 Docker runner 執行。

- [x] **Step 3: 驗證前端**

Run:

```bash
cd web
npm ci
npm run lint
npm run build
```

Expected: exit 0，Oxlint 無錯誤且 `dist/` 建置成功。

- [x] **Step 4: 檢查 staged diff 並提交**

Run:

```bash
git add .gitignore AGENTS.md README.md web/README.md web/index.html docs/screenshots docs/superpowers/plans/2026-09-09-public-readme.md
git diff --cached --check
git diff --cached --stat
git commit -m "文件：整理公開專案說明與展示畫面"
```

Expected: 只包含本計畫列出的文件、metadata、三張圖片與計畫檔，commit 成功。

---

### Task 3: 推送並確認部署

**Files:**
- Verify: `.github/workflows/ci.yml`
- Verify: `.github/workflows/deploy-api.yml`
- Verify: `.github/workflows/deploy-web.yml`

**Interfaces:**
- Consumes: Task 2 的已驗證 commit 與既有 GitHub Actions 設定。
- Produces: `origin/main` 上的提交、成功的 CI/部署紀錄與可存取的線上站點。

- [ ] **Step 1: 推送 main**

Run:

```bash
git push origin main
```

Expected: `main -> main`，且遠端包含本機 HEAD。

- [ ] **Step 2: 等候 GitHub Actions**

Run:

```bash
gh run list --commit "$(git rev-parse HEAD)" --limit 10
gh run watch "$(gh run list --commit "$(git rev-parse HEAD)" --workflow CI --json databaseId --jq '.[0].databaseId')" --exit-status
```

Expected: CI conclusion 為 success；隨後的 Deploy API 與 Deploy web workflow 為 success。若 deployment workflow 因 GitHub 的 `workflow_run` 關聯未被 commit filter 列出，改用 `gh run list --workflow 'Deploy API'` 與 `gh run list --workflow 'Deploy web'` 找到由該次 CI 觸發的最新 run。

- [ ] **Step 3: 驗證線上站點與 API**

Run:

```bash
curl -fsS -o /dev/null https://gentle-plant-0058db400.6.azurestaticapps.net/
curl -fsS https://gentle-plant-0058db400.6.azurestaticapps.net/ | rg -q '<title>線上購票系統</title>'
```

Expected: 首頁回應成功且部署後 HTML title 正確。API 健康檢查由 Deploy API workflow 使用 Azure 查得的實際 hostname 執行，不在文件或命令中猜測 secure unique hostname。

- [ ] **Step 4: 確認 repository 狀態**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main
```

Expected: 工作樹乾淨，HEAD 與 `origin/main` 相同。
