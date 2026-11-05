# v0.1.0 Windows 人工驗收

狀態：尚未執行。由使用者在對應環境完成並回傳結果；本文件不是通過紀錄。

## 產物與環境

使用 GitHub Actions「Windows build and smoke」對應實作分支 commit 的 artifact，或本地 `artifacts/final-candidate/snmp-simulator.exe`。記錄完整 commit、EXE SHA-256、Windows 版本／build、CPU 架構、是否已安裝 .NET。CI artifact 中的 SHA256SUMS.txt 及 SOURCE_COMMIT.txt 可供比對。

至少分別測 Windows 10 22H2 x64、Windows 11 x64；另有一個支援 OS 的乾淨 VM 不安裝 .NET，只複製 EXE，測試啟動與內建範例。不將未測組合推論為通過。可允許必要原生元件解壓至暫存路徑。

## GUI 與單檔（T11）

1. 只複製 EXE 到可寫入的測試資料夾，開啟並確認標題版本為 v0.1.0。
2. 使用內建資料，端點先採 127.0.0.1:1161，開始後應顯示設備名稱、七個 OID 與端點。按授權聲明，確認可讀本專案 MIT 與 runtime/WPF 原文。
3. 使用 SNMP 工具查詢下列命令。確認名稱正確、uptime 增加，walk 有七筆有效資料並結束。
4. 停止後查詢應逾時；重新開始可查詢，uptime 重設，累計統計重新計算。停止後仍保留最後統計可查看。
5. 選取外部 samples/device.json，修改 sysName 為其他文字，開始後確認回應。模擬中再改檔不應立即生效；停止後重新開始才更新。
6. 以重複 OID、錯誤型別或壞 JSON 測試：應顯示錯誤，且不可監聽。修正檔案後可正常開始。
7. 輸入無效 IP／port，或讓另一個程序占用同端點，應顯示錯誤，並可修正重試。
8. 測試載入中／執行中停止、視窗拖動及關閉；不應無回應或留下占用端點的程序。重新開啟仍可使用原端點。

## 外部工具與內網（T12）

Net-SNMP 指令供使用者既有工具參考；本機自動互通實際使用 BSD 授權 PySNMP，不會將 Net-SNMP 散布進 EXE。

```sh
snmpget -v1 -c public -On -t 2 -r 0 127.0.0.1:1161 1.3.6.1.2.1.1.5.0
snmpget -v2c -c public -On -t 2 -r 0 127.0.0.1:1161 1.3.6.1.2.1.1.3.0
snmpwalk -v1 -c public -On -t 2 -r 0 127.0.0.1:1161 1.3.6.1.2.1
snmpbulkwalk -v2c -c public -On -t 2 -r 0 127.0.0.1:1161 1.3.6.1.2.1
```

綁定 Windows 實際網卡 IPv4，依你管理的防火牆政策允許對應 UDP port，再從另一台內網主機以該 IP 替換 127.0.0.1 重跑。這部分必須跨主機，不能用 localhost 代替。錯誤 community 應逾時；SET 應被拒絕且不改值。確認 UI 收送數增加，但不要把本機送出數當成端到端送達數。

## 回報格式

| 項目 | 結果 |
| --- | --- |
| commit／EXE SHA-256 | 待填 |
| Windows 版本／build／x64 | 待填 |
| 是否無 .NET runtime 的乾淨環境 | 待填 |
| 單檔與內建範例 | 通過／失敗／未執行 |
| GUI、啟停、關閉與重載 | 通過／失敗／未執行 |
| 授權原文可取得 | 通過／失敗／未執行 |
| 外部工具／版本與命令 | 待填 |
| 跨主機內網 GET／walk／bulkwalk | 通過／失敗／未執行 |
| 錯誤內容或截圖 | 適用時提供，移除敏感資訊 |

此版不驗收吞吐量或 p99／500 ms。失敗時保留具體步驟、端點與錯誤，不先假定是程式或防火牆。
