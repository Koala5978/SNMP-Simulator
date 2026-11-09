# Changelog

## Unreleased — v0.1.1

- 統一保存輸入、設定檔與 UDP 失敗原因，修正狀態刷新清除最新錯誤或還原舊錯誤的問題。
- 有效開始清除錯誤，失敗後可修正重試；背景清理完成前不接受新開始。
- 視窗版號改由 App 組件產生，版本集中為 0.1.1。
- 新增狀態／重試回歸及 Windows 發布 EXE 版本一致性檢查，延續安全掃描與 Release gate。

尚待本候選的 GitHub CI、Windows 人工補驗與最終授權／交付核對，沒有 v0.1.1 tag／Release。

## v0.1.0

已由使用者確認發布；原功能內容如下。歷史驗收詳見 docs/verification.md。

- .NET 10 / WPF Windows x64 單設備唯讀 SNMP 模擬器，內建七個 OID 與可重載 JSON。
- 支援 v1／v2c GET、GETNEXT 與 v2c GETBULK；包含版本錯誤回應、SET 拒絕及有界回應。
- 背景載入、不可變資料快照、數字 OID 索引、uptime、啟停清理及累計統計。
- self-contained 單檔發布、內建授權聲明；獨立 PySNMP 互通與 Windows CI workflow。

功能與 CI 已有使用者驗收回報；原生元件授權對照是否完整另行記錄，不從已發布狀態推導為已完成。
