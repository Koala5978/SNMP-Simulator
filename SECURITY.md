# Security policy

## Supported scope

目前 v0.1.0 為已發布版本，v0.1.1 規劃中。優先修正最新維護版本的安全問題；舊版是否回補依影響個別評估。

本工具接收不可信 UDP／BER 與設備 JSON。v1／v2c community 為明文，並非加密或強驗證協定；用途為可控測試網路。預設 loopback，對外監聽與防火牆由使用者管理，不主張可安全暴露於 Internet。

## Reporting a vulnerability

請優先使用本儲存庫 **Security → Advisories → Report a vulnerability** 私人回報。需由維護者啟用 Private vulnerability reporting；若尚無該按鈕，可先開不包含漏洞細節的 issue 請維護者提供私人回報管道。不要在公開 issue 貼 token、真實 community、利用封包或可識別的設備資料。

回報請包含受影響版本、平台、可重現步驟、預期／實際行為及影響，敏感細節透過私人管道提供。不承諾固定回應時間或獎金。

## Automated checks

- `.github/workflows/security.yml`：PR 至 develop／main、推送、每週一 02:23 UTC、手動及 reusable workflow。CodeQL 在 Windows 實際編譯 C#／WPF，分析 security-extended；另外執行 NuGet／runtime pack／Python advisory 與現有協定邊界測試。
- `scripts/security_scan.py`：標準函式庫實作報告門檻，沒有額外第三方 scanner 套件。Python 以 pip 的 wheel-only dry-run report 解析完整測試相依，再查每個版本的 PyPI JSON advisories；不安裝被測套件或執行 sdist。
- NuGet 包含直接／遞移套件，另建立不參與產品建置的 audit-only project 納入固定的 .NETCore 與 WindowsDesktop runtime packs。這不是逐個原生 DLL／OS 漏洞的完整分析，仍須追蹤 .NET 官方安全公告及主機更新。
- CodeQL High／Critical（security-severity >=7）及 error 阻擋；NuGet High／Critical 阻擋。低／中資訊保留於報告供追蹤；PyPI advisory 不統一提供嚴重度，因此所有有效 advisory 阻擋。feed 或掃描錯誤、缺報告也失敗。
- 發布附件 job 依賴 security reusable workflow，掃描來源與建置來源都使用確切 tag。既有 Release published 不會自動重播；已發布 v0.1.0 不改 tag。若舊 tag 不含安全腳本，新流程會失敗而非略過，歷史補傳需另行審查方案；本流程預期從包含安全流程的 v0.1.1 起使用。
- `.github/dependabot.yml`：NuGet／pip／Actions 每週檢查，PR 目標 develop，不自動合併。框架版本、固定 runtime 與 SDK 的更新不能只依賴 Dependabot，一併人工核對公告與授權。

Python advisory 來源：https://docs.pypi.org/api/json/#known-vulnerabilities
CodeQL 工具僅 CI 使用，依 GitHub 條款；公開儲存庫可用資格須持續確認，不能把 Action 本身的 MIT 授權當成整個 CodeQL CLI 的授權。

本流程涵蓋已知漏洞與靜態檢查，不宣稱掃描成功代表沒有漏洞。安全與第三方授權審查分別記錄。

## GitHub settings that require maintainer action

在 Settings → Security／Code security 的可用項目中啟用：

- Dependency graph、Dependabot alerts／security updates（依平台支援）；自動 PR 不代表自動接受升級。
- Secret scanning 與 Push protection（依公開性／方案可用性），以及 Private vulnerability reporting。
- Code scanning 的 Actions 上傳權限。使用進階 CodeQL workflow 時避免重複啟用同範圍 default setup；先確認可用方案。
- develop／main 的 branch protection／ruleset 要求安全 workflow 的 CSharp SAST、Dependency audit、Protocol boundary tests，確認首次 run 實際 check 名稱再選定。原 main-source-policy 繼續保留，限制 develop 才能 PR 回 main。

Agent 目前未取得設定這些項目的 GitHub API 存取；未宣稱它們已啟用。未合併到預設分支前，排程與部分平台功能也不一定啟動。

## Evidence

報告作為 Actions artifact 保存，對應 run／commit；維護者應確認 SARIF、NuGet、PyPI JSON 的真實結果與涵蓋範圍。不要上傳環境變數、credential 檔案或真實設備資料。CodeQL 零測試不是協定通過，既有 59 項 smoke 及互通測試也不是完整 fuzzing 或效能驗證。
