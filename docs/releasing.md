# Release 執行檔與附件

GitHub 的 Release、tag 與 Actions artifact 是不同物件。建立 tag／Release 不會自動編譯 EXE；Source code.zip／tar.gz 只包含來源檔案。Windows build and smoke 保存的是 Actions artifact，不會自動出現在 Release Assets。

## 自動發布附件

Publish Release assets workflow：

1. 由 Release published 事件啟動；也可 workflow_dispatch 指定已存在的版本補傳附件。
2. checkout 該確切 tag，確認來源已合併 main、GitHub Release 已存在、組件版本與 tag 相符。
3. 執行核心／UDP 與獨立互通測試，再建立 Windows x64 單檔與版本 ZIP。
4. 附上 EXE、完整套件 ZIP、授權檔與 licenses.zip、SOURCE_COMMIT.txt、SHA256SUMS.txt。

Workflow 使用 repository GITHUB_TOKEN 的 contents:write，不需要另存 PAT。若組織政策不允許，會明確失敗；不要略過失敗宣稱附件已上傳。既有同名附件不覆寫；重跑若名稱衝突，先核對既有附件與雜湊，確認後再決定刪除／替換，勿悄悄覆蓋已發布版本。

本 workflow 只負責建置與附件，不代表新增了人工功能或第三方授權審查結果；沿用版本本身的驗收紀錄。

## 已存在的 v0.1.0 補傳

1. 將本修正 PR 合併至 develop，再由 develop PR 合併至 main。
2. GitHub Actions 選 Publish Release assets → Run workflow，選 main，tag 輸入 v0.1.0。
3. 成功後至 Releases → v0.1.0 → Assets 下載 EXE 或版本 ZIP。

不需刪除 Release、移動 tag 或新增相同版本的 tag。由於 v0.1.0 已發布，之後合併 workflow 不會重播舊 published 事件，因此必須手動執行一次。

若希望立即人工補傳，也可下載該發布 tag 對應 commit 的成功 Actions artifact，核對 SOURCE_COMMIT.txt 與 EXE SHA-256，再於 Edit release 上傳附件。不要把先前 feature 分支的成品直接視為最終 tag 的建置。

目前環境無法透過 GitHub API 代為上傳附件或啟動 workflow（先前回覆 Forbidden）。workflow 推送只表示定義已保存，執行與附件上傳成功需另確認。
