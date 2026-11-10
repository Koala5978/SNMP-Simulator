# SNMP Simulator

以 C#／.NET 10 與 WPF 開發的 Windows SNMP 模擬工具，用來測試監控系統、MIB Browser 與 SNMP 查詢程式。可透過內建範例或外部 JSON 模擬一台唯讀設備。

目前原始碼版本為 **v0.1.1**；正式可下載版本以 [GitHub Releases](https://github.com/Koala5978/SNMP-Simulator/releases) 為準，變更紀錄見 [CHANGELOG](CHANGELOG.md)。

## 系統需求與下載

- Windows 10 22H2 或 Windows 11，僅支援 **x86_64（x64／AMD64）**。
- 發布版為 self-contained 單一 EXE，**不需另外安裝 .NET runtime**。
- 到 [Releases](https://github.com/Koala5978/SNMP-Simulator/releases) 的 **Assets** 下載 `snmp-simulator.exe`，或下載包含範例、授權與來源資訊的 Windows x64 ZIP。
- `Source code.zip`／`Source code.tar.gz` 為原始碼，並非可直接執行的 Windows 程式。
- 可使用隨版本提供的 `SHA256SUMS.txt` 核對檔案；`SOURCE_COMMIT.txt` 記錄建置來源。

單獨攜帶 EXE 即可使用內建範例與查看授權聲明。執行時可能將必要原生元件解壓至暫存目錄，不需要安裝程序。

## 支援功能

| 項目 | 說明 |
| --- | --- |
| 模擬設備 | 同時一台設備、一組 IPv4／UDP 監聽端點 |
| SNMP | v1／v2c GET、GETNEXT；v2c 額外支援 GETBULK |
| 設備資料 | 七個內建範例 OID，或載入外部 JSON |
| 資料行為 | 固定值與模擬啟動後的 uptime |
| 操作 | 開始、停止、失敗後修正重試、停止後重新載入 |
| 狀態 | 設備名稱、OID 數量、監聽端點及累計接收／送出／丟棄數 |
| 唯讀保護 | SET 回傳拒絕錯誤，不改變資料；錯誤 community 不回應 |

### v0.1.1 修正重點

- 驗證或啟動失敗的錯誤訊息持續保留，不再被定期刷新覆蓋或還原成舊錯誤。
- 有效啟動後清除舊錯誤，修正設定後可在同一視窗重試。
- 背景工作完成清理前，不接受新的啟動請求。
- 視窗版號由 App 組件取得，與集中建置版本一致。

## 快速開始

1. 開啟 `snmp-simulator.exe`。
2. 選擇設備資料。若 EXE 同目錄有 `device.json`，程式會預選該檔；否則使用內建範例，也可按「選擇 JSON…」載入自訂資料。
3. 設定本機 IPv4 與 UDP port。預設 `127.0.0.1:161` 僅供本機查詢；若 port 已被占用，可改用 `1161`。
4. 按「開始模擬」，確認狀態顯示設備與監聽端點。
5. 由 MIB Browser 或其他 SNMP 工具查詢，範例 community 為 `public`。
6. 按「停止」結束模擬；修正資料或換檔後，可再次開始。

若要從另一台內網主機查詢，請改用模擬器主機的實際網卡 IPv4，並依網路管理政策允許相應的 Windows 防火牆 UDP port。程式不會自動修改防火牆。

常用範例：

| 名稱 | OID | 型別／預期回應 |
| --- | --- | --- |
| sysDescr | `1.3.6.1.2.1.1.1.0` | OctetString：`SNMP Simulator - Demo Switch` |
| sysName | `1.3.6.1.2.1.1.5.0` | OctetString：`demo-switch-01` |
| sysUpTime | `1.3.6.1.2.1.1.3.0` | TimeTicks：本次模擬啟動後持續增加 |

若已安裝 Net-SNMP，可使用以下命令；改用其他 IP／port 時請同步調整：

```sh
snmpget -v1 -c public -On 127.0.0.1:161 1.3.6.1.2.1.1.5.0
snmpget -v2c -c public -On 127.0.0.1:161 1.3.6.1.2.1.1.3.0
snmpwalk -v1 -c public -On 127.0.0.1:161 1.3.6.1.2.1
snmpbulkwalk -v2c -c public -On 127.0.0.1:161 1.3.6.1.2.1
```

畫面的送出數表示本機 socket 送出成功，丟棄數表示程式決定不回應；兩者不等同用戶端成功率或網路丟包率。

## 自訂設備 JSON

完整格式見 [samples/device.json](samples/device.json)。

- `schemaVersion` 目前為 `1`；v0.1.1 延用 v0.1.0 格式，不需轉換。
- `name` 為設備顯示名稱，`community` 為 v1／v2c 共用值。
- `objects` 每筆指定完整數字 `oid`、明確 `type` 及固定資料的 `value`。
- 支援 `OctetString`（UTF-8 文字）、`Integer32`、`ObjectIdentifier`、`Counter32`、`Gauge32`、`TimeTicks`、`Uptime`。
- `Uptime` 不需 value，以 SNMP TimeTicks（百分之一秒）回應，重啟模擬後重設，依 32 位元無號值溢位。
- OID 不可重複；純量使用完整實例 OID（通常以 `.0` 結尾），按數字段落排序，不要求檔案預先排序。

**每次開始才讀取檔案。** 執行中修改外部 JSON 不立即影響回應；停止再開始後才套用。修改磁碟上的範例檔不會改變 EXE 內建範例。

JSON、OID、型別或數值不合法時不啟動監聽；錯誤會保留供查看，修正後可重新開始。程式不解析 MIB 檔案；可在 JSON 逐項列出完整 table cell OID，但內建範例不代表完整 MIB 或介面 table。

## 目前限制

- 僅單設備、IPv4、唯讀 v1／v2c；不支援 v3、Trap／Inform、SET 寫入、多設備、IPv6 或真實設備 walk 匯入。
- 尚不支援任意二進位 OctetString、Counter64、腳本或一般動態計數器；uptime 除外。
- 設定檔最多 16 MiB、100,000 個 OID；每請求最多 256 個 varbind。
- GETBULK 最多處理 100 次 repetitions、256 個輸出 varbind；超限依既有保護規則截限或回覆 tooBig。
- 回應大小門檻為 1400 bytes，包含 tooBig 的最終編碼也會檢查；v1 錯誤回應仍過大時丟棄。
- 尚無吞吐量容量承諾；p99／500 ms 為後續效能目標，不是本版驗收門檻。

SNMP v1／v2c community 為明文機制，本工具以可控測試網路為使用情境。安全說明與私人回報方式見 [SECURITY.md](SECURITY.md)。

## 開發、測試與建置

從原始碼開發需要 **.NET 10 SDK**；Python 僅用於互通與安全檢查。所有命令在儲存庫根目錄執行。

```sh
# 核心、錯誤狀態回歸與實際 UDP 測試
dotnet run -c Release --project tests/SnmpSimulator.Smoke -- samples/device.json

# WPF 建置與 Windows x64 單檔發布
dotnet build src/SnmpSimulator.App -c Release
dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

WPF 僅能在 Windows 執行；核心／UDP 測試可在 Linux 執行，Windows 產物可交叉建置。原生 EXE 檔案版本檢查由 Windows CI 執行；交叉建置不代表實際 GUI 驗收通過。

獨立 SNMP 互通檢查：先執行上方 Release 測試建置，再於 Python 虛擬環境安裝測試相依並執行：

```sh
python -m pip install -r scripts/interop-requirements.txt
python scripts/interop.py
```

PySNMP／pyasn1 只供測試，不打包進產品。安全門檻與相依檢查：

```sh
python -m unittest discover -s tests/security -v
python scripts/security_scan.py sca
```

GitHub Actions 執行 Windows 建置／測試、CodeQL C# SAST、相依漏洞及協定邊界檢查。Release 附件流程等待同一 tag 的安全檢查通過，再建置與上傳 EXE／ZIP／授權／來源及雜湊，操作方式見 [發布說明](docs/releasing.md)。檢查結果以對應 commit／tag 的實際 run 為準。

## 文件索引

| 文件 | 內容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 版本變更與發布狀態 |
| [v0.1.1 Spec](docs/releases/v0.1.1-spec.md) | 本版範圍與驗收條件 |
| [v0.1.1 架構](docs/releases/v0.1.1-architecture.md) | 錯誤快照、生命週期、版本來源與安全交付 |
| [v0.1.1 任務](docs/releases/v0.1.1-task.md) | 實作及交付進度 |
| [v0.1.1 驗證紀錄](docs/releases/v0.1.1-verification.md) | 自動檢查、使用者驗收與待核實證據 |
| [v0.1.1 人工驗收](docs/releases/v0.1.1-acceptance.md) | 使用者已回報本輪項目測試通過，詳情依紀錄 |
| [後續規劃](docs/roadmap.md) | v0.2.0 候選事項，尚未實作 |
| [v0.2.0 Spec](docs/releases/v0.2.0-spec.md) | 已確認的 About／短 commit 需求，其餘功能規劃中，尚未實作 |
| [v0.1.0 需求](docs/requirements.md)／[架構](docs/architecture.md)／[驗證](docs/verification.md) | 初始版本歷史基準 |
| [AGENTS.md](AGENTS.md) | AI 協作、分支、授權與安全開發規則 |

## 授權

本專案原創程式碼與文件採 [MIT License](LICENSE)。第三方元件保留各自授權；採用寬鬆授權的相依政策與盤點狀態見 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，完整聲明保留於 `licenses/` 並可在程式「授權聲明」查看。
