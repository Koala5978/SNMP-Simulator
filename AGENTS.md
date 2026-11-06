# 開發協作規則

## 分支與合併

- `main` 為交付主分支；`develop` 為主要開發整合分支。
- 額外需求、功能、修正與文件工作一律從最新的 `develop` 建立分支，透過 PR 合併回 `develop`。
- 只有本儲存庫的 `develop` 能作為合併至 `main` 的 PR 來源；其他分支不可直接合併至 `main`，包括修正分支。
- 不直接推送至 `main`、不強制推送、不繞過必要檢查；交付時由 `develop` 提出 PR，驗證後合併至 `main`。
- 每個雲端任務已有隔離環境，使用現有 checkout；除非使用者要求，不另建 Git worktree。
- 保留既有未提交變更；只提交本次授權範圍的檔案，不因切換分支而清除或覆寫工作。

GitHub 上的強制執行需啟用 `main` ruleset／branch protection：要求 PR、要求 `main-source-policy` 通過、禁止強制推送與刪除，且不允許繞過 PR／必要檢查。來源檢查工作流程使用 `pull_request_target`，須先經由 `develop` PR 合併到 `main` 才能在目標分支生效。只有檔案存在不代表遠端保護已啟用。

## 需求與實作流程

- `docs/requirements.md` 是已確認的 v0.1.0 需求基準。需求確認不代表架構、程式、發版或效能已驗收。
- 先整理架構與任務供確認；明確授權實作後才修改程式。不把方案討論當成實作授權。
- 若變更影響功能、相容性、版本範圍或驗收條件，先提出問題、影響與建議，確認後再更新受影響文件與實作。
- 授權範圍內的一般實作細節可自行處理；修正程式符合需求，不以降低需求掩蓋錯誤。
- 報告實際執行的測試；未驗證、失敗與通過分別記錄，不以交叉建置成功代替 Windows 實機驗收。

## 授權與第三方相依

- 本專案原創程式碼與文件採 MIT，見 `LICENSE`；第三方元件保留各自授權，不重新授權為 MIT。
- 套件預設限用已核對的寬鬆授權：MIT、BSD-2-Clause、BSD-3-Clause、Apache-2.0、ISC、Zlib、BSL-1.0（SPDX 識別碼）。寬鬆不代表無義務，需保留適用的授權、著作權、NOTICE 與修改註記。
- 不引入 GPL、AGPL、LGPL、MPL 或其他含原始碼公開義務的元件。此為使用者選定的專案政策，不是對所有整合情況的法律判定。
- 自訂、未知、未列出或雙重授權先核對並提出建議，未確認前不加入。雙重授權只有在確實可選用、已記錄所選寬鬆條款且完整相依均符合政策時才可採用；不可忽略 AND 條件或原生子元件的獨立授權。
- 新增或升級時，核對確切版本的直接／遞移相依、runtime、原生元件、引用程式碼與圖片／字型。記錄名稱、版本、來源、授權、散布義務及審查狀態；NuGet metadata 或掃描結果只是線索，需對照該版本的正式授權。
- 不從授權不明的來源複製程式碼。AI 生成不代表已完成來源或授權審查；不要刪除第三方原有聲明。
- 發布前核對實際 self-contained EXE 內容與所需聲明，產生對應的第三方授權紀錄。單檔打包不免除散布義務；內建聲明或隨交付提供的方式依條款確認，不可省略必要文件。
- 開發／測試工具另行記錄其用途與授權，不因單純執行工具就認定它被散布進 EXE；若僅供開發的非寬鬆工具確有需要，也先提出例外，不自行加入。
- 目前只確認政策，未完成整包相依／runtime 授權審查，不宣稱既有發布成品已符合全部義務。

## 安全開發流程

- 安全檢查是每次開發／發布流程的一部分：SAST、直接與遞移相依漏洞、固定 runtime pack、機密外洩保護及協定邊界測試。發布流程須等待同一 tag 的 security workflow 通過。
- 新增／升級相依時同時審查漏洞與授權；NuGet PackageReference 為零不表示 runtime／原生元件沒有漏洞。工具／測試相依也需盤點。
- CodeQL 僅作 GitHub CI 分析服務，不散布進產品；使用需符合 GitHub CodeQL 條款與公開儲存庫／方案資格，不將其標示為 MIT 套件。若儲存庫改為私有，先確認服務授權。
- 不為了 CI 通過而停用檢查、降低門檻、忽略 feed 錯誤或任意豁免 CVE。NuGet High／Critical、CodeQL security-severity >=7 或 error 阻擋；PyPI 無統一嚴重度，所有有效 advisory 阻擋。
- 誤報／風險例外需提出版本、advisory、影響評估、理由、負責人與期限，確認後才設計最小例外；目前無自動豁免機制。既有問題也需處理，不把它們全部放進忽略清單。
- 修改 BER、JSON、GETBULK、socket 或取消／清理時，執行相應惡意輸入、界線與恢復測試。不得只靠 SAST 取代實際行為驗證。
- 不提交或輸出 token、密碼、實際敏感 community；樣本 public 不代表可把使用者環境設定寫入日誌。
- Actions 使用最小權限、正常 pull_request 事件及停用 checkout 憑證持留；禁止用 pull_request_target 執行 PR 程式。此規則不禁止只讀 event metadata 的既有 main-source-policy。
- 必要安全工作流程不可因路徑過濾或 continue-on-error 靜默跳過。套件／feed／權限錯誤記為掃描未完成，不記為安全通過。
- GitHub Secret scanning、Push protection、Dependabot alerts 與 required checks 需儲存庫設定；workflow 檔案存在不代表平台設定已啟用，見 SECURITY.md。

## 現有建置與驗證

使用 .NET 10 SDK，在儲存庫根目錄執行：

```sh
dotnet run --project tests/SnmpSimulator.Smoke -- samples/device.json
dotnet build src/SnmpSimulator.App -c Release
dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

程式與測試已納入實作分支；核心結果與外部限制見 docs/verification.md。WPF 只在 Windows 執行，本機 UDP 檢查不能代替跨主機內網或效能驗收。
