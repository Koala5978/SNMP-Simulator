# v0.1.0 候選驗證紀錄

日期：2026-11-04。此為候選結果，不是正式 release 驗收完成聲明。

| 檢查 | 命令／證據 | 結果 |
| --- | --- | --- |
| 原型基準 | commit 05dd7a7；原 smoke runner | 20 項通過，exit 0 |
| 核心與真實 UDP | `dotnet run -c Release --project tests/SnmpSimulator.Smoke -- samples/device.json`，Linux x64 | 59 項通過，含 1,000 個畸形封包樣本，exit 0 |
| 獨立協定互通 | PySNMP 7.1.22／pyasn1 0.6.5；`python scripts/interop.py` | 24 項通過，exit 0；v1／v2c GET、walk、SET 拒絕、v2c bulk、錯誤 community timeout |
| Windows 發布 | `dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/final-candidate` | 交叉建置成功；runtime 固定 10.0.14，版本 0.1.0 |
| Windows CI | 使用者提供 run 41760480915、artifact 13289684606，見下節 | 使用者確認該 run 最終狀態為 Success，並提供產物／雜湊證據；有 Node.js 20 action runtime 棄用警告，非測試失敗。Agent 未獨立讀取 API 狀態 |
| Windows GUI／最低 OS／無 runtime | 使用者人工回報，見下節 | 使用者最終整體確認除效能外皆通過，人工功能驗收完成；實際 OS／build 明細未提供，見最終確認範圍 |
| 跨主機內網 | 使用者人工回報，見下節 | 使用者最終整體確認通過，含需求中的 v1／v2c 操作組合；截圖未逐項涵蓋，依人工確認記錄 |
| 授權 | THIRD-PARTY-NOTICES.md、licenses/ | 主授權與完整 upstream notices 已保留；原生元件逐項對照未完成 |
| 效能 | p99／500 ms | 未測，非本版完成門檻 |

尚未建立 tag／Release 或合併至 main。人工功能驗收已獲使用者整體確認；後續仍需處理交付與最終授權對照；新版 CI 的成功及警告消除已由使用者確認。

## 本地候選產物（歷史建置，非本次人工驗收的 CI 產物）

本地候選來源 commit：`0f858236eee7a2763b351dce4c7aac51e3a2bc16`。

EXE SHA-256：`73aa327583cbf1cdaaedfa278f62f3e3e18e3760d0c2cba4a817a21afdb3999a`。

候選 ZIP：`artifacts/snmp-simulator-v0.1.0-candidate-win-x64.zip`，含 EXE、選用 JSON、完整授權與來源／雜湊紀錄。單獨 EXE 也可使用內建範例及查看內建授權。

CI 產物另以該 run 的完整 SHA 為準，不與本地候選混用。本紀錄提交只修改文件，不代表本地 EXE 來自較新的文件 commit。

## 實際人工驗收使用的 CI 產物

