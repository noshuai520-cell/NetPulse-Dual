# ⚡ NetPulse Dual v2.0｜雙核網路脈搏診斷儀

> **Author:** `Le_morries`  
> 一款專為 Windows 設計的免安裝、輕量化 **IT 級網路拓撲偵測 × 雙獨立測速核心交叉診斷 × 24H 全自動哨兵蒐證儀表板**。

---

## 🔄 版本演進：從 v1.0 (NetWatchdog Pro) 到 v2.0 (NetPulse Dual)

本專案為前代單引擎監控工具（NetWatchdog Pro v1.0）的全面重構進化版，解決了單一測速來源容易受限於電信商白名單優化、以及無法區分內網與外網瓶頸的痛點：

* **雙獨立核心交叉驗證（Dual-Engine）**：從單一 Ookla 核心升級為 **Ookla Speedtest CLI（引擎 A）** 搭配 **開源 LibreSpeed CLI（引擎 B）**，精準比對物理線路極限與無白名單純 HTTP 真實速率，一秒抓出 ISP 流量整形（偷限速）。
* **IT 級內網路由拓撲自動掃描**：新增開機自動偵測「本機網卡協商速率 ➔ 預設閘道器（Router/ONT）第一跳延遲 ➔ 外網出口 IP 與 NAT/CGNAT 架構」，第一時間釐清卡頓源自自家設備還是電信商機房。
* **雙向 Bufferbloat 4 級專業診斷**：同時監控「下載滿載延遲」與「上傳滿載延遲」相對於閒置延遲的增幅（`0~15ms 良好`、`16~40ms 輕微`、`41~80ms 中度`、`>80ms 嚴重`）。
* **精準 DNS 異常判別**：修正傳統測速在 DNS 解析失敗時誤計耗時的盲點，解析失敗直接標記為 `999ms` 並納入異常蒐證。
* **全自適應響應式儀表板**：支援視窗任意縮放與最大化全螢幕，所有 GDI+ 圓弧儀表、即時波形圖與狀態進度條皆自動等比例鋪滿。

---

## ✨ 核心功能一覽

### 🧭 1. IT 級內網拓撲路徑偵測
啟動即自動掃描並於頂部狀態列呈現完整連線路徑：
`💻 本機 [網卡型號與協商速率 · 內網IP] ➔ 🏠 閘道器 [Router IP · 第一跳延遲] ➔ 🌐 外網出口 [Public IP] (NAT / PPPoE / CGNAT 架構)`

### ⚡ 2. 雙引擎接力深度診斷
* **【引擎 A】底層物理頻寬引擎 (Ookla Speedtest CLI)**：測試本地電信機房滿載下載/上傳極限、閒置延遲（Idle Ping）、雙向滿載延遲、抖動（Jitter）與掉包率。
* **【引擎 B】獨立開源與應用層探針 (LibreSpeed CLI + HTTPS/DNS)**：透過標準 HTTP/HTTPS 多執行緒獨立測速，並實測 DNS 解析反應、HTTPS 握手延遲與 4K 影音串流流暢度。

### 🛡️ 3. 24 小時全自動哨兵模式（零頻寬干擾）
* **日常超省流巡檢**：每 30 秒僅發送極輕量 TCP / HTTP HEAD 心跳探針，**完全不佔用日常頻寬**，追劇打遊戲零干擾。
* **異常自動抓包蒐證**：當偵測到延遲飆升、斷線或每 30 分鐘定期體檢時，自動觸發雙引擎全速實測並寫入 UTF-8 BOM CSV 報表（內建 240 秒防封鎖冷卻保護）。

---

## 🛠️ 如何編譯與使用（無需安裝 Visual Studio）

為了遵守第三方測速核心的授權規範，本開源倉庫不包含二進位 `.exe` 檔案，你可以透過內建的自動化腳本在 10 秒內完成單一獨立執行檔封裝：

1. 下載或 `git clone` 本專案至本機資料夾。
2. 準備兩個官方 Windows 64-bit 命令列核心並放入同一個資料夾內：
   * **Ookla Speedtest CLI**：前往 [Ookla 官方下載頁](https://speedtest.net) 下載 Windows 64-bit 壓縮檔，解壓取得 `speedtest.exe`。
   * **LibreSpeed CLI**：前往 [LibreSpeed CLI GitHub Releases](https://github.com) 下載 `windows_amd64.zip`，解壓取得 `librespeed-cli.exe`。
3. 雙擊執行 **`build.bat`**，腳本將自動呼叫 Windows 內建的 .NET C# 編譯器，將雙核心內嵌打包生成單一綠色執行檔 **`NetPulseDual_v2.0.exe`**。

---

## 🖥️ 系統需求

* **作業系統**：Windows 10 / Windows 11 (64-bit)
* **執行環境**：Windows 內建 .NET Framework 4.x（無需額外安裝任何執行庫）

---

## 📜 授權與聲明 (License & Disclaimer)

* 本專案主程式（`Program.cs`、`build.bat`）由 **Le_morries** 開發，採用 **MIT License** 開源釋出。
* 本工具僅作為個人與企業網路品質診斷用途；整合之外部測速核心（Ookla Speedtest CLI 與 LibreSpeed CLI [LGPL-3.0]）版權各自歸屬其原作者與公司，使用時請遵守各官方授權條款。
