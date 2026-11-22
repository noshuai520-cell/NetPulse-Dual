using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DualEngineNetWatchdog {
    public class MainForm : Form {
        ComboBox cmbPlan, cmbTol;
        Button btnSingle, btnSentinel, btnStop, btnCsv, btnHelp;
        Label lblHwInfo, lblStatusBanner, lblDiagReport, lblSrv, lblIsp, lblUpVal, lblIdlePing, lblLoadDown, lblLoadUp, lblJitter;
        Label lblWebMs, lblWebPct, lblStreamMbps, lblStreamPct, lbl4kPct, lbl4kTag, lblDnsMs, lblShapingTxt;
        Panel pnlGauge, pnlChart, pnlRing, pnlWebBar, pnlStreamBar, pnl4kBar;
        DataGridView dgvLogs;
        CancellationTokenSource loopCts;
        double baseDown = 500, baseUp = 500, tolPercent = 8.0, currentGaugeMbps = 0, maxGaugeMbps = 1000, webPctVal = 0, streamPctVal = 0, video4kVal = 0;
        string baseDir = "", cliExe = "", lsExe = "", csvPath = "";
        bool wasAbnormal = false;
        DateTime lastFullTestTime = DateTime.MinValue;
        List<double> speedHistory = new List<double>();
        int appScore = 0;

        public MainForm() {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            cliExe = Path.Combine(Path.GetTempPath(), "ookla_st_netpulse_v2.exe");
            lsExe = Path.Combine(Path.GetTempPath(), "librespeed_netpulse_v2.exe");
            InitUI();
            DetectHardwareAndIpAsync();
        }
        protected override void OnFormClosing(FormClosingEventArgs e) { if (loopCts != null) { try { loopCts.Cancel(); } catch { } } base.OnFormClosing(e); }

        void InitUI() {
            this.Text = "NetPulse Dual v2.0｜雙核網路脈搏診斷儀  (by Le_morries)";
            this.Size = new Size(1060, 790); this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(16, 22, 34); this.ForeColor = Color.White;
            this.Font = new Font("Microsoft JhengHei UI", 9.5F); this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true; this.MinimumSize = new Size(1060, 790); this.DoubleBuffered = true;

            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(22, 30, 46) };
            Label lblTitle = new Label { Text = "  ⚡ NetPulse Dual v2.0｜雙核網路脈搏診斷儀", Font = new Font("Microsoft JhengHei UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(65, 210, 235), Location = new Point(10, 8), AutoSize = true };
            Label lblAuthor = new Label { Text = "Author: Le_morries  ", Font = new Font("Consolas", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(130, 155, 185), Dock = DockStyle.Right, Width = 185, TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand };
            lblAuthor.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://github.com") { UseShellExecute = true }); } catch { } };
            lblHwInfo = new Label { Text = "  正在偵測本機網卡硬體規格與內外網拓撲路徑...", Font = new Font("Microsoft JhengHei UI", 8.5F), ForeColor = Color.FromArgb(150, 170, 195), Location = new Point(12, 38), AutoSize = true };
            pnlTop.Controls.AddRange(new Control[] { lblTitle, lblAuthor, lblHwInfo }); this.Controls.Add(pnlTop);



            Panel pnlCtrl = new Panel { Location = new Point(16, 74), Size = new Size(1012, 86), BackColor = Color.FromArgb(24, 33, 50) };
            Label l1 = new Label { Text = "申裝頻寬方案：", Location = new Point(14, 10), AutoSize = true, ForeColor = Color.FromArgb(170, 185, 205), Font = new Font("Microsoft JhengHei UI", 8.5F) };
            cmbPlan = new ComboBox { Location = new Point(14, 30), Width = 205, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(35, 46, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbPlan.Items.AddRange(new object[] { "100M / 40M (一般家用)", "100M / 100M (對稱光纖)", "300M / 300M (高速光纖)", "500M / 250M (非對稱高速)", "500M / 500M (對稱高速)", "1G / 600M (1000M/600M)", "1G / 1G (對稱 1000M)" });
            cmbPlan.SelectedIndex = 4;

            Label l2 = new Label { Text = "容許誤差：", Location = new Point(230, 10), AutoSize = true, ForeColor = Color.FromArgb(170, 185, 205), Font = new Font("Microsoft JhengHei UI", 8.5F) };
            cmbTol = new ComboBox { Location = new Point(230, 30), Width = 185, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(35, 46, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbTol.Items.AddRange(new object[] { "5% (純光纖直連)", "8% (PPPoE 標頭建議)", "10% (一般家用共用)", "15% (寬鬆波動容許)" });
            cmbTol.SelectedIndex = 1;

            btnSingle = CreateBtn("▶ 單次雙引擎診斷", Color.FromArgb(30, 136, 229), 425, 26, 148); btnSingle.Click += (s, e) => StartEngine(false);
            btnSentinel = CreateBtn("🛡️ 啟動哨兵監控", Color.FromArgb(38, 166, 91), 580, 26, 142); btnSentinel.Click += (s, e) => StartEngine(true);
            btnHelp = CreateBtn("💡 哨兵功能說明", Color.FromArgb(48, 62, 85), 728, 26, 122); btnHelp.ForeColor = Color.FromArgb(80, 215, 245);
            btnStop = CreateBtn("■ 停止", Color.FromArgb(215, 58, 73), 856, 26, 70); btnStop.Enabled = false; btnStop.Click += BtnStop_Click;
            btnCsv = CreateBtn("📄 報表", Color.FromArgb(65, 80, 105), 932, 26, 70);
            btnCsv.Click += (s, e) => { if (!File.Exists(csvPath)) { MessageBox.Show("尚未產生報表！", "提示"); return; } try { Process.Start(new ProcessStartInfo(csvPath) { UseShellExecute = true }); } catch { Process.Start("notepad.exe", csvPath); } };


            string helpMsg = "💡 哨兵怎麼幫你顧網路？（免動手全自動）\n\n• 平時超省流（每 30 秒）：只做極輕微的連線心跳檢查，完全不吃頻寬。\n• 抓到變慢（立刻蒐證）：一旦發現卡頓或斷線，馬上啟動 A+B 雙引擎全速測速並存證。\n• 定時體檢（每 30 分鐘）：每半小時例行測一次，確保電信商沒偷降速。";
            new ToolTip { InitialDelay = 150, AutoPopDelay = 20000 }.SetToolTip(btnHelp, helpMsg);
            btnHelp.Click += (s, e) => MessageBox.Show(helpMsg, "哨兵運作白話說明", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Label lblHint = new Label { Text = "🛡️ 自動哨兵：平時安靜顧網（不佔網速、不影響追劇打遊戲），只有抓到網路變慢或斷線時，才會全速測速並留存客訴證據。", Location = new Point(14, 64), AutoSize = true, ForeColor = Color.FromArgb(145, 165, 190), Font = new Font("Microsoft JhengHei UI", 8.5F) };
            pnlCtrl.Controls.AddRange(new Control[] { l1, cmbPlan, l2, cmbTol, btnSingle, btnSentinel, btnHelp, btnStop, btnCsv, lblHint });
            this.Controls.Add(pnlCtrl);

            Panel pnlA = new Panel { Location = new Point(16, 170), Size = new Size(498, 345), BackColor = Color.FromArgb(24, 33, 50) };
            pnlA.Controls.Add(new Label { Text = "【引擎 A】底層物理頻寬與穩定度 (Ookla CLI)", Location = new Point(14, 12), AutoSize = true, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 215, 245) });
            pnlGauge = new Panel { Location = new Point(14, 42), Size = new Size(225, 130), BackColor = Color.FromArgb(20, 28, 42) }; pnlGauge.Paint += PnlGauge_Paint;
            pnlChart = new Panel { Location = new Point(249, 42), Size = new Size(235, 130), BackColor = Color.FromArgb(20, 28, 42) }; pnlChart.Paint += PnlChart_Paint;
            pnlA.Controls.AddRange(new Control[] { pnlGauge, pnlChart });

            lblSrv = CreateMiniCard(pnlA, "測試節點", "待測", 14, 180, 225, Color.White);
            lblIsp = CreateMiniCard(pnlA, "ISP 網路業者", "自動偵測中...", 249, 180, 235, Color.FromArgb(120, 220, 255));
            lblUpVal = CreateMiniCard(pnlA, "上傳極限速率", "-- Mbps", 14, 234, 225, Color.FromArgb(80, 230, 160));
            lblIdlePing = CreateMiniCard(pnlA, "閒置延遲 (Idle)", "-- ms", 249, 234, 235, Color.White);
            lblLoadDown = CreateMiniCard(pnlA, "下載滿載延遲 (Bufferbloat)", "-- ms", 14, 288, 152, Color.FromArgb(255, 200, 90));
            lblLoadUp = CreateMiniCard(pnlA, "上傳滿載延遲", "-- ms", 174, 288, 148, Color.FromArgb(255, 200, 90));
            lblJitter = CreateMiniCard(pnlA, "抖動 / 掉包率", "-- ms / --%", 330, 288, 154, Color.FromArgb(140, 240, 170));
            this.Controls.Add(pnlA);

            Panel pnlB = new Panel { Location = new Point(530, 170), Size = new Size(498, 345), BackColor = Color.FromArgb(24, 33, 50) };
            pnlB.Controls.Add(new Label { Text = "【引擎 B】獨立開源引擎交叉驗證 (LibreSpeed CLI + 應用探針)", Location = new Point(14, 12), AutoSize = true, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 235, 185) });
            pnlRing = new Panel { Location = new Point(14, 42), Size = new Size(470, 68), BackColor = Color.FromArgb(20, 28, 42) }; pnlRing.Paint += PnlRing_Paint;
            pnlB.Controls.Add(pnlRing);

            lblWebMs = CreateBarCard(pnlB, "網頁平均載入時間 (HTTPS 握手)", "-- ms", 14, 118, out pnlWebBar, () => webPctVal, Color.FromArgb(55, 210, 235), out lblWebPct);
            lblStreamMbps = CreateBarCard(pnlB, "LibreSpeed 獨立下載速率 (無白名單實測)", "-- Mbps", 14, 176, out pnlStreamBar, () => streamPctVal, Color.FromArgb(65, 225, 165), out lblStreamPct);
            lbl4kPct = CreateBarCard(pnlB, "4K 影音串流流暢度預估", "-- %", 14, 234, out pnl4kBar, () => video4kVal, Color.FromArgb(100, 185, 255), out lbl4kTag);
            lblDnsMs = CreateMiniCard(pnlB, "DNS 解析反應時間", "-- ms", 14, 290, 228, Color.FromArgb(130, 240, 180));
            lblShapingTxt = CreateMiniCard(pnlB, "雙向 Bufferbloat 與限速診斷", "等待檢測", 252, 290, 232, Color.FromArgb(130, 240, 180));
            this.Controls.Add(pnlB);

            Panel pnlDiag = new Panel { Location = new Point(16, 524), Size = new Size(1012, 62), BackColor = Color.FromArgb(28, 38, 56) };
            pnlDiag.Controls.Add(new Label { Text = "【自動化工程師白話診斷報告】", Location = new Point(12, 6), AutoSize = true, Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 210, 95) });
            lblDiagReport = new Label { Text = "系統就緒。請點擊上方「單次雙引擎診斷」進行完整體檢，或啟動「哨兵監控」讓程式在背景 24 小時自動抓異常。", Location = new Point(12, 26), Size = new Size(988, 32), ForeColor = Color.FromArgb(220, 230, 242), Font = new Font("Microsoft JhengHei UI", 9F) };
            pnlDiag.Controls.Add(lblDiagReport);
            this.Controls.Add(pnlDiag);

            lblStatusBanner = new Label { Text = "  目前狀態：待機中（已內嵌官方測速核心與應用層探針）", Location = new Point(16, 594), Size = new Size(1012, 28), BackColor = Color.FromArgb(32, 44, 64), ForeColor = Color.FromArgb(180, 205, 230), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold) };
            this.Controls.Add(lblStatusBanner);

            dgvLogs = new DataGridView { Location = new Point(16, 628), Size = new Size(1012, 115), BackgroundColor = Color.FromArgb(22, 30, 44), BorderStyle = BorderStyle.None, AllowUserToAddRows = false, ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(42, 54, 75) };
            dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 44, 65); dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Bold); dgvLogs.ColumnHeadersHeight = 26;
            dgvLogs.DefaultCellStyle.BackColor = Color.FromArgb(22, 30, 44); dgvLogs.DefaultCellStyle.ForeColor = Color.White; dgvLogs.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 8.5F); dgvLogs.RowTemplate.Height = 24;
            dgvLogs.Columns.Add("Time", "發生時間"); dgvLogs.Columns.Add("Mode", "觸發類型"); dgvLogs.Columns.Add("Down", "物理下載"); dgvLogs.Columns.Add("Up", "物理上傳");
            dgvLogs.Columns.Add("Stream", "應用串流"); dgvLogs.Columns.Add("Ping", "閒置/滿載延遲"); dgvLogs.Columns.Add("Score", "體驗分"); dgvLogs.Columns.Add("Diag", "工程師診斷結論");
            dgvLogs.Columns["Time"].FillWeight = 115; dgvLogs.Columns["Diag"].FillWeight = 230;
            this.Controls.Add(dgvLogs);
                    // 讓控制列、診斷報告與狀態列隨視窗寬度自動拉長
            pnlCtrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlDiag.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblDiagReport.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblStatusBanner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // 讓底部的歷史紀錄表格在全螢幕時自動向左右、向下填滿空間（可同時看更多筆紀錄）
            dgvLogs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                        // 紀錄面板內所有子元件的原始比例，全螢幕時同步等比例放大鋪滿，零留白、不變形
            var baseRectsA = pnlA.Controls.Cast<Control>().Where(c => !(c is Label)).ToDictionary(c => c, c => c.Bounds);
            var baseRectsB = pnlB.Controls.Cast<Control>().Where(c => !(c is Label)).ToDictionary(c => c, c => c.Bounds);

            this.Resize += (s, e) => {
                int totalW = Math.Max(1012, this.ClientSize.Width - 32);
                int halfW = (totalW - 16) / 2;
                int extraH = Math.Max(0, (this.ClientSize.Height - 750) / 3);
                int panelH = 345 + extraH;

                pnlA.Location = new Point(16, 170);
                pnlA.Size = new Size(halfW, panelH);
                pnlB.Location = new Point(16 + halfW + 16, 170);
                pnlB.Size = new Size(totalW - halfW - 16, panelH);

                double sxA = pnlA.Width / 498.0, syA = pnlA.Height / 345.0;
                foreach (var kv in baseRectsA) {
                    Rectangle r = kv.Value;
                    kv.Key.Bounds = new Rectangle((int)(r.X * sxA), (int)(r.Y * syA), (int)(r.Width * sxA), (int)(r.Height * syA));
                }

                double sxB = pnlB.Width / 498.0, syB = pnlB.Height / 345.0;
                foreach (var kv in baseRectsB) {
                    Rectangle r = kv.Value;
                    kv.Key.Bounds = new Rectangle((int)(r.X * sxB), (int)(r.Y * syB), (int)(r.Width * sxB), (int)(r.Height * syB));
                }

                // 讓引擎 B 內的進度條與右側狀態標籤跟著寬度自動延伸
                foreach (Control card in baseRectsB.Keys) {
                    if (card is Panel && card != pnlRing && card.Controls.Count >= 4) {
                        card.Controls[1].Left = card.Width - 100;
                        card.Controls[3].Width = Math.Max(100, card.Width - 147);
                        card.Controls[3].Invalidate();
                    }
                }

                pnlGauge.Invalidate(); pnlChart.Invalidate(); pnlRing.Invalidate();

                pnlDiag.Top = 170 + panelH + 10;
                lblStatusBanner.Top = pnlDiag.Bottom + 8;
                dgvLogs.Top = lblStatusBanner.Bottom + 6;
                dgvLogs.Height = Math.Max(115, this.ClientSize.Height - dgvLogs.Top - 12);
            };

}
        Button CreateBtn(string text, Color bg, int x, int y, int w) {
            Button b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 32), BackColor = bg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; return b;
        }

        Label CreateMiniCard(Panel parent, string title, string initVal, int x, int y, int w, Color valColor) {
            Panel p = new Panel { Location = new Point(x, y), Size = new Size(w, 48), BackColor = Color.FromArgb(20, 28, 42) };
            Label lt = new Label { Text = title, Location = new Point(8, 5), AutoSize = true, ForeColor = Color.FromArgb(145, 165, 190), Font = new Font("Microsoft JhengHei UI", 8F) };
            Label lv = new Label { Text = initVal, Location = new Point(8, 22), Size = new Size(w - 14, 22), ForeColor = valColor, Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Bold) };
            p.Controls.AddRange(new Control[] { lt, lv }); parent.Controls.Add(p); return lv;
        }
        Label CreateBarCard(Panel parent, string title, string initVal, int x, int y, out Panel barPanel, Func<double> pctGetter, Color barColor, out Label lblRightTag) {
            Panel p = new Panel { Location = new Point(x, y), Size = new Size(470, 52), BackColor = Color.FromArgb(20, 28, 42) };
            Label lt = new Label { Text = title, Location = new Point(10, 5), AutoSize = true, ForeColor = Color.FromArgb(165, 185, 205), Font = new Font("Microsoft JhengHei UI", 8.5F) };
            Label tag = new Label { Text = "--", Location = new Point(370, 5), Size = new Size(90, 18), TextAlign = ContentAlignment.MiddleRight, ForeColor = barColor, Font = new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold) };
            Label lv = new Label { Text = initVal, Location = new Point(10, 24), Size = new Size(120, 22), ForeColor = Color.White, Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold) };
            Panel bp = new Panel { Location = new Point(135, 30), Size = new Size(323, 10), BackColor = Color.FromArgb(36, 48, 68) };
            bp.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; int fillW = (int)(bp.Width * (Math.Max(0, Math.Min(100, pctGetter())) / 100.0)); if (fillW > 0) using (SolidBrush br = new SolidBrush(barColor)) e.Graphics.FillRectangle(br, 0, 0, fillW, bp.Height); };
            p.Controls.AddRange(new Control[] { lt, tag, lv, bp }); parent.Controls.Add(p); barPanel = bp; lblRightTag = tag; return lv;
        }
        void PnlGauge_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(42, 12, 140, 140);
            using (Pen bgPen = new Pen(Color.FromArgb(40, 54, 78), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bgPen, rect, 150, 240);
            float sweep = (float)(Math.Min(1.0, currentGaugeMbps / Math.Max(100, maxGaugeMbps)) * 240.0);
            if (sweep > 1) using (Pen fgPen = new Pen(Color.FromArgb(65, 220, 240), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(fgPen, rect, 150, sweep);
            string numStr = currentGaugeMbps.ToString("0.0");
            using (Font fNum = new Font("Segoe UI", 18F, FontStyle.Bold)) using (Font fSub = new Font("Microsoft JhengHei UI", 8F))
            using (SolidBrush bw = new SolidBrush(Color.White)) using (SolidBrush bg = new SolidBrush(Color.FromArgb(150, 175, 200))) {
                g.DrawString(numStr, fNum, bw, 112 - g.MeasureString(numStr, fNum).Width / 2, 54);
                g.DrawString("Mbps 下載極限", fSub, bg, 112 - g.MeasureString("Mbps 下載極限", fSub).Width / 2, 92);
            }
        }
        void PnlChart_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen gp = new Pen(Color.FromArgb(32, 44, 64), 1)) for (int y = 26; y < 120; y += 26) g.DrawLine(gp, 10, y, 225, y);
            if (speedHistory.Count < 2) { using (Font f = new Font("Microsoft JhengHei UI", 8.5F)) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 130, 160))) g.DrawString("即時測速波形待命中...", f, b, 52, 56); return; }
            double maxVal = Math.Max(maxGaugeMbps, speedHistory.Max() * 1.1);
            PointF[] pts = new PointF[speedHistory.Count];
            for (int i = 0; i < speedHistory.Count; i++) pts[i] = new PointF(10 + (float)i / (speedHistory.Count - 1) * 215f, 118 - (float)(speedHistory[i] / maxVal * 102f));
            using (Pen lp = new Pen(Color.FromArgb(75, 225, 240), 2.2f)) g.DrawLines(lp, pts);
        }
        void PnlRing_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(16, 10, 48, 48);
            using (Pen bg = new Pen(Color.FromArgb(40, 54, 78), 5)) g.DrawEllipse(bg, r);
            Color ringCol = appScore >= 85 ? Color.FromArgb(65, 225, 165) : (appScore >= 65 ? Color.FromArgb(255, 200, 80) : Color.FromArgb(255, 95, 95));
            if (appScore > 0) using (Pen fg = new Pen(ringCol, 5)) g.DrawArc(fg, r, -90, (float)(appScore / 100.0 * 360.0));
            using (Font fScore = new Font("Segoe UI", 12F, FontStyle.Bold)) using (SolidBrush bCol = new SolidBrush(ringCol)) {
                string s = appScore > 0 ? appScore.ToString() : "--";
                SizeF sz = g.MeasureString(s, fScore); g.DrawString(s, fScore, bCol, 40 - sz.Width / 2, 34 - sz.Height / 2);
            }
            using (Font f1 = new Font("Microsoft JhengHei UI", 10.5F, FontStyle.Bold)) using (Font f2 = new Font("Microsoft JhengHei UI", 8.5F))
            using (SolidBrush bw = new SolidBrush(Color.White)) using (SolidBrush bgText = new SolidBrush(Color.FromArgb(150, 175, 200))) {
                g.DrawString(appScore > 0 ? string.Format("應用層綜合體驗分數：{0} / 100", appScore) : "應用層綜合體驗分數：等待測試", f1, bw, 78, 12);
                g.DrawString(appScore >= 85 ? "網頁載入、4K 影音串流與路由品質表現優良" : (appScore > 0 ? "偵測到應用層延遲或限速波動，請參閱下方診斷建議" : "啟動診斷後將自動評估網頁、串流與 DNS 反應"), f2, bgText, 78, 36);
            }
        }
        async void DetectHardwareAndIpAsync() {
            await Task.Run(() => {
                try {
                    string nicInfo = "未知網卡", localIp = "127.0.0.1", gwIp = "無閘道器";
                    long gwPing = -1;
                    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                        if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback) {
                            var ipProps = ni.GetIPProperties();
                            var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                            var unicast = ipProps.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                            if (unicast != null && gw != null) {
                                nicInfo = string.Format("{0} ({1:0.##}G)", ni.Description, ni.Speed / 1000000000.0);
                                localIp = unicast.Address.ToString();
                                gwIp = gw.Address.ToString();
                                try { using (Ping p = new Ping()) { var r = p.Send(gwIp, 800); if (r.Status == IPStatus.Success) gwPing = r.RoundtripTime; } } catch { }
                                break;
                            }
                        }
                    }

                    string pubIp = "查詢中...", ispName = "自動偵測中...";
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (HttpClient hc = new HttpClient { Timeout = TimeSpan.FromSeconds(4) }) {
                        hc.DefaultRequestHeaders.UserAgent.ParseAdd("NetPulseDual/2.0");
                        try {
                            string json = hc.GetStringAsync("https://ipinfo.io/json").Result;
                            var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                            if (d != null && d.ContainsKey("ip")) pubIp = Convert.ToString(d["ip"]).Trim();
                            if (d != null && d.ContainsKey("org")) {
                                string org = Convert.ToString(d["org"]);
                                int sp = org.IndexOf(' ');
                                ispName = sp > 0 ? org.Substring(sp + 1) : org;
                            }
                        } catch {
                            try { pubIp = hc.GetStringAsync("https://icanhazip.com").Result.Trim(); } catch { pubIp = "離線"; }
                        }
                    }

                    string natTag = (localIp == pubIp) ? "PPPoE / 實體直連" : (pubIp.StartsWith("100.") ? "CGNAT 共享架構" : "NAT 路由器架構");
                    string gwPingTxt = gwPing >= 0 ? gwPing + "ms" : "禁Ping";
                    string topoText = string.Format("  🧭 拓撲路徑：💻 本機 [{0} · {1}]  ➔  🏠 閘道器 [{2} · {3}]  ➔  🌐 外網出口 [{4}] ({5})", nicInfo, localIp, gwIp, gwPingTxt, pubIp, natTag);

                    this.Invoke((Action)(() => { lblHwInfo.Text = topoText; lblIsp.Text = ispName; }));
                } catch { }
            });
        }

        void StartEngine(bool isSentinelMode) {
            double[] downs = { 100, 100, 300, 500, 500, 1000, 1000 }, ups = { 40, 100, 300, 250, 500, 600, 1000 }, tols = { 5, 8, 10, 15 };
            baseDown = downs[cmbPlan.SelectedIndex]; baseUp = ups[cmbPlan.SelectedIndex]; tolPercent = tols[cmbTol.SelectedIndex]; maxGaugeMbps = baseDown * 1.1;
            btnSingle.Enabled = btnSentinel.Enabled = cmbPlan.Enabled = cmbTol.Enabled = false; btnStop.Enabled = true; wasAbnormal = false;
            csvPath = Path.Combine(baseDir, string.Format("{0}_{1}M_{2}.csv", isSentinelMode ? "雙引擎哨兵紀錄" : "雙引擎單次診斷", baseDown, DateTime.Now.ToString("yyyyMMdd_HHmm")));
            File.WriteAllText(csvPath, "發生時間,觸發類型,物理下載(Mbps),物理上傳(Mbps),應用串流(Mbps),閒置延遲(ms),下載滿載延遲(ms),網頁載入(ms),體驗分數,工程師診斷結論\r\n", new UTF8Encoding(true));
            loopCts = new CancellationTokenSource(); Task.Run(() => RunWorkflowAsync(isSentinelMode, loopCts.Token));
        }
        void BtnStop_Click(object sender, EventArgs e) { if (loopCts != null) loopCts.Cancel(); ResetButtons(); UpdateBanner("  目前狀態：已手動停止監控", Color.FromArgb(32, 44, 64), Color.LightGray); }
        void ResetButtons() { if (!this.IsDisposed && this.IsHandleCreated) this.Invoke((Action)(() => { btnSingle.Enabled = btnSentinel.Enabled = cmbPlan.Enabled = cmbTol.Enabled = true; btnStop.Enabled = false; })); }
        void UpdateBanner(string txt, Color bg, Color fg) { if (!this.IsDisposed && this.IsHandleCreated) try { this.Invoke((Action)(() => { lblStatusBanner.Text = txt; lblStatusBanner.BackColor = bg; lblStatusBanner.ForeColor = fg; })); } catch { } }
        void ExtractEmbeddedEngine() {
            if (!File.Exists(cliExe) || new FileInfo(cliExe).Length < 1000000) {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("speedtest.exe")) { if (s == null) throw new Exception("找不到內嵌的 Ookla 測速核心"); using (FileStream fs = new FileStream(cliExe, FileMode.Create, FileAccess.Write)) s.CopyTo(fs); }
            }
            if (!File.Exists(lsExe) || new FileInfo(lsExe).Length < 1000000) {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("librespeed-cli.exe")) { if (s == null) throw new Exception("找不到內嵌的 LibreSpeed 開源核心"); using (FileStream fs = new FileStream(lsExe, FileMode.Create, FileAccess.Write)) s.CopyTo(fs); }
            }
        }
        string GetBufferbloatGrade(long deltaMs) {
            if (deltaMs <= 15) return "良好";
            if (deltaMs <= 40) return "輕微";
            if (deltaMs <= 80) return "中度";
            return "嚴重";
        }
        async Task RunWorkflowAsync(bool isSentinel, CancellationToken token) {
            try { ExtractEmbeddedEngine(); } catch (Exception ex) { UpdateBanner("  ❌ 核心初始化失敗：" + ex.Message, Color.FromArgb(120, 30, 30), Color.White); ResetButtons(); return; }
            double passRatio = (100.0 - tolPercent) / 100.0; double minOkDown = Math.Round(baseDown * passRatio, 1), minOkUp = Math.Round(baseUp * passRatio, 1); int cycle = 0; bool firstRun = true;
            while (!token.IsCancellationRequested) {
                try {
                    string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    long quickPing = GetTcpPing("168.95.1.1", 53); long quickWeb = await MeasureHttpHeadMsAsync("https://google.com");
                    bool cooldownReady = (DateTime.Now - lastFullTestTime).TotalSeconds >= 240;
                    bool needFull = !isSentinel || firstRun || (cycle % 60 == 0) || (((quickPing > 60) || (quickWeb > 800) || wasAbnormal) && cooldownReady);
                    if (needFull) {
                        lastFullTestTime = DateTime.Now; this.Invoke((Action)(() => { speedHistory.Clear(); pnlChart.Invalidate(); }));
                        UpdateBanner("  ⚡ [第 1 棒 / 引擎 A] 正在全速測試物理極限頻寬與雙向滿載延遲 (Ookla)...", Color.FromArgb(35, 55, 88), Color.FromArgb(80, 215, 245));
                        var resA = await RunEngineA_OoklaAsync(token); if (resA == null) throw new Exception("引擎 A 連線逾時或無回應");
                        UpdateBanner("  🚀 [第 2 棒 / 引擎 B] 物理測速完成，接力啟動 LibreSpeed 開源引擎與 DNS/網頁實測...", Color.FromArgb(30, 68, 65), Color.FromArgb(80, 235, 185));
                        var resB = await RunEngineB_AppLayerAsync(resA.Item1, token);

                        long dlDelta = Math.Max(0, resA.Item5 - resA.Item3);
                        long ulDelta = Math.Max(0, resA.Item6 - resA.Item3);
                        long maxDelta = Math.Max(dlDelta, ulDelta);
                        string bbGrade = GetBufferbloatGrade(maxDelta);

                        bool dBad = resA.Item1 < minOkDown, uBad = resA.Item2 < minOkUp;
                        bool bufBad = maxDelta > 40; // 中度 (41~80ms) 或 嚴重 (>80ms) 皆納入異常
                        bool shpBad = (resA.Item1 >= minOkDown) && (resB.Item2 < resA.Item1 * 0.45);
                        bool dnsBad = resB.Item3 >= 999;
                        bool isAbn = dBad || uBad || bufBad || shpBad || dnsBad;

                        string diagMsg = BuildEngineerReport(resA.Item1, resA.Item2, minOkDown, minOkUp, resA.Item3, dlDelta, ulDelta, bbGrade, resB.Item1, resB.Item2, resB.Item3, shpBad);
                        UpdateAllDashboardUI(resA, resB, diagMsg, dBad, uBad, bufBad, shpBad, bbGrade, maxDelta);
                        AddLogAndCsv(nowStr, !isSentinel ? "單次雙引擎診斷" : (firstRun ? "哨兵啟動基準" : (isAbn ? "【偵測異常蒐證】" : "例行雙引擎體檢")), resA.Item1, resA.Item2, resB.Item2, resA.Item3, Math.Max(resA.Item5, resA.Item6), resB.Item1, resB.Item4, diagMsg, isAbn);
                        wasAbnormal = isAbn; firstRun = false;
                        if (!isSentinel) { UpdateBanner("  ✅ NetPulse Dual v2.0 雙引擎深度診斷完成！請檢視下方報告或匯出 CSV。", Color.FromArgb(28, 75, 58), Color.FromArgb(140, 250, 185)); break; }
                    } else {
                        int remainCool = Math.Max(0, 240 - (int)(DateTime.Now - lastFullTestTime).TotalSeconds);
                        UpdateBanner(string.Format("  🛡️ 哨兵背景站崗中 ({0}) | 骨幹延遲 {1}ms | 網頁反應 {2}ms | 零頻寬干擾{3}", DateTime.Now.ToString("HH:mm:ss"), quickPing, quickWeb, remainCool > 0 ? " | 防封鎖冷卻 " + remainCool + "s" : ""), Color.FromArgb(26, 65, 54), Color.FromArgb(145, 245, 185));
                    }
                } catch (OperationCanceledException) { break; }
                catch (Exception ex) { UpdateBanner("  ❌ 偵測到網路中斷或引擎異常：" + ex.Message, Color.FromArgb(120, 30, 30), Color.White); AddLogAndCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "【斷線/異常】", 0, 0, 0, 999, 999, 999, 0, "網路無法連線：" + ex.Message, true); wasAbnormal = true; if (!isSentinel) break; }
                if (!isSentinel) break; cycle++; try { await Task.Delay(30000, token); } catch { break; }
            }
            ResetButtons();
        }
        Task<Tuple<double, double, long, double, long, long, double, Tuple<string>>> RunEngineA_OoklaAsync(CancellationToken token) {
            return Task.Run(() => {
                var psi = new ProcessStartInfo { FileName = cliExe, Arguments = "--accept-license --accept-gdpr -f jsonl", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                double dMbps = 0, uMbps = 0, jitter = 0, loss = 0; long idlePing = 0, dlLoadPing = 0, ulLoadPing = 0; string srvName = "台灣本地機房"; var jss = new JavaScriptSerializer();
                using (var p = Process.Start(psi)) using (token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } })) {
                    string line;
                    while ((line = p.StandardOutput.ReadLine()) != null) {
                        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{")) continue;
                        try {
                            var obj = jss.Deserialize<Dictionary<string, object>>(line); if (obj == null || !obj.ContainsKey("type")) continue; string type = Convert.ToString(obj["type"]);
                            if (type == "testStart" && obj.ContainsKey("server")) { var srv = obj["server"] as Dictionary<string, object>; if (srv != null && srv.ContainsKey("name")) srvName = Convert.ToString(srv["name"]); }
                            else if (type == "download" && obj.ContainsKey("download")) { var dl = obj["download"] as Dictionary<string, object>; if (dl != null && dl.ContainsKey("bandwidth")) { double cur = Math.Round((Convert.ToDouble(dl["bandwidth"]) * 8.0) / 1000000.0, 2); this.Invoke((Action)(() => { currentGaugeMbps = cur; speedHistory.Add(cur); if (speedHistory.Count > 45) speedHistory.RemoveAt(0); pnlGauge.Invalidate(); pnlChart.Invalidate(); })); } }
                                                        else if (type == "result") {
                                if (obj.ContainsKey("isp")) {
                                    string realIsp = Convert.ToString(obj["isp"]);
                                    if (!string.IsNullOrWhiteSpace(realIsp)) this.Invoke((Action)(() => { lblIsp.Text = realIsp; }));
                                }
                                var dl = obj.ContainsKey("download") ? obj["download"] as Dictionary<string, object> : null; var ul = obj.ContainsKey("upload") ? obj["upload"] as Dictionary<string, object> : null; var pg = obj.ContainsKey("ping") ? obj["ping"] as Dictionary<string, object> : null;
                                if (dl != null) { if (dl.ContainsKey("bandwidth")) dMbps = Math.Round((Convert.ToDouble(dl["bandwidth"]) * 8.0) / 1000000.0, 2); if (dl.ContainsKey("latency")) { var lat = dl["latency"] as Dictionary<string, object>; if (lat != null && lat.ContainsKey("iqm")) dlLoadPing = (long)Math.Round(Convert.ToDouble(lat["iqm"])); } }
                                if (ul != null) { if (ul.ContainsKey("bandwidth")) uMbps = Math.Round((Convert.ToDouble(ul["bandwidth"]) * 8.0) / 1000000.0, 2); if (ul.ContainsKey("latency")) { var lat = ul["latency"] as Dictionary<string, object>; if (lat != null && lat.ContainsKey("iqm")) ulLoadPing = (long)Math.Round(Convert.ToDouble(lat["iqm"])); } }
                                if (pg != null) { if (pg.ContainsKey("latency")) idlePing = (long)Math.Round(Convert.ToDouble(pg["latency"])); if (pg.ContainsKey("jitter")) jitter = Math.Round(Convert.ToDouble(pg["jitter"]), 2); }
                                if (obj.ContainsKey("packetLoss")) loss = Math.Round(Convert.ToDouble(obj["packetLoss"]), 2);
                            }

                        } catch { }
                    }
                    p.WaitForExit(5000);
                }
                if (dlLoadPing == 0) dlLoadPing = idlePing; if (ulLoadPing == 0) ulLoadPing = idlePing;
                return (dMbps <= 0 && uMbps <= 0) ? null : Tuple.Create(dMbps, uMbps, idlePing, jitter, dlLoadPing, ulLoadPing, loss, srvName);
            }, token);
        }
        async Task<Tuple<long, double, long, int>> RunEngineB_AppLayerAsync(double physDownMbps, CancellationToken token) {
            long dnsMs = 999;
            try {
                var swDns = Stopwatch.StartNew();
                var entry = await Dns.GetHostEntryAsync("://youtube.com");
                swDns.Stop();
                if (entry != null && entry.AddressList != null && entry.AddressList.Length > 0) dnsMs = Math.Max(1, swDns.ElapsedMilliseconds);
            } catch { dnsMs = 999; }

            long webMs = (await MeasureHttpHeadMsAsync("https://google.com") + await MeasureHttpHeadMsAsync("https://1.1.1")) / 2;
            double lsDownMbps = 0;

            await Task.Run(() => {
                try {
                    var psi = new ProcessStartInfo { FileName = lsExe, Arguments = "--json --telemetry-level disabled", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                    using (var p = Process.Start(psi)) using (token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } })) {
                        string outJson = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(15000);
                        if (!string.IsNullOrWhiteSpace(outJson)) {
                            var jss = new JavaScriptSerializer();
                            var list = jss.Deserialize<List<Dictionary<string, object>>>(outJson.Trim());
                            if (list != null && list.Count > 0 && list[0].ContainsKey("download")) {
                                lsDownMbps = Math.Round(Convert.ToDouble(list[0]["download"]), 2);
                            }
                        }
                    }
                } catch { lsDownMbps = 0; }
            }, token);

            int dnsScore = (dnsMs >= 999) ? 0 : (dnsMs <= 30 ? 25 : (dnsMs <= 80 ? 18 : 10));
            int webScore = (webMs >= 999) ? 0 : (webMs <= 120 ? 35 : (webMs <= 300 ? 28 : 15));
            int spdScore = (lsDownMbps >= 80) ? 40 : (lsDownMbps >= 35 ? 32 : (lsDownMbps > 0 ? 18 : 0));
            int totalScore = Math.Min(100, dnsScore + webScore + spdScore);
            return Tuple.Create(webMs, lsDownMbps, dnsMs, totalScore);
        }

        long GetTcpPing(string host, int port) {
            try { var sw = Stopwatch.StartNew(); using (var c = new TcpClient()) { var ar = c.BeginConnect(host, port, null, null); if (ar.AsyncWaitHandle.WaitOne(1500)) { c.EndConnect(ar); sw.Stop(); return sw.ElapsedMilliseconds; } } } catch { } return 999;
        }
        async Task<long> MeasureHttpHeadMsAsync(string url) {
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; var sw = Stopwatch.StartNew(); using (HttpClient hc = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }) { var req = new HttpRequestMessage(HttpMethod.Head, url); var res = await hc.SendAsync(req); sw.Stop(); return sw.ElapsedMilliseconds; } } catch { return 999; }
        }
        string BuildEngineerReport(double d, double u, double md, double mu, long idle, long dlDelta, long ulDelta, string bbGrade, long webMs, double lsMbps, long dnsMs, bool shp) {
            if (dnsMs >= 999) return "⚠️ DNS 網域解析失敗 (999ms)！無法正常解析網址，建議檢查數據機 DNS 或改用 168.95.1.1 / 8.8.8.8。";
            if (shp) return "⚠️ 偵測到疑似 ISP 流量整形！Ookla 物理速率達標，但 LibreSpeed 獨立測速明顯偏低。";
            if (bbGrade == "嚴重" || bbGrade == "中度") return string.Format("⚠️ 雙向 Bufferbloat 延遲惡化【{0}】（下載 +{1}ms / 上傳 +{2}ms），滿載上傳下載時容易爆 Ping 瞬卡。", bbGrade, dlDelta, ulDelta);
            if (d < md || u < mu) return "⚠️ 下載或上傳物理頻寬低於方案標準容許值，建議重啟數據機或檢查網路線。";
            return string.Format("✅ 雙引擎驗證達標！Bufferbloat 延遲控制【{0}】（下載 +{1}ms / 上傳 +{2}ms），連線品質極佳。", bbGrade, dlDelta, ulDelta);
        }
        void UpdateAllDashboardUI(Tuple<double, double, long, double, long, long, double, Tuple<string>> rA, Tuple<long, double, long, int> rB, string diag, bool dBad, bool uBad, bool bufBad, bool shpBad, string bbGrade, long maxDelta) {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.Invoke((Action)(() => {
                currentGaugeMbps = rA.Item1; maxGaugeMbps = Math.Max(maxGaugeMbps, rA.Item1 * 1.1); pnlGauge.Invalidate(); pnlChart.Invalidate();
                lblSrv.Text = rA.Rest.Item1; lblUpVal.Text = rA.Item2.ToString("0.0") + " Mbps";
                lblIdlePing.Text = rA.Item3 + " ms"; lblLoadDown.Text = rA.Item5 + " ms"; lblLoadUp.Text = rA.Item6 + " ms";
                lblJitter.Text = rA.Item4 + " ms / " + rA.Item7.ToString("0.0") + "%";
                webPctVal = rB.Item1 >= 999 ? 0 : Math.Max(0, Math.Min(100, 100.0 - (rB.Item1 / 3.0))); lblWebMs.Text = rB.Item1 + " ms"; lblWebPct.Text = string.Format("{0:0}%", webPctVal); pnlWebBar.Invalidate();
                streamPctVal = Math.Max(0, Math.Min(100, (rB.Item2 / Math.Max(100, baseDown)) * 100.0)); lblStreamMbps.Text = rB.Item2.ToString("0.0") + " Mbps"; lblStreamPct.Text = string.Format("{0:0}%", streamPctVal); pnlStreamBar.Invalidate();
                video4kVal = rB.Item2 >= 25 ? 98 : (rB.Item2 >= 15 ? 85 : 45); lbl4kPct.Text = video4kVal + " %"; lbl4kTag.Text = rB.Item2 >= 25 ? "可順暢播 4K" : "建議 1080p"; pnl4kBar.Invalidate();
                lblDnsMs.Text = rB.Item3 + " ms";
                lblDnsMs.ForeColor = rB.Item3 >= 999 ? Color.FromArgb(255, 110, 110) : Color.FromArgb(130, 240, 180);

                lblShapingTxt.Text = shpBad ? "⚠️ 疑似限速" : string.Format("緩衝：{0} (+{1}ms)", bbGrade, maxDelta);
                lblShapingTxt.ForeColor = (shpBad || bbGrade == "嚴重" || bbGrade == "中度") ? Color.FromArgb(255, 120, 120) : (bbGrade == "輕微" ? Color.FromArgb(255, 205, 90) : Color.FromArgb(130, 240, 180));

                appScore = rB.Item4; pnlRing.Invalidate();
                lblDiagReport.Text = diag; lblDiagReport.ForeColor = (dBad || uBad || bufBad || shpBad || rB.Item3 >= 999) ? Color.FromArgb(255, 155, 120) : Color.FromArgb(180, 245, 205);
            }));
        }

        void AddLogAndCsv(string time, string mode, double d, double u, double strm, long p, long ld, long w, int sc, string diag, bool isAbn) {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.Invoke((Action)(() => {
                dgvLogs.Rows.Insert(0, time, mode, d.ToString("0.1") + "M", u.ToString("0.1") + "M", strm.ToString("0.1") + "M", string.Format("{0}ms / {1}ms", p, ld), sc, diag);
                if (dgvLogs.Rows.Count > 50) dgvLogs.Rows.RemoveAt(dgvLogs.Rows.Count - 1);
            }));
            try { File.AppendAllText(csvPath, string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},\"{9}\"\r\n", time, mode, d, u, strm, p, ld, w, sc, diag.Replace("\"", "\"\"")), new UTF8Encoding(true)); } catch { }
        }
        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
