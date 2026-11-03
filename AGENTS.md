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

## 現有建置與驗證

使用 .NET 10 SDK，在儲存庫根目錄執行：

```sh
dotnet run --project tests/SnmpSimulator.Smoke -- samples/device.json
dotnet build src/SnmpSimulator.App -c Release
dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

上述專案目前可能仍為未提交原型；使用前確認檔案存在。WPF 只在 Windows 執行，本機 UDP 檢查不能代替跨主機內網或效能驗收。
