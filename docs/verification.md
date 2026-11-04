# v0.1.0 候選驗證紀錄

日期：2026-11-04。此為候選結果，不是正式 release 驗收完成聲明。

| 檢查 | 命令／證據 | 結果 |
| --- | --- | --- |
| 原型基準 | commit 05dd7a7；原 smoke runner | 20 項通過，exit 0 |
| 核心與真實 UDP | `dotnet run -c Release --project tests/SnmpSimulator.Smoke -- samples/device.json`，Linux x64 | 59 項通過，含 1,000 個畸形封包樣本，exit 0 |
| 獨立協定互通 | PySNMP 7.1.22／pyasn1 0.6.5；`python scripts/interop.py` | 24 項通過，exit 0；v1／v2c GET、walk、SET 拒絕、v2c bulk、錯誤 community timeout |
| Windows 發布 | `dotnet publish src/SnmpSimulator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/final-candidate` | 交叉建置成功；runtime 固定 10.0.14，版本 0.1.0 |
| Windows CI | `.github/workflows/windows-build.yml` | 已實作，未取得 run 結果；管理 API 回覆 Forbidden |
| Windows GUI／10 22H2／11／無 runtime | 見 acceptance.md | 未執行，需對應實機／VM |
| 跨主機內網 | 見 acceptance.md | 未執行 |
| 授權 | THIRD-PARTY-NOTICES.md、licenses/ | 主授權與完整 upstream notices 已保留；原生元件逐項對照未完成 |
| 效能 | p99／500 ms | 未測，非本版完成門檻 |

本次沒有建立 tag／Release，也未合併至 main。需要人工結果及 CI／最終授權核對後才能宣告版本驗收完成。

## 候選產物

本地候選來源 commit：`0f858236eee7a2763b351dce4c7aac51e3a2bc16`。

EXE SHA-256：`73aa327583cbf1cdaaedfa278f62f3e3e18e3760d0c2cba4a817a21afdb3999a`。

候選 ZIP：`artifacts/snmp-simulator-v0.1.0-candidate-win-x64.zip`，含 EXE、選用 JSON、完整授權與來源／雜湊紀錄。單獨 EXE 也可使用內建範例及查看內建授權。

CI 產物另以該 run 的完整 SHA 為準，不與本地候選混用。本紀錄提交只修改文件，不代表本地 EXE 來自較新的文件 commit。
