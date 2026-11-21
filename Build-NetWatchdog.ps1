$g1 = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace SpeedWatchdogGUI {
public class MainForm : Form {
ComboBox cmbPlan, cmbTol;
Button btnStart, btnStop, btnOpenCsv;
Label lblDownVal, lblUpVal, lblPingVal, lblStatusBanner;
DataGridView dgvLogs;
CancellationTokenSource loopCts;
double baseDown = 100, baseUp = 100, tolPercent = 5.0;
string baseDir = "", cliExe = "", csvPath = "";
bool wasAbnormal = false;
public MainForm() {
baseDir = AppDomain.CurrentDomain.BaseDirectory;
cliExe = Path.Combine(Path.GetTempPath(), "ookla_st_embedded.exe");
InitUI();
}
protected override void OnFormClosing(FormClosingEventArgs e) {
if (loopCts != null) {
try { loopCts.Cancel(); } catch { }
}
base.OnFormClosing(e);
}
'@
$g2 = @'
void InitUI() {
this.Text = "網速哨兵 Pro (NetWatchdog)  |  作者：Le_morries";
this.Size = new Size(940, 620);
this.StartPosition = FormStartPosition.CenterScreen;
this.BackColor = Color.FromArgb(24, 26, 32);
this.ForeColor = Color.White;
this.Font = new Font("Microsoft JhengHei UI", 10F);
this.FormBorderStyle = FormBorderStyle.FixedSingle;
this.MaximizeBox = false;
Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(32, 36, 46) };
Label lblTitle = new Label { Text = "  ⚡ 網速哨兵 Pro (NetWatchdog Pro)", Font = new Font("Microsoft JhengHei UI", 14F, FontStyle.Bold), ForeColor = Color.FromArgb(90, 200, 250), AutoSize = false, Dock = DockStyle.Left, Width = 650, TextAlign = ContentAlignment.MiddleLeft };
Label lblAuthor = new Label { Text = "設計開發：Le_morries  ", Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(160, 170, 190), AutoSize = false, Dock = DockStyle.Right, Width = 240, TextAlign = ContentAlignment.MiddleRight };
pnlTop.Controls.Add(lblTitle); pnlTop.Controls.Add(lblAuthor);
this.Controls.Add(pnlTop);
Panel pnlCtrl = new Panel { Location = new Point(20, 70), Size = new Size(885, 85), BackColor = Color.FromArgb(34, 38, 48) };
Label l1 = new Label { Text = "申請頻寬方案：", Location = new Point(15, 14), AutoSize = true, ForeColor = Color.LightGray };
cmbPlan = new ComboBox { Location = new Point(15, 40), Width = 245, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 50, 62), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
cmbPlan.Items.AddRange(new object[] { "100M / 40M (一般家用光纖)", "100M / 100M (對稱型光纖)", "300M / 300M (高速對稱光纖)", "500M / 250M (非對稱高速)", "500M / 500M (對稱高速光纖)", "1G / 600M (1000M / 600M)", "1G / 1G (1000M / 1000M)", "2G / 1G (2000M / 1000M)" });
cmbPlan.SelectedIndex = 4;
Label l2 = new Label { Text = "容許誤差範圍：", Location = new Point(280, 14), AutoSize = true, ForeColor = Color.LightGray };
cmbTol = new ComboBox { Location = new Point(280, 40), Width = 290, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 50, 62), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
cmbTol.Items.AddRange(new object[] { "3%  (極度嚴格 - 僅限純企業專線)", "5%  (嚴格建議 - 無干擾純光纖直連)", "7%  (標準嚴格 - 一般光纖微量損耗)", "8%  (PPPoE建議 - 扣除撥號標頭損耗)", "9%  (穩健標準 - 家用與辦公室環境)", "10% (寬鬆標準 - 1G以上或多人共用)", "15% (高容許度 - 忽略尖峰正常波動)", "20% (抓大異常 - 僅抓斷流嚴重降速)" });
cmbTol.SelectedIndex = 1;
btnStart = new Button { Text = "▶ 啟動監控", Location = new Point(590, 34), Size = new Size(110, 36), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
btnStart.FlatAppearance.BorderSize = 0; btnStart.Click += BtnStart_Click;
btnStop = new Button { Text = "■ 停止", Location = new Point(710, 34), Size = new Size(78, 36), BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold), Enabled = false, Cursor = Cursors.Hand };
btnStop.FlatAppearance.BorderSize = 0; btnStop.Click += BtnStop_Click;
btnOpenCsv = new Button { Text = "📄 報表", Location = new Point(798, 34), Size = new Size(72, 36), BackColor = Color.FromArgb(70, 80, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
btnOpenCsv.FlatAppearance.BorderSize = 0;
btnOpenCsv.Click += (s, e) => {
if (!File.Exists(csvPath)) { MessageBox.Show("尚未產生報表，請先啟動監控！", "提示"); return; }
try { Process.Start(new ProcessStartInfo(csvPath) { UseShellExecute = true }); }
catch { Process.Start("notepad.exe", csvPath); }
};
pnlCtrl.Controls.AddRange(new Control[] { l1, cmbPlan, l2, cmbTol, btnStart, btnStop, btnOpenCsv });
this.Controls.Add(pnlCtrl);
lblDownVal = CreateCard("Speedtest 實測下載 (Download)", "-- Mbps", Color.FromArgb(80, 220, 120), 20, 170);
lblUpVal   = CreateCard("Speedtest 實測上傳 (Upload)", "-- Mbps", Color.FromArgb(90, 200, 250), 320, 170);
lblPingVal = CreateCard("台灣機房節點延遲 (Ping)", "-- ms", Color.FromArgb(255, 200, 80), 620, 170);
lblStatusBanner = new Label { Text = "  目前狀態：待機中（已內嵌官方測速核心，免下載、免建資料夾隨處可用）", Location = new Point(20, 285), Size = new Size(885, 34), BackColor = Color.FromArgb(40, 44, 56), ForeColor = Color.FromArgb(200, 210, 225), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold) };
this.Controls.Add(lblStatusBanner);
dgvLogs = new DataGridView { Location = new Point(20, 330), Size = new Size(885, 235), BackgroundColor = Color.FromArgb(30, 34, 42), BorderStyle = BorderStyle.None, AllowUserToAddRows = false, ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(50, 55, 68) };
dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 48, 60); dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Bold); dgvLogs.ColumnHeadersHeight = 32;
dgvLogs.DefaultCellStyle.BackColor = Color.FromArgb(30, 34, 42); dgvLogs.DefaultCellStyle.ForeColor = Color.White; dgvLogs.RowTemplate.Height = 28;
dgvLogs.Columns.Add("Time", "異動發生時間"); dgvLogs.Columns.Add("Status", "狀態變化");
dgvLogs.Columns.Add("Down", "下載 (Mbps)"); dgvLogs.Columns.Add("Up", "上傳 (Mbps)");
dgvLogs.Columns.Add("Ping", "延遲 (ms)"); dgvLogs.Columns.Add("Reason", "異常判定與測速機房");
dgvLogs.Columns.Cast<DataGridViewColumn>().ElementAt(0).FillWeight = 130;
dgvLogs.Columns.Cast<DataGridViewColumn>().ElementAt(1).FillWeight = 120;
dgvLogs.Columns.Cast<DataGridViewColumn>().ElementAt(5).FillWeight = 210;
this.Controls.Add(dgvLogs);
}
Label CreateCard(string title, string initVal, Color valColor, int x, int y) {
Panel card = new Panel { Location = new Point(x, y), Size = new Size(285, 100), BackColor = Color.FromArgb(34, 38, 48) };
Label lblT = new Label { Text = title, Location = new Point(15, 12), AutoSize = true, ForeColor = Color.FromArgb(160, 170, 190), Font = new Font("Microsoft JhengHei UI", 9.5F) };
Label lblV = new Label { Text = initVal, Location = new Point(15, 40), Size = new Size(255, 45), ForeColor = valColor, Font = new Font("Segoe UI", 22F, FontStyle.Bold) };
card.Controls.Add(lblT); card.Controls.Add(lblV); this.Controls.Add(card);
return lblV;
}
'@
$g3 = @'
void BtnStart_Click(object sender, EventArgs e) {
double[] downs = { 100, 100, 300, 500, 500, 1000, 1000, 2000 };
double[] ups   = { 40,  100, 300, 250, 500, 600,  1000, 1000 };
double[] tols  = { 3, 5, 7, 8, 9, 10, 15, 20 };
baseDown = downs.ElementAt(cmbPlan.SelectedIndex);
baseUp = ups.ElementAt(cmbPlan.SelectedIndex);
tolPercent = tols.ElementAt(cmbTol.SelectedIndex);
btnStart.Enabled = false; cmbPlan.Enabled = false; cmbTol.Enabled = false; btnStop.Enabled = true;
wasAbnormal = false;
csvPath = Path.Combine(baseDir, string.Format("網速哨兵紀錄_{0}M_誤差{1}%_{2}.csv", baseDown, tolPercent, DateTime.Now.ToString("yyyyMMdd_HHmm")));
File.WriteAllText(csvPath, "異動發生時間,狀態變化,實測下載(Mbps),實測上傳(Mbps),基準規格,容許誤差,機房延遲(ms),異常原因與機房,Speedtest官方認證網址\r\n", new UTF8Encoding(true));
loopCts = new CancellationTokenSource();
Task.Run(() => MonitorLoopAsync(loopCts.Token));
}
void BtnStop_Click(object sender, EventArgs e) {
if (loopCts != null) loopCts.Cancel();
btnStart.Enabled = true; cmbPlan.Enabled = true; cmbTol.Enabled = true; btnStop.Enabled = false;
UpdateBanner("  目前狀態：已手動停止監控", Color.FromArgb(40, 44, 56), Color.LightGray);
}
void ExtractEmbeddedEngine() {
try {
if (File.Exists(cliExe) && new FileInfo(cliExe).Length > 1000000) return;
var asm = Assembly.GetExecutingAssembly();
using (Stream s = asm.GetManifestResourceStream("speedtest.exe")) {
if (s == null) throw new Exception("找不到內嵌的測速核心資源");
string tmpFile = cliExe + "." + Guid.NewGuid().ToString("N") + ".tmp";
using (FileStream fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
s.CopyTo(fs);
}
if (!File.Exists(cliExe)) File.Move(tmpFile, cliExe);
else File.Delete(tmpFile);
}
} catch (IOException) {
if (!File.Exists(cliExe)) throw;
}
}
async Task MonitorLoopAsync(CancellationToken token) {
try { ExtractEmbeddedEngine(); }
catch (Exception ex) {
UpdateBanner("  ❌ 核心初始化失敗：" + ex.Message, Color.FromArgb(120, 30, 30), Color.White);
return;
}
double passRatio = (100.0 - tolPercent) / 100.0;
double minOkDown = Math.Round(baseDown * passRatio, 1);
double minOkUp = Math.Round(baseUp * passRatio, 1);
int cycle = 0; bool firstRun = true;
while (!token.IsCancellationRequested) {
try {
string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
long quickPing = await GetTcpPingAsync("168.95.1.1", 53);
bool needFull = firstRun || (cycle % 30 == 0) || (quickPing > 50) || wasAbnormal;
if (needFull) {
UpdateBanner("  ⚡ 正在連線台灣本地 Speedtest 機房，準備即時測速...", Color.FromArgb(45, 55, 80), Color.FromArgb(90, 200, 250));
var res = await RunLiveSpeedtestAsync(token);
if (res == null) throw new Exception("Speedtest 引擎回傳異常或連線逾時");
double realDown = res.Item1; double realUp = res.Item2; long pingMs = res.Item3;
string srvName = res.Item4; string resultUrl = res.Item5;
bool downBad = realDown < minOkDown; bool upBad = realUp < minOkUp; bool isAbn = downBad || upBad;
string reason = string.Format("頻寬正常 [{0}]", srvName);
if (downBad && upBad) reason = string.Format("雙向異常掉速 [{0}]", srvName);
else if (downBad) reason = string.Format("下載異常掉速 (僅達 {0:0}%) [{1}]", (realDown / baseDown) * 100, srvName);
else if (upBad) reason = string.Format("上傳異常掉速 (僅達 {0:0}%) [{1}]", (realUp / baseUp) * 100, srvName);
UpdateMetrics(realDown, realUp, pingMs, downBad, upBad);
if (firstRun) { AddRecord(nowStr, isAbn ? "啟動檢測(目前異常)" : "啟動檢測(目前正常)", realDown, realUp, pingMs, reason, resultUrl, isAbn); wasAbnormal = isAbn; firstRun = false; }
else if (isAbn && !wasAbnormal) { AddRecord(nowStr, "【發生掉速異動】", realDown, realUp, pingMs, reason, resultUrl, true); wasAbnormal = true; }
else if (!isAbn && wasAbnormal) { AddRecord(nowStr, "【網速恢復正常】", realDown, realUp, pingMs, string.Format("已恢復正常 [{0}]", srvName), resultUrl, false); wasAbnormal = false; }
if (wasAbnormal) UpdateBanner("  ⚠️ 警告：目前處於掉速異常狀態！持續使用 Speedtest 追蹤恢復中...", Color.FromArgb(110, 35, 45), Color.White);
else UpdateBanner(string.Format("  ✅ 狀態正常 ({0}/{1} Mbps | 節點:{2}) | 輕量哨兵背景巡檢中...", realDown, realUp, srvName), Color.FromArgb(30, 75, 55), Color.FromArgb(150, 250, 180));
} else {
UpdatePingOnly(quickPing);
UpdateBanner(string.Format("  ✅ 哨兵巡檢中 ({0}) | 國內骨幹延遲 {1}ms | 狀態穩定無異動", DateTime.Now.ToString("HH:mm:ss"), quickPing), Color.FromArgb(30, 75, 55), Color.FromArgb(150, 250, 180));
}
} catch (OperationCanceledException) { break; }
catch (Exception ex) {
string errTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
UpdateMetrics(0, 0, 999, true, true);
AddRecord(errTime, "【斷線/無法連線】", 0, 0, 999, "連線異常：" + ex.Message, "", true);
UpdateBanner("  ❌ 網路中斷或 Speedtest 連線逾時！正在持續嘗試重連...", Color.FromArgb(120, 30, 30), Color.White);
wasAbnormal = true;
}
cycle++;
try { await Task.Delay(30000, token); } catch { break; }
}
}
'@
$g4 = @'
Task<Tuple<double, double, long, string, string>> RunLiveSpeedtestAsync(CancellationToken token) {
return Task.Run(() => {
var psi = new ProcessStartInfo {
FileName = cliExe,
Arguments = "--accept-license --accept-gdpr -f jsonl",
RedirectStandardOutput = true,
RedirectStandardError = true,
UseShellExecute = false,
CreateNoWindow = true,
StandardOutputEncoding = Encoding.UTF8
};
double finalDown = 0, finalUp = 0; long finalPing = 0;
string srvName = "台灣本地節點", resUrl = "";
var jss = new JavaScriptSerializer();
using (var p = Process.Start(psi)) {
using (token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } })) {
string line;
while ((line = p.StandardOutput.ReadLine()) != null) {
if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{")) continue;
try {
var obj = jss.Deserialize<Dictionary<string, object>>(line);
if (obj == null || !obj.ContainsKey("type")) continue;
string type = Convert.ToString(obj["type"]);
if (type == "testStart" && obj.ContainsKey("server")) {
var srv = obj["server"] as Dictionary<string, object>;
if (srv != null && srv.ContainsKey("name")) srvName = Convert.ToString(srv["name"]);
UpdateBanner(string.Format("  ⚡ 已鎖定測速機房：[{0}]，正在測試節點延遲...", srvName), Color.FromArgb(45, 55, 80), Color.FromArgb(90, 200, 250));
} else if (type == "ping" && obj.ContainsKey("ping")) {
var pingObj = obj["ping"] as Dictionary<string, object>;
if (pingObj != null && pingObj.ContainsKey("latency")) {
double lat = Convert.ToDouble(pingObj["latency"]);
if (lat > 0) { finalPing = (long)Math.Round(lat); UpdatePingOnly(finalPing); }
}
} else if (type == "download" && obj.ContainsKey("download")) {
var dlObj = obj["download"] as Dictionary<string, object>;
if (dlObj != null) {
double bw = dlObj.ContainsKey("bandwidth") ? Convert.ToDouble(dlObj["bandwidth"]) : 0;
double prog = dlObj.ContainsKey("progress") ? Convert.ToDouble(dlObj["progress"]) : 0;
UpdateLiveSpeed(true, Math.Round((bw * 8.0) / 1000000.0, 2), (int)(prog * 100), srvName);
}
} else if (type == "upload" && obj.ContainsKey("upload")) {
var ulObj = obj["upload"] as Dictionary<string, object>;
if (ulObj != null) {
double bw = ulObj.ContainsKey("bandwidth") ? Convert.ToDouble(ulObj["bandwidth"]) : 0;
double prog = ulObj.ContainsKey("progress") ? Convert.ToDouble(ulObj["progress"]) : 0;
UpdateLiveSpeed(false, Math.Round((bw * 8.0) / 1000000.0, 2), (int)(prog * 100), srvName);
}
} else if (type == "result") {
var dlObj = obj.ContainsKey("download") ? obj["download"] as Dictionary<string, object> : null;
var ulObj = obj.ContainsKey("upload") ? obj["upload"] as Dictionary<string, object> : null;
var pingObj = obj.ContainsKey("ping") ? obj["ping"] as Dictionary<string, object> : null;
var resObj = obj.ContainsKey("result") ? obj["result"] as Dictionary<string, object> : null;
if (dlObj != null && dlObj.ContainsKey("bandwidth")) finalDown = Math.Round((Convert.ToDouble(dlObj["bandwidth"]) * 8.0) / 1000000.0, 2);
if (ulObj != null && ulObj.ContainsKey("bandwidth")) finalUp = Math.Round((Convert.ToDouble(ulObj["bandwidth"]) * 8.0) / 1000000.0, 2);
if (pingObj != null && pingObj.ContainsKey("latency")) finalPing = (long)Math.Round(Convert.ToDouble(pingObj["latency"]));
if (resObj != null && resObj.ContainsKey("url")) resUrl = Convert.ToString(resObj["url"]);
}
} catch { }
}
p.WaitForExit(5000);
}
}
if (finalDown <= 0 && finalUp <= 0) return null;
return Tuple.Create(finalDown, finalUp, finalPing, srvName, resUrl);
}, token);
}
'@
$g5 = @'
void UpdateLiveSpeed(bool isDown, double mbps, int pct, string srv) {
if (this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
try {
this.Invoke((Action)(() => {
if (isDown) {
lblDownVal.Text = mbps.ToString("0.00") + " Mbps";
lblDownVal.ForeColor = Color.FromArgb(80, 220, 120);
lblStatusBanner.Text = string.Format("  ⚡ 正在全速測試【下載速率】... 進度 {0}% | 即時速率：{1:0.00} Mbps [{2}]", pct, mbps, srv);
} else {
lblUpVal.Text = mbps.ToString("0.00") + " Mbps";
lblUpVal.ForeColor = Color.FromArgb(90, 200, 250);
lblStatusBanner.Text = string.Format("  ⚡ 正在全速測試【上傳速率】... 進度 {0}% | 即時速率：{1:0.00} Mbps [{2}]", pct, mbps, srv);
}
}));
} catch { }
}
void UpdateMetrics(double d, double u, long p, bool dBad, bool uBad) {
if (this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
try {
this.Invoke((Action)(() => {
lblDownVal.Text = d.ToString("0.00") + " Mbps"; lblDownVal.ForeColor = dBad ? Color.FromArgb(255, 95, 95) : Color.FromArgb(80, 220, 120);
lblUpVal.Text = u.ToString("0.00") + " Mbps"; lblUpVal.ForeColor = uBad ? Color.FromArgb(255, 95, 95) : Color.FromArgb(90, 200, 250);
lblPingVal.Text = p + " ms"; lblPingVal.ForeColor = p > 50 ? Color.FromArgb(255, 95, 95) : Color.FromArgb(255, 200, 80);
}));
} catch { }
}
void UpdatePingOnly(long p) {
if (this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
try {
this.Invoke((Action)(() => {
lblPingVal.Text = p + " ms";
lblPingVal.ForeColor = p > 50 ? Color.FromArgb(255, 95, 95) : Color.FromArgb(255, 200, 80);
}));
} catch { }
}
void UpdateBanner(string txt, Color bg, Color fg) {
if (this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
try {
this.Invoke((Action)(() => { lblStatusBanner.Text = txt; lblStatusBanner.BackColor = bg; lblStatusBanner.ForeColor = fg; }));
} catch { }
}
void AddRecord(string time, string status, double d, double u, long p, string reason, string url, bool isAlert) {
string line = string.Format("{0},{1},{2},{3},{4}/{5}M,{6}%,{7},\"{8}\",{9}\r\n", time, status, d, u, baseDown, baseUp, tolPercent, p, reason, url);
try {
File.AppendAllText(csvPath, line, new UTF8Encoding(true));
} catch (IOException) {
string backupCsv = csvPath.Replace(".csv", "_寫入鎖定備援.csv");
try { File.AppendAllText(backupCsv, line, new UTF8Encoding(true)); } catch { }
}
if (this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
try {
this.Invoke((Action)(() => {
int idx = dgvLogs.Rows.Add(time, status, d.ToString("0.00"), u.ToString("0.00"), p.ToString(), reason);
var row = dgvLogs.Rows.Cast<DataGridViewRow>().ElementAt(idx);
row.DefaultCellStyle.BackColor = isAlert ? Color.FromArgb(90, 35, 42) : Color.FromArgb(32, 70, 52);
dgvLogs.FirstDisplayedScrollingRowIndex = idx;
}));
} catch { }
}
async Task<long> GetTcpPingAsync(string host, int port) {
try { using (var c = new TcpClient()) { var sw = Stopwatch.StartNew(); var t = c.ConnectAsync(host, port); if (await Task.WhenAny(t, Task.Delay(1500)) == t && c.Connected) { sw.Stop(); return sw.ElapsedMilliseconds; } } } catch { }
return 999;
}
[STAThread]
static void Main() {
try { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
catch (Exception ex) { MessageBox.Show("啟動失敗：\n" + ex.ToString(), "錯誤診斷"); }
}
}
}
'@

$foundCli = Get-ChildItem -Path $env:USERPROFILE, $env:TEMP -Filter "speedtest.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 1000000 } | Select-Object -First 1
if (-not $foundCli) {
Write-Host "正在從官方下載區準備測速核心進行封裝..." -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$tmpZip = Join-Path $env:TEMP "st_pack.zip"
$tmpDir = Join-Path $env:TEMP "st_pack_dir"
Invoke-WebRequest -Uri "https://speedtest.net" -OutFile $tmpZip
Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force
$foundCli = Get-Item (Join-Path $tmpDir "speedtest.exe")
}
$realDesk = [Environment]::GetFolderPath("Desktop")
if (-not (Test-Path $realDesk)) { $realDesk = Join-Path $env:USERPROFILE "Desktop" }
$exeGui = Join-Path $realDesk "網速哨兵 Pro.exe"
$fullGui = $g1 + "`n" + $g2 + "`n" + $g3 + "`n" + $g4 + "`n" + $g5
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
$cp.GenerateExecutable = $true
$cp.OutputAssembly = $exeGui
$cp.CompilerOptions = '/target:winexe /resource:"{0}",speedtest.exe' -f $foundCli.FullName
$cp.ReferencedAssemblies.AddRange(@("System.dll", "System.Core.dll", "System.Windows.Forms.dll", "System.Drawing.dll", "System.Web.Extensions.dll"))
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$res = $provider.CompileAssemblyFromSource($cp, $fullGui)
Remove-Item (Join-Path $env:TEMP "st_pack.zip"), (Join-Path $env:TEMP "st_pack_dir") -Recurse -Force -ErrorAction SilentlyContinue
if ($res.Errors.Count -gt 0) {
$res.Errors | ForEach-Object { Write-Host $_.ErrorText -ForegroundColor Red }
} else {
Write-Host "打包成功！已生成單一綠色執行檔：$exeGui" -ForegroundColor Green
}
