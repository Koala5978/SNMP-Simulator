# SNMP Simulator

.NET 10 / WPF 的 Windows x64 單設備、唯讀 SNMP 模擬器。第一版聚焦功能驗證，尚未進行吞吐量最佳化或容量驗證。

## 文件與目前狀態

此實作分支包含 v0.1.0 候選程式、範例與測試，尚未正式發布。核心與本機 UDP 可在 Linux 測試，WPF 發行物只在 Windows 執行。Windows 實機及完整相依授權驗收仍待完成。

- [需求規格（已確認）](docs/requirements.md)
- [架構設計與架構圖（已確認）](docs/architecture.md)
- [任務規劃（草稿）](docs/task.md)
- [AI 協作與授權政策](AGENTS.md)

### 後續版本規劃

- [v0.1.1 修補版 Spec（設計基準）](docs/releases/v0.1.1-spec.md)
- [v0.1.1 增量架構與流程圖（已確認）](docs/releases/v0.1.1-architecture.md)
- [v0.1.1 開發與驗收任務（已確認）](docs/releases/v0.1.1-task.md)
- [v0.2.0 候選事項與後續方向（未開始）](docs/roadmap.md)

## 授權

本專案原創程式碼與文件採 [MIT License](LICENSE)。第三方元件保留各自授權；套件政策限用經審查的寬鬆授權，包含遞移相依。發布前仍須核對實際打包的 .NET runtime／原生元件及必要聲明，目前尚未完成整包授權審查。

- [Windows 人工驗收清單](docs/acceptance.md)
- [第三方元件與聲明](THIRD-PARTY-NOTICES.md)

## 執行

發布後只需攜帶 `snmp-simulator.exe`，不需另外安裝 .NET。內建七個範例 OID，直接按「開始模擬」即可使用。也可以選擇外部 JSON。預設監聽 `127.0.0.1:161`，community 為 `public`。

若要由其他電腦查詢，請將本機 IPv4 改成網卡實際位址，並允許 Windows 防火牆的對應 UDP port。若 161 已被其他服務使用，可改用 1161。停止再啟動會重新讀取設定檔並重設 uptime。

目前支援：

- SNMP v1 / v2c：GET、GETNEXT；v2c 額外支援 GETBULK。
- 不存在 OID、walk 結束、錯誤 community、唯讀 SET 拒絕及 tooBig 回應。
- 累計接收、成功送出、丟棄計數；每秒更新 UI，封包處理在背景執行。
- 固定值與啟動後 uptime。無 SET 寫入、Trap、v3、walk 匯入或多設備功能。

## 設備資料

格式見 [samples/device.json](samples/device.json)。`schemaVersion` 目前為 1，`name` 為顯示名稱，`community` 為 v1 / v2c 共用值；`objects` 定義數字 OID、明確型別及值。

型別：`OctetString`（UTF-8 文字）、`Integer32`、`ObjectIdentifier`、`Counter32`、`Gauge32`、`TimeTicks`、`Uptime`。除 `Uptime` 外都必須提供適合型別的 `value`；`Uptime` 無須 value，以百分之一秒回傳並按 32 位元溢位。

設定檔最大 16 MiB，載入前及讀取中檢查；載入與驗證在背景執行。

OID 必須使用標準數字格式，不可重複。純量請使用完整實例 OID（通常結尾 `.0`），table 則列出完整 cell OID。載入後依 OID 數字段落排序，而非字串排序。這不是 MIB 定義檔；第一版不解析 MIB。尚無任意二進位 OctetString 或 Counter64，未來 walk 匯入須擴充 schema。

原型保護限制：最多 100,000 個設定 OID、每個請求最多 256 個 varbind、GETBULK 最多處理 100 次重複／256 個輸出 varbind，一般回應門檻為 1400 bytes。超過輸出限制回傳 tooBig；超額 max-repetitions 會限制為 100。所有回應含 tooBig 均重新檢查最終大小；v1 錯誤回應仍過大時丟棄。此版本使用單一 UDP 收送迴圈，並非先前討論的高吞吐量最終引擎。

## 開發與建置

需要 .NET 10 SDK。使用現有 checkout，不需額外建立 worktree。

```sh
dotnet run --project tests/SnmpSimulator.Smoke -- samples/device.json
dotnet build src/SnmpSimulator.App -c Release
dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

WPF 僅能在 Windows 執行；核心與 smoke checks 可在 Linux 執行，Windows 版本可交叉建置。單檔 WPF 程式可能在執行時解壓原生相依元件到暫存目錄，不需要安裝程序。

Smoke checks 使用非零失敗結束碼，測試協定與實際 UDP 收送／重啟。Windows GUI、Windows 防火牆與發行 EXE 實際執行需在 Windows 驗證；這些檢查尚未於 Linux 環境執行。

若安裝了 Net-SNMP，可在啟動視窗模擬後驗證：

```sh
snmpget -v1 -c public -On 127.0.0.1:161 1.3.6.1.2.1.1.5.0
snmpget -v2c -c public -On 127.0.0.1:161 1.3.6.1.2.1.1.3.0
snmpwalk -v2c -c public -On 127.0.0.1:161 1.3.6.1.2.1
snmpbulkwalk -v2c -c public -On 127.0.0.1:161 1.3.6.1.2.1
```

預期設備名稱為 `demo-switch-01`，uptime 持續增加，walk 可取得七個範例 OID。這些外部工具命令是 Windows 驗收步驟，並非已執行的檢查。

獨立用戶端互通：先以 Release 執行 smoke，再於 Python 虛擬環境執行 `pip install -r scripts/interop-requirements.txt` 與 `python scripts/interop.py`。PySNMP／pyasn1 只供測試，不打包進 EXE。

## Release 附件

建立 tag／Release 不會自動提供執行檔；發布與補傳方式見 [Release 說明](docs/releasing.md)。`Publish Release assets` 工作流程從確切 tag 建置並附加 EXE、ZIP、授權與 SHA-256。

## 安全開發流程

SAST、相依漏洞／runtime pack 檢查、協定邊界測試及私人回報／GitHub 設定見 [SECURITY.md](SECURITY.md)。掃描 workflow 已定義不等於平台防護已啟用或第一次掃描已通過。
