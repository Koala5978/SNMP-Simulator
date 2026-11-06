# Security workflow 初次建立紀錄

日期：2026-11-06。這是開發流程驗證，不是安全認證。

| 項目 | 本次結果 |
| --- | --- |
| 報告門檻單元測試 | 9 項通過；包含 High、低嚴重度、feed 失敗、缺失 SARIF、SAST 失敗及未知 rule |
| NuGet 與 runtime | 3 個既有專案及 runtime audit-only project 查詢完成；核對清單確實包含兩個 win-x64 runtime pack 10.0.14；未報告 High／Critical 阻擋項目 |
| Python | pip wheel-only dry-run 完整解析 PySNMP 7.1.22、pyasn1 0.6.5；逐版本查 PyPI advisories，當次未報告有效 advisory |
| Workflow | YAML 結構與同 tag 的 Release needs:security 驗證通過 |
| CodeQL | 尚未在 GitHub Windows runner 執行，未宣稱 SAST 通過 |
| 平台設定 | Secret scanning、Push protection、Private vulnerability reporting、required checks 未由 Agent 啟用；由維護者確認 |
| 授權 | 最終掃描腳本僅用 Python 標準函式庫、既有 pip／dotnet，無新增產品或掃描套件；CodeQL 為 CI 服務，依平台條款 |

重現：

```sh
python -m unittest discover -s tests/security -v
python scripts/security_scan.py sca
```

本地 SDK 位於 /workspace/.dotnet 時可使用 DOTNET_EXE 指定 dotnet 執行檔。原始報告位於 artifacts/security/sca-verified/（未納入 Git），CI 另保存對應 run 的 artifacts。資料庫會更新，之後結果可能不同，不能將本次 clean 結果永久快取作為通過條件。

CodeQL 必須取得真實 SARIF，所有高嚴重度與掃描失敗都阻擋；尚未設任何漏洞例外。低／中結果可供追蹤，不應隱藏。完整原生 DLL 來源對照、第三方授權審查與安全掃描是不同事項。
