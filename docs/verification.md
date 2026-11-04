# v0.1.0 候選驗證紀錄

日期：2026-11-04。此為候選結果，不是正式 release 驗收完成聲明。

| 檢查 | 命令／證據 | 結果 |
| --- | --- | --- |
| 原型基準 | commit 05dd7a7；原 smoke runner | 20 項通過，exit 0 |
| 核心與真實 UDP | `dotnet run -c Release --project tests/SnmpSimulator.Smoke -- samples/device.json`，Linux x64 | 59 項通過，含 1,000 個畸形封包樣本，exit 0 |
| 獨立協定互通 | PySNMP 7.1.22／pyasn1 0.6.5；`python scripts/interop.py` | 24 項通過，exit 0；v1／v2c GET、walk、SET 拒絕、v2c bulk、錯誤 community timeout |
| Windows 發布 | `dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/final-candidate` | 交叉建置成功；runtime 固定 10.0.14，版本 0.1.0 |
| Windows CI | 使用者提供 run 41760480915、artifact 13289684606，見下節 | 使用者確認該 run 最終狀態為 Success，並提供產物／雜湊證據；有 Node.js 20 action runtime 棄用警告，非測試失敗。Agent 未獨立讀取 API 狀態 |
| Windows GUI／最低 OS／無 runtime | 使用者人工回報，見下節 | 回報單檔、內建範例、GUI、啟停、關閉、重載及乾淨環境通過；實際 OS／build 待補，不能推論 10／11 均已驗證 |
| 跨主機內網 | 使用者人工回報，見下節 | 回報 GET／walk／bulkwalk 通過；各 SNMP 版本的操作組合待補 |
| 授權 | THIRD-PARTY-NOTICES.md、licenses/ | 主授權與完整 upstream notices 已保留；原生元件逐項對照未完成 |
| 效能 | p99／500 ms | 未測，非本版完成門檻 |

本次沒有建立 tag／Release，也未合併至 main。需要人工結果及 CI／最終授權核對後才能宣告版本驗收完成。

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
- 使用者確認 Actions run 41760480915 最終為 Success。警告針對 action 自身使用的 Node.js 20，不是 SNMP 程式或 Python 的 runtime；已核對官方 action.yml，改用 checkout v5、setup-dotnet v5、setup-python v6、upload-artifact v6（均為 node24）；更新後需以新的 run 結果確認警告消除，不改寫本次已成功結果。
- 使用者提到 SNMP 測試截圖，但該則訊息未收到附件；尚無額外圖片可判讀操作／版本組合。

### 尚待補充的追溯資料

- 實際 Windows 版本、build、x64；已知使用全新安裝 VM，確認是否於安裝其他 runtime／工具前執行單檔測試。
- 分別執行的 v1 GET／walk 與 v2c GET／walk／bulkwalk 組合。
- 重載是否包含執行中修改不生效、停止後重新開始才套用。

授權視窗可讀取代表檢視功能通過，不等同第三方授權內容完整性已核對。