| 欄位 | 使用者提供的內容 |
| --- | --- |
| Actions run | [41760480915](https://github.com/Koala5978/SNMP-Simulator/actions/runs/41760480915) |
| Artifact | [snmp-simulator-win-x64-1482a4d8ecc1afb9be2496529054eeba5d1f9c61](https://github.com/Koala5978/SNMP-Simulator/actions/runs/41760480915/artifacts/13289684606) |
| Artifact ID | `13289684606` |
| SOURCE_COMMIT.txt | `1482a4d8ecc1afb9be2496529054eeba5d1f9c61` |
| SHA256SUMS.txt 的 EXE SHA-256 | `AE4A54B3FFF3498600DCBF78FBD1548B5259C5E96F67302A351B4958378B0E4C` |
| 檔案 | `snmp-simulator.exe`（雜湊對象是 EXE，不是外層 ZIP） |
| 獨立計算工具 | MD5 & SHA Checksum Utility 2.1，依使用者截圖視窗標題 |
| 比對結果 | 使用者截圖顯示相同 SHA-256，並提示「SHA-256 Hash matched.」 |

Artifact 名稱與 SOURCE_COMMIT.txt 的 commit 一致；實際下載 EXE 的計算值與封裝內雜湊清單一致。此證據確認本次驗收產物的識別與完整性比對，不代表已獨立重建、驗證數位簽章或取得所有 CI job 的最終狀態。截圖未另存入儲存庫，也不記錄畫面上的使用者本機路徑。

## 使用者人工驗收回報（2026-11-04）

使用者回報單檔與內建範例、GUI／啟停／關閉／重載、無 .NET runtime 的乾淨環境、授權原文可取得及跨主機內網 GET／walk／bulkwalk 均通過。本節記錄使用者提供的結果，不將它們擴大成尚未提供的測試組合或 CI 結果。

### 外部工具（About 截圖確認）

- 名稱：iReasoning MIB Browser。
- 版本類別：Personal Edition。
- 版本：12.0，Build 4516。
- 來源證據：使用者於對話提供的 About 視窗截圖。截圖未另存入儲存庫。
- 此為使用者的外部驗收工具，未作為產品相依或打包進 EXE。

### 環境、重載與 CI 補充

- 使用者確認以全新安裝的 VM 執行，乾淨環境驗收依此記錄；實際 Windows 版本／build／x64 仍待補，未推論 Windows 10 與 11 均已測試。
- 使用者回報多次停止／啟用及換檔測試；可記錄啟停與換檔操作通過。執行中修改同一 JSON 不生效、停止再啟動才套用的情境尚待明確確認。
- 使用者確認 Actions run 41760480915 最終為 Success。警告針對 action 自身使用的 Node.js 20，不是 SNMP 程式或 Python 的 runtime；已核對官方 action.yml，改用 checkout v5、setup-dotnet v5、setup-python v6、upload-artifact v6（均為 node24）；更新後的成功及警告消除已有下節使用者截圖／回報證據；舊 run 的警告保留為歷史紀錄。
- 使用者後續補上 iReasoning MIB Browser 結果截圖：目標為私網 IPv4，操作欄為 Get；表格包含七個範例 OID，顯示設備描述、sysObjectID、uptime、聯絡人、名稱、位置及介面數，內容與型別符合範例。sysUpTime 顯示 46 seconds (4692)，對應 TimeTicks。
- 使用者確認多筆相同 sysDescr OID／值來自短時間內反覆發送 GET，目的是觀察是否遺漏封包；實際觀察請求皆立即收到回應，未發現遺漏。記為人工突發收送功能檢查通過，不是單次 walk 重複回覆的問題。未記錄請求總數、送出速率、測試時長或延遲分布，因此不據此推導量化吞吐量、丟包率或 p99。
- 截圖未顯示 Advanced 裡的 SNMP 版本，亦未顯示 walk／bulkwalk 操作設定或結束結果，因此用作 GET／範例資料的補充證據，不自動標記所有版本／操作組合完成。是否跨主機仍依使用者回報，不從私網目標位址單獨推斷。

### 先前待補項目與最終確認

- 實際 Windows 版本、build、x64；已知使用全新安裝 VM，確認是否於安裝其他 runtime／工具前執行單檔測試。
- v1 GET／walk 與 v2c GET／walk／bulkwalk 組合：已依使用者最終整體確認記錄通過，未另外取得逐項操作截圖。
- 執行中修改不生效、停止後重新開始才套用：已依使用者最終整體確認記錄通過。

授權視窗可讀取代表檢視功能通過，不等同第三方授權內容完整性已核對。

## 使用者最終功能驗收確認（2026-11-04）

使用者明確表示「除了 p99／500 ms 之外，全部都通過」。據此，v0.1.0 其餘人工功能驗收項目記為通過，包括前述詢問的 SNMP 版本／操作組合與重載行為。此結論來自使用者的整體驗收確認，不偽造未提供的 OS build、逐項截圖或 Agent 獨立測試結果。既有 VM、工具、CI 產物 commit／hash 與成功 run 證據均保留。

p99／500 ms 為本版本範圍外的效能目標，狀態為未量測，不記為失敗，也不阻擋 v0.1.0 功能驗收。原生元件授權對照、PR／合併與正式發布屬另行的工程交付工作，不能從功能驗收推導為已完成。

## Node.js 24 action 升級後驗證（2026-11-04）

- 使用者確認修改後的 Actions 沒有警告訊息。
- 提供的 windows-build.yml 執行截圖顯示 windows job 綠色成功，耗時 56 秒；這是 CI job 時間，不是 SNMP 延遲或效能數據。
- 截圖的 artifact 名稱為 `snmp-simulator-win-x64-fd6ce3017d30dade50d31ac82566b1f4f32609ec`，大小 57.8 MB；來源 SHA 與該次提交一致。
- 截圖的 Digest 屬 artifact 摘要，不取代內部 EXE 的 SHA256SUMS.txt。本次未提供新版 EXE hash，不沿用舊產物 hash。
- 本項記錄基於使用者截圖與無警告確認；未提供新 run URL，不補造 run ID。T09 action runtime 升級驗證通過。
