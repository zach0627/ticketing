#!/usr/bin/env bash
#
# 從「現在這個地點」暫時開啟 Azure SQL 的防火牆，用完關掉。
#
# 為什麼需要這個腳本
# ------------------
# Azure SQL 的伺服器層防火牆是「允許哪些來源 IP」的白名單。開發機在家、在公司、
# 在客戶端的公網 IP 都不一樣，而且 ISP 的浮動 IP 會自己換。
#
# 直覺的做法是留一條長期規則、換地方就改它。**那是錯的**：
# 你離開之後，那個 IP 會被 ISP 配給另一個用戶，而規則還在——
# 等於資料庫防火牆為一個陌生人開著。這台伺服器是 Entra-only 驗證，
# 所以不至於被利用，但那是拿第二道防線去補第一道的洞。
#
# 所以這裡的預設是**沒有常駐例外**：要連的時候開，做完關。
#
# 用法
# ----
#   ./deploy/scripts/db-access.sh open     # 為目前的公網 IP 開一條規則
#   ./deploy/scripts/db-access.sh close    # 關掉（做完事一定要跑這個）
#   ./deploy/scripts/db-access.sh status   # 目前有哪些非 App Service 的規則
#
# 需要先 `az login`。資源名稱來自 azure.env 或環境變數（見下方，**不在版控裡**）。
#
# 規則生效需要時間
# ----------------
# Azure 的錯誤訊息自己寫著「It may take up to five minutes for this change to
# take effect」。實測通常幾秒內就好，但**連不上時先等一下再重試**，
# 不要急著懷疑 IP 抓錯——那會讓你在對的設定上一直找錯。
# close 之後同理，存取權會再多活一小段時間。
#
set -euo pipefail

# 資源名稱**不寫在版控裡**。
#
# 這個 repo 是公開的。前端網址與 API 網址本來就是公開的（訪客的瀏覽器就會看到），
# 但**資料庫端點不是**——沒有任何使用者會看到它。
# 把它寫死在腳本裡當預設值，等於免費送給任何人一個明確的攻擊目標。
#
# 這不會讓資料庫「被打開」（防火牆預設關著，而且伺服器是 Entra-only 驗證，
# 連 SQL 帳密登入這條路都不存在），但不公布端點是基本衛生。
#
# 值從兩個地方來，環境變數優先：
#   1. 環境變數 TICKETING_RG / TICKETING_SQL_SERVER
#   2. 同目錄的 azure.env（被 .gitignore 排除；範本見 azure.env.example）
_env_rg="${TICKETING_RG:-}"
_env_server="${TICKETING_SQL_SERVER:-}"
_env_rule="${TICKETING_DEV_RULE:-}"

_config="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/azure.env"
# shellcheck source=/dev/null
[ -f "$_config" ] && . "$_config"

RG="${_env_rg:-${TICKETING_RG:-}}"
SERVER="${_env_server:-${TICKETING_SQL_SERVER:-}}"
RULE="${_env_rule:-${TICKETING_DEV_RULE:-dev-roaming}}"

die() { printf '\033[31m%s\033[0m\n' "$*" >&2; exit 1; }
ok()  { printf '\033[32m%s\033[0m\n' "$*"; }
warn(){ printf '\033[33m%s\033[0m\n' "$*"; }

if [ -z "$RG" ] || [ -z "$SERVER" ]; then
  die "缺少資源名稱。請擇一：
  ① cp deploy/scripts/azure.env.example deploy/scripts/azure.env 並填入實際值（該檔已被 .gitignore 排除）
  ② 或設環境變數 TICKETING_RG 與 TICKETING_SQL_SERVER"
fi

