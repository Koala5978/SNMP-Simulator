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

## 現有建置與驗證

使用 .NET 10 SDK，在儲存庫根目錄執行：

```sh
dotnet run --project tests/SnmpSimulator.Smoke -- samples/device.json
dotnet build src/SnmpSimulator.App -c Release
dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

程式與測試已納入實作分支；核心結果與外部限制見 docs/verification.md。WPF 只在 Windows 執行，本機 UDP 檢查不能代替跨主機內網或效能驗收。