# 從多個來源取公網 IP 並要求一致。
# 只問一個服務的話，它掛掉或回傳快取值時你不會知道——而寫錯 IP 的症狀
# （連不上）跟沒開規則一模一樣，會浪費很多時間。
detect_ip() {
  local ips=() ip
  for url in https://api.ipify.org https://ifconfig.me/ip https://icanhazip.com; do
    ip="$(curl -fsS -m 8 "$url" 2>/dev/null | tr -d '[:space:]' || true)"
    [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] && ips+=("$ip")
  done
  [ "${#ips[@]}" -ge 2 ] || die "取不到公網 IP（至少要兩個來源成功）。檢查網路。"
  for ip in "${ips[@]}"; do
    [ "$ip" = "${ips[0]}" ] || die "各來源看到的 IP 不一致：${ips[*]}。可能在代理或分流網路後面，請手動確認。"
  done
  printf '%s' "${ips[0]}"
}

# 只列「不是 App Service 出口」的規則——那 32 條是常駐的，不該被這個腳本碰到。
list_dev_rules() {
  az sql server firewall-rule list -g "$RG" -s "$SERVER" \
     --query "[?!starts_with(name,'appservice-')].{name:name,start:startIpAddress,end:endIpAddress}" -o json
}

cmd_status() {
  local current rules
  current="$(detect_ip)"
  echo "目前公網 IP：$current"
  echo
  rules="$(list_dev_rules)"
  if [ "$(echo "$rules" | python3 -c 'import json,sys;print(len(json.load(sys.stdin)))')" = "0" ]; then
    ok "沒有任何開發用規則——這是正常狀態。"
    return
  fi
  echo "開發用規則："
  echo "$rules" | python3 -c '
import json,sys
cur = sys.argv[1]
stale = []
for r in json.load(sys.stdin):
    same = r["start"] == cur == r["end"]
    print("  %-20s %s%s" % (r["name"], r["start"], "" if r["start"]==r["end"] else " - "+r["end"]),
          "  ← 目前這台" if same else "")
    if not same: stale.append(r["name"])
if stale:
    print()
    print("⚠️  這些規則指向你現在不在的 IP：%s" % ", ".join(stale))
    print("    那些位址現在可能已經屬於別人了。用 close 關掉。")
' "$current"
}

cmd_open() {
  local ip; ip="$(detect_ip)"
  echo "目前公網 IP：$ip"
  # create 與 update 分開：規則已存在時 create 會失敗，而我們要的是「就地換成新 IP」，
  # 不是累積一堆規則。
  if az sql server firewall-rule show -g "$RG" -s "$SERVER" -n "$RULE" -o none 2>/dev/null; then
    az sql server firewall-rule update -g "$RG" -s "$SERVER" -n "$RULE" \
       --start-ip-address "$ip" --end-ip-address "$ip" -o none
    ok "已更新規則 $RULE → $ip"
  else
    az sql server firewall-rule create -g "$RG" -s "$SERVER" -n "$RULE" \
       --start-ip-address "$ip" --end-ip-address "$ip" -o none
    ok "已建立規則 $RULE → $ip"
  fi
  echo "（規則生效最多需要 5 分鐘，通常幾秒。連不上先等一下再重試。）"
  warn "用完請執行：$0 close"
}

cmd_close() {
  if az sql server firewall-rule show -g "$RG" -s "$SERVER" -n "$RULE" -o none 2>/dev/null; then
    az sql server firewall-rule delete -g "$RG" -s "$SERVER" -n "$RULE" -o none
    ok "已刪除規則 $RULE"
  else
    ok "規則 $RULE 不存在，不需要動作。"
  fi
  local others
  others="$(list_dev_rules | python3 -c '
import json,sys
names=[r["name"] for r in json.load(sys.stdin)]
print(" ".join(names))')"
  [ -n "$others" ] && warn "還有其他開發用規則：$others（這個腳本只管 $RULE）"
  return 0
}

case "${1:-}" in
  open)   cmd_open   ;;
  close)  cmd_close  ;;
  status) cmd_status ;;
  *) die "用法：$0 {open|close|status}" ;;
esac
