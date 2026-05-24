using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace WindowsUpdatePauser
{
    /// <summary>
    /// 程序入口类。
    /// 应用程序从这里启动，创建并显示主窗体 MainForm。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();               // 启用 Windows 视觉样式（圆角控件等）
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());                // 启动主窗体消息循环
        }
    }

    /// <summary>
    /// 主窗体：Windows 更新暂停工具的核心界面。
    /// 功能包括：暂停/恢复 Windows 更新、设置暂停时长、管理员权限检测。
    ///
    /// 工作原理：通过修改注册表键值控制 Windows Update 的暂停行为：
    ///   HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
    ///   写入 PauseUpdatesStartTime / PauseUpdatesExpiryTime 等值。
    ///
    /// 如果你要修改界面文字、布局位置、颜色等，在下面找对应区域即可。
    /// </summary>
    internal sealed class MainForm : Form
    {
        // ============================================================
        // 常量定义区 — 修改这些值会影响应用名称和注册表操作目标路径
        // ============================================================

        /// <summary>应用程序标题，显示在窗口标题栏</summary>
        private const string AppName = "Windows Update Pauser";

        /// <summary>
        /// Windows Update 暂停设置所在的注册表路径。
        /// 这是 Windows 官方支持的暂停更新机制，修改此路径下的值即可控制更新暂停。
        /// 完整路径：HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
        /// </summary>
        private const string SettingsPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

        /// <summary>点击 GitHub 链接时打开的仓库地址，修改为你的仓库即可</summary>
        private const string GitHubUrl = "https://github.com/kefanlee/Windows-Update-Pauser";

        /// <summary>当前版本号，发版时修改此值即可，与 git tag 保持一致</summary>
        private const string CurrentVersion = "1.0";

        /// <summary>GitHub Release API 地址，用于检查最新版本</summary>
        private const string ReleasesApiUrl = "https://api.github.com/repos/kefanlee/Windows-Update-Pauser/releases/latest";

        // ============================================================
        // 颜色定义区 — 修改这些值可以统一更换整个 UI 的配色方案
        // Color.FromArgb(A, R, G, B) 或 Color.FromArgb(R, G, B)
        // ============================================================

        /// <summary>页面背景色（浅灰蓝）</summary>
        private static readonly Color PageBack = Color.FromArgb(245, 247, 250);

        /// <summary>卡片/面板背景色（白色）</summary>
        private static readonly Color CardBack = Color.White;

        /// <summary>主要文字颜色（深色）</summary>
        private static readonly Color MainText = Color.FromArgb(29, 37, 48);

        /// <summary>次要/辅助文字颜色（灰色）</summary>
        private static readonly Color SubText = Color.FromArgb(105, 115, 128);

        /// <summary>分隔线/边框颜色</summary>
        private static readonly Color LineColor = Color.FromArgb(226, 232, 240);

        /// <summary>管理员标签 — 背景色（浅绿）</summary>
        private static readonly Color AdminFill = Color.FromArgb(227, 244, 240);

        /// <summary>管理员标签 — 文字颜色（深绿）</summary>
        private static readonly Color AdminText = Color.FromArgb(24, 101, 91);

        /// <summary>操作按钮 — 背景色（浅蓝）</summary>
        private static readonly Color ActionFill = Color.FromArgb(225, 241, 247);

        /// <summary>操作按钮 — 文字颜色（深蓝）</summary>
        private static readonly Color ActionText = Color.FromArgb(31, 93, 116);

        /// <summary>预设天数按钮（Chip） — 背景色（浅绿）</summary>
        private static readonly Color ChipFill = Color.FromArgb(236, 246, 244);

        /// <summary>预设天数按钮（Chip） — 文字颜色（深绿）</summary>
        private static readonly Color ChipText = Color.FromArgb(43, 105, 96);

        /// <summary>天数输入框 — 背景色</summary>
        private static readonly Color InputFill = Color.FromArgb(239, 244, 250);

        /// <summary>天数输入框 — 文字颜色</summary>
        private static readonly Color InputText = Color.FromArgb(48, 82, 112);

        /// <summary>警告标签/按钮 — 背景色（浅橙）</summary>
        private static readonly Color WarningFill = Color.FromArgb(255, 246, 230);

        /// <summary>警告标签/按钮 — 文字颜色（深橙）</summary>
        private static readonly Color WarningText = Color.FromArgb(150, 86, 26);

        /// <summary>禁用状态按钮 — 背景色</summary>
        private static readonly Color DisabledFill = Color.FromArgb(241, 243, 246);

        /// <summary>禁用状态按钮 — 文字颜色</summary>
        private static readonly Color DisabledText = Color.FromArgb(145, 153, 164);

        // ============================================================
        // UI 控件声明区 — 窗体上所有可交互的控件
        // ============================================================

        /// <summary>右上角管理员状态提示标签（"管理员模式" / "需要管理员权限"）</summary>
        private readonly Label _adminHint = new Label();

        /// <summary>状态卡片中左侧圆点指示器</summary>
        private readonly Label _statusDot = new Label();

        /// <summary>状态卡片中的标题文字（"Windows 更新已暂停" / "正常运行"）</summary>
        private readonly Label _statusTitle = new Label();

        /// <summary>状态卡片中的详细说明文字</summary>
        private readonly Label _statusDetail = new Label();

        /// <summary>状态卡片右下角最后刷新时间</summary>
        private readonly Label _lastLine = new Label();

        /// <summary>时长卡片中的预计截止日期预览文字</summary>
        private readonly Label _previewLine = new Label();

        /// <summary>自定义天数输入框</summary>
        private readonly TextBox _daysText = new TextBox();

        /// <summary>"暂停更新" / "修改暂停时间" 按钮</summary>
        private CleanButton _btnPause;

        /// <summary>"恢复更新" 按钮</summary>
        private CleanButton _btnResume;

        /// <summary>"以管理员运行" 按钮（仅在非管理员模式下显示）</summary>
        private CleanButton _btnAdmin;

        /// <summary>"刷新状态" 按钮</summary>
        private CleanButton _btnRefresh;

        /// <summary>预设天数快捷选择按钮数组（7天/14天/30天/1年）</summary>
        private ChipButton[] _chips;

        /// <summary>底部 GitHub 链接标签</summary>
        private Label _linkGitHub;
        private Label _linkCheckUpdate;

        /// <summary>日志文件路径（自动生成在 logs 目录下）</summary>
        private string _logFile;

        /// <summary>当前更新是否处于暂停状态</summary>
        private bool _isPaused;

        /// <summary>当前程序是否以管理员权限运行</summary>
        private bool _isAdmin;

        /// <summary>
        /// 防止循环更新的标志位。
        /// 当代码通过 SetDays() 修改文本框内容时设为 true，
        /// 避免 TextChanged 事件触发 OnDaysTextChanged 导致递归。
        /// </summary>
        private bool _editingText;

        /// <summary>
        /// 构造函数：初始化主窗体。
        /// 在这里设置窗口属性、初始化日志、检测管理员权限、构建 UI。
        ///
        /// 如果要修改窗口大小，修改 ClientSize 的 width 和 height 即可。
        /// </summary>
        public MainForm()
        {
            Text = AppName;                                            // 窗口标题
            StartPosition = FormStartPosition.CenterScreen;            // 启动时居中显示
            FormBorderStyle = FormBorderStyle.FixedSingle;             // 固定边框，禁止拖拽调整大小
            MaximizeBox = false;                                       // 禁用最大化按钮
            ClientSize = new Size(760, 500);                           // 窗口客户区大小（宽760，高500）
            BackColor = PageBack;                                      // 窗口背景色
            Font = new Font("Microsoft YaHei UI", 11f);                // 默认字体
            TryLoadIcon();                                             // 尝试加载图标文件

            _logFile = InitLogFile();                                  // 初始化日志文件
            _isAdmin = IsAdministrator();                              // 检测是否以管理员身份运行

            BuildUi();                                                 // 构建所有 UI 控件
            SetDays("30");                                             // 默认设为 30 天
            RefreshStatus();                                           // 从注册表读取当前暂停状态并刷新界面

            // Shown 事件：窗体首次显示后触发，用于微调控件的初始状态
            Shown += delegate
            {
                _daysText.SelectionStart = _daysText.Text.Length;      // 光标移到文本末尾
                _daysText.SelectionLength = 0;
                ActiveControl = null;                                  // 取消焦点，避免输入框默认高亮
                ForceRefreshButtons();                                 // 强制刷新所有按钮颜色
                ThreadPool.QueueUserWorkItem(_ => CheckForUpdates(silent: true));  // 后台静默检查新版本
            };
        }

        // ============================================================
        // UI 构建区 — 以下方法负责创建和布局所有界面元素
        // 如果要调整某个控件的位置、大小，修改 Location 和 Size 即可
        // ============================================================

        /// <summary>
        /// 构建主界面布局。
        /// 整体结构（从上到下）：
        ///   1. 标题行
        ///   2. 状态卡片
        ///   3. 时长卡片（左） + 操作卡片（右）
        ///   4. 底部提示文字 + 版权信息
        /// </summary>
        private void BuildUi()
        {
            // ---------- 标题行 ----------
            // 英文大标题
            Controls.Add(new Label
            {
                Text = "Windows Update Pauser",
                Location = new Point(38, 26),                          // 位置 (x, y)
                Size = new Size(440, 38),
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                ForeColor = MainText,
                BackColor = Color.Transparent
            });

            // 中文副标题
            Controls.Add(new Label
            {
                Text = "Windows 更新暂停工具",
                Location = new Point(41, 68),
                Size = new Size(320, 28),
                Font = new Font("Microsoft YaHei UI", 11f),
                ForeColor = SubText,
                BackColor = Color.Transparent
            });

            // 右上角管理员状态标签
            _adminHint.Location = new Point(570, 36);
            _adminHint.Size = new Size(150, 34);
            _adminHint.TextAlign = ContentAlignment.MiddleCenter;
            _adminHint.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            Controls.Add(_adminHint);

            // 构建各个功能卡片
            BuildStatusCard();      // 状态显示卡片
            BuildDurationCard();    // 时长选择卡片（左侧）
            BuildActionCard();      // 操作按钮卡片（右侧）
            BuildFooter();          // 底部提示栏
        }

        /// <summary>
        /// 构建状态卡片。
        /// 显示当前 Windows Update 的状态（已暂停/正常运行）、暂停截止时间、
        /// 最后刷新时间等信息。
        ///
        /// 卡片内控件从上到下：
        ///   状态圆点 + 标题行
        ///   详细状态说明行
        ///   最后刷新时间（右下角）
        /// </summary>
        private void BuildStatusCard()
        {
            // 创建圆角卡片容器
            CleanCard card = new CleanCard
            {
                Location = new Point(38, 108),
                Size = new Size(682, 120),
                Radius = 20,                                            // 圆角半径
                FillColor = CardBack,
                BorderColor = LineColor
            };
            Controls.Add(card);

            // 状态指示圆点（绿色=正常，橙色=已暂停）
            _statusDot.Location = new Point(30, 34);
            _statusDot.Size = new Size(16, 16);
            _statusDot.BackColor = AdminText;
            card.Controls.Add(_statusDot);

            // 状态标题（大字）
            _statusTitle.Location = new Point(60, 25);
            _statusTitle.Size = new Size(500, 34);
            _statusTitle.Font = new Font("Microsoft YaHei UI", 16.5f, FontStyle.Bold);
            _statusTitle.ForeColor = MainText;
            _statusTitle.BackColor = Color.Transparent;
            card.Controls.Add(_statusTitle);

            // 状态详细说明
            _statusDetail.Location = new Point(61, 64);
            _statusDetail.Size = new Size(565, 30);
            _statusDetail.Font = new Font("Microsoft YaHei UI", 10.5f);
            _statusDetail.ForeColor = SubText;
            _statusDetail.BackColor = Color.Transparent;
            card.Controls.Add(_statusDetail);

            // 右下角最后刷新时间
            _lastLine.Location = new Point(500, 90);
            _lastLine.Size = new Size(150, 24);
            _lastLine.TextAlign = ContentAlignment.MiddleRight;
            _lastLine.Font = new Font("Segoe UI", 9f);
            _lastLine.ForeColor = Color.FromArgb(145, 153, 164);
            _lastLine.BackColor = Color.Transparent;
            card.Controls.Add(_lastLine);
        }

        /// <summary>
        /// 构建时长选择卡片（左侧）。
        /// 包含 4 个预设天数按钮（7天/14天/30天/1年）和自定义天数输入框。
        ///
        /// 如果要添加新的预设按钮，参考 CreateChip() 调用方式，
        /// 并相应调整 _chips 数组和按钮位置。
        /// </summary>
        private void BuildDurationCard()
        {
            // 创建圆角卡片容器
            CleanCard card = new CleanCard
            {
                Location = new Point(38, 248),
                Size = new Size(462, 182),
                Radius = 20,
                FillColor = CardBack,
                BorderColor = LineColor
            };
            Controls.Add(card);

            // 卡片标题
            card.Controls.Add(new Label
            {
                Text = "暂停时长",
                Location = new Point(30, 22),
                Size = new Size(180, 30),
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                ForeColor = MainText,
                BackColor = Color.Transparent
            });

            // 卡片说明文字
            card.Controls.Add(new Label
            {
                Text = "选择预设天数，或输入自定义天数。",
                Location = new Point(31, 55),
                Size = new Size(320, 26),
                Font = new Font("Microsoft YaHei UI", 10f),
                ForeColor = SubText,
                BackColor = Color.Transparent
            });

            // -------- 预设天数按钮（Chip） --------
            // 每个按钮的构造函数参数：显示文字, 对应天数, x坐标, y坐标
            // 如果要修改预设天数，修改第二个参数即可
            _chips = new ChipButton[]
            {
                CreateChip("7 天", 7, 30, 88),      // 7天预设
                CreateChip("14 天", 14, 122, 88),    // 14天预设
                CreateChip("30 天", 30, 214, 88),    // 30天预设
                CreateChip("1 年", 365, 306, 88)     // 365天预设
            };

            for (int i = 0; i < _chips.Length; i++)
            {
                card.Controls.Add(_chips[i]);
            }

            // -------- 自定义天数输入框区域 --------
            Panel inputBox = new Panel
            {
                Location = new Point(30, 132),
                Size = new Size(154, 36),
                BackColor = InputFill
            };
            card.Controls.Add(inputBox);

            // "自定义" 标签
            inputBox.Controls.Add(new Label
            {
                Text = "自定义",
                Location = new Point(13, 6),
                Size = new Size(56, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                ForeColor = InputText,
                BackColor = Color.Transparent
            });

            // 天数输入框（纯数字，无边框）
            _daysText.BorderStyle = BorderStyle.None;
            _daysText.Location = new Point(68, 7);
            _daysText.Size = new Size(48, 24);
            _daysText.TextAlign = HorizontalAlignment.Center;
            _daysText.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _daysText.ForeColor = InputText;
            _daysText.BackColor = InputFill;
            _daysText.TabStop = false;                                 // Tab 键不会聚焦到此输入框
            _daysText.TextChanged += delegate { OnDaysTextChanged(); }; // 内容改变时实时更新预览
            _daysText.Leave += delegate { NormalizeDaysText(); };       // 失去焦点时规范化输入
            inputBox.Controls.Add(_daysText);

            // "天" 单位标签
            inputBox.Controls.Add(new Label
            {
                Text = "天",
                Location = new Point(120, 6),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                ForeColor = InputText,
                BackColor = Color.Transparent
            });

            // 预计截止日期预览标签
            _previewLine.Location = new Point(198, 134);
            _previewLine.Size = new Size(250, 30);
            _previewLine.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            _previewLine.ForeColor = ActionText;
            _previewLine.BackColor = Color.Transparent;
            card.Controls.Add(_previewLine);
        }

        /// <summary>
        /// 构建操作按钮卡片（右侧）。
        /// 包含：暂停更新、恢复更新、以管理员运行、刷新状态 四个按钮。
        ///
        /// 按钮的显示/隐藏逻辑：
        ///   - 管理员模式下："以管理员运行" 隐藏，其他按钮可见
        ///   - 非管理员模式下："以管理员运行" 可见，"暂停/恢复" 禁用
        /// </summary>
        private void BuildActionCard()
        {
            CleanCard card = new CleanCard
            {
                Location = new Point(518, 248),
                Size = new Size(202, 182),
                Radius = 20,
                FillColor = CardBack,
                BorderColor = LineColor
            };
            Controls.Add(card);

            // 卡片标题
            card.Controls.Add(new Label
            {
                Text = "操作",
                Location = new Point(22, 22),
                Size = new Size(120, 30),
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                ForeColor = MainText,
                BackColor = Color.Transparent
            });

            // 【暂停更新】按钮 — 点击后执行 PauseUpdates()
            _btnPause = new CleanButton
            {
                Text = "暂停更新",
                Location = new Point(22, 58),
                Size = new Size(158, 34),
                Mode = ButtonMode.Action
            };
            _btnPause.Click += delegate { PauseUpdates(); };
            card.Controls.Add(_btnPause);

            // 【恢复更新】按钮 — 点击后弹出确认框，确认后清除暂停设置
            _btnResume = new CleanButton
            {
                Text = "恢复更新",
                Location = new Point(22, 98),
                Size = new Size(158, 34),
                Mode = ButtonMode.Action
            };
            _btnResume.Click += delegate { ConfirmAndClearPause(); };
            card.Controls.Add(_btnResume);

            // 【以管理员运行】按钮 — 仅在非管理员模式下显示
            _btnAdmin = new CleanButton
            {
                Text = "以管理员运行",
                Location = new Point(22, 98),
                Size = new Size(158, 34),
                Mode = ButtonMode.Warning                         // 警告风格（橙色）
            };
            _btnAdmin.Click += delegate { RestartAsAdministrator(); };
            card.Controls.Add(_btnAdmin);

            // 【刷新状态】按钮 — 重新读取注册表并更新界面
            _btnRefresh = new CleanButton
            {
                Text = "刷新状态",
                Location = new Point(22, 138),
                Size = new Size(158, 34),
                Mode = ButtonMode.Action
            };
            _btnRefresh.Click += delegate { RefreshStatus(); };
            card.Controls.Add(_btnRefresh);
        }

        /// <summary>
        /// 构建底部信息栏。
        /// 显示操作提示和版权信息。
        /// 如果要修改版权信息文字，在这里修改 Text 内容即可。
        /// </summary>
        private void BuildFooter()
        {
            // 操作提示
            Controls.Add(new Label
            {
                Text = "提示：修改 Windows 更新状态需要管理员权限。",
                Location = new Point(41, 448),
                Size = new Size(410, 22),
                Font = new Font("Microsoft YaHei UI", 9.5f),
                ForeColor = SubText,
                BackColor = Color.Transparent
            });

            // 检查更新 — 点击手动检测 GitHub 是否有新版本
            _linkCheckUpdate = new Label
            {
                Text = "检查更新",
                Location = new Point(570, 446),
                Size = new Size(90, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Underline),
                ForeColor = SubText,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _linkCheckUpdate.Click += delegate { ThreadPool.QueueUserWorkItem(_ => CheckForUpdates()); };
            _linkCheckUpdate.MouseEnter += delegate { _linkCheckUpdate.ForeColor = ActionText; };
            _linkCheckUpdate.MouseLeave += delegate { _linkCheckUpdate.ForeColor = SubText; };
            Controls.Add(_linkCheckUpdate);

            // GitHub 链接 — 位于底栏右侧，与检查更新相邻
            _linkGitHub = new Label
            {
                Text = "GitHub",
                Location = new Point(665, 446),
                Size = new Size(55, 24),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Underline),
                ForeColor = SubText,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _linkGitHub.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true }); }
                catch { }
            };
            _linkGitHub.MouseEnter += delegate { _linkGitHub.ForeColor = ActionText; };
            _linkGitHub.MouseLeave += delegate { _linkGitHub.ForeColor = SubText; };
            Controls.Add(_linkGitHub);

            // 版权信息 — 修改这里的 Text 可以更换底部文字
            Controls.Add(new Label
            {
                Text = "Powered by ZheL · Version " + CurrentVersion + " · © 2026 · 仅供学习交流",
                Location = new Point(41, 472),
                Size = new Size(680, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 9.5f),
                ForeColor = Color.FromArgb(145, 153, 164),
                BackColor = Color.Transparent
            });
        }

        /// <summary>
        /// 强制刷新所有按钮的颜色。
        /// 这是一个辅助方法，用于解决窗口首次显示时按钮颜色可能不正确的问题。
        /// 通常在 Shown 事件中调用。
        /// </summary>
        private void ForceRefreshButtons()
        {
            // 重置所有预设天数按钮的颜色
            if (_chips != null)
            {
                for (int i = 0; i < _chips.Length; i++)
                {
                    _chips[i].BackColor = ChipFill;
                    _chips[i].ForeColor = ChipText;
                }
            }

            // 重新应用各操作按钮的颜色
            ApplyButtonPalette(_btnPause);
            ApplyButtonPalette(_btnResume);
            ApplyButtonPalette(_btnAdmin);
            ApplyButtonPalette(_btnRefresh);
        }

        /// <summary>
        /// 根据按钮的当前状态（启用/禁用、模式）设置正确的颜色。
        ///
        /// 逻辑：
        ///   1. 禁用状态 → 灰色
        ///   2. Warning 模式 → 橙色（"以管理员运行"按钮）
        ///   3. Action 模式（默认）→ 蓝色
        /// </summary>
        private void ApplyButtonPalette(CleanButton button)
        {
            if (button == null) return;

            // 禁用状态：统一灰色
            if (!button.Enabled)
            {
                button.BackColor = DisabledFill;
                button.ForeColor = DisabledText;
                return;
            }

            // 警告模式（如"以管理员运行"）：橙色
            if (button.Mode == ButtonMode.Warning)
            {
                button.BackColor = WarningFill;
                button.ForeColor = WarningText;
            }
            // 普通操作按钮：蓝色
            else
            {
                button.BackColor = ActionFill;
                button.ForeColor = ActionText;
            }
        }

        /// <summary>
        /// 创建一个预设天数选择按钮（Chip）。
        /// 点击后自动将对应天数填入输入框。
        ///
        /// 参数：
        ///   text - 按钮显示的文字（如 "7 天"）
        ///   days - 对应的天数
        ///   x, y - 按钮在卡片中的位置坐标
        /// </summary>
        private ChipButton CreateChip(string text, int days, int x, int y)
        {
            ChipButton chip = new ChipButton();
            chip.Text = text;
            chip.Days = days;
            chip.Location = new Point(x, y);
            chip.Size = new Size(78, 34);
            chip.BackColor = ChipFill;
            chip.ForeColor = ChipText;
            chip.Click += delegate { SetDays(days.ToString()); };
            return chip;
        }

        // ============================================================
        // 核心功能方法区
        // ============================================================

        /// <summary>
        /// 暂停/修改 Windows 更新。
        ///
        /// 工作流程：
        ///   1. 检查管理员权限（没有则提示）
        ///   2. 验证输入的天数是否为正整数
        ///   3. 计算暂停截止日期（UTC 时间）
        ///   4. 弹出确认对话框
        ///   5. 写入注册表 6 个键值（FeatureUpdates、QualityUpdates、通用暂停）
        ///   6. 调用 UsoClient.exe RefreshSettings 刷新系统更新设置
        ///   7. 刷新界面状态
        ///
        /// 注册表写入的键值（字符串格式：yyyy-MM-ddTHH:mm:ss.000Z）：
        ///   - PauseUpdatesStartTime       → 暂停开始时间
        ///   - PauseUpdatesExpiryTime      → 暂停到期时间
        ///   - PauseFeatureUpdatesStartTime → 功能更新暂停开始
        ///   - PauseFeatureUpdatesEndTime   → 功能更新暂停截止
        ///   - PauseQualityUpdatesStartTime → 质量更新暂停开始
        ///   - PauseQualityUpdatesEndTime   → 质量更新暂停截止
        /// </summary>
        private void PauseUpdates()
        {
            if (!_isAdmin)
            {
                MessageBox.Show("请先以管理员身份运行本程序。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 读取并验证用户输入的天数
            string inputDays;
            if (!TryReadPositiveIntegerDays(out inputDays))
            {
                MessageBox.Show("请输入正整数天数。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 计算暂停时间范围
            DateTime start = DateTime.UtcNow;                          // 暂停开始时间 = 当前 UTC 时间

            // 计算最大可设置天数（DateTime.MaxValue 能表示的范围）
            long maxDays = (long)((DateTime.MaxValue.Date - start.Date).TotalDays - 1);

            // 如果用户输入超过系统日期上限，自动截断为最大值
            bool overMax = ComparePositiveIntegerText(inputDays, maxDays.ToString()) > 0;
            double safeDays = overMax ? maxDays : double.Parse(inputDays);
            DateTime end = start.AddDays(safeDays);                    // 暂停截止时间

            // 构建确认对话框提示语
            // 如果当前已暂停，提示"修改"；否则提示"暂停"
            string ask = _isPaused
                ? "是否将暂停时间修改至：" + end.ToLocalTime().ToString("yyyy年MM月dd日") + "？"
                : "是否暂停 Windows 更新至：" + end.ToLocalTime().ToString("yyyy年MM月dd日") + "？";

            // 如果超过上限，额外提示用户
            if (overMax)
            {
                ask += Environment.NewLine + Environment.NewLine + "输入天数超过系统日期上限，将自动设置为系统可支持的最大日期。";
            }

            if (MessageBox.Show(ask, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                // 打开注册表键（如果不存在则创建）
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(SettingsPath))
                {
                    // 格式化时间为 Windows Update 注册表要求的格式
                    string startText = start.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
                    string endText = end.ToString("yyyy-MM-ddTHH:mm:ss.000Z");

                    // 写入 3 组共 6 个键值，分别控制不同类型的更新暂停
                    // 组1：通用更新暂停
                    key.SetValue("PauseUpdatesStartTime", startText, RegistryValueKind.String);
                    key.SetValue("PauseUpdatesExpiryTime", endText, RegistryValueKind.String);

                    // 组2：功能更新暂停（大版本更新，如 22H2 → 23H2）
                    key.SetValue("PauseFeatureUpdatesStartTime", startText, RegistryValueKind.String);
                    key.SetValue("PauseFeatureUpdatesEndTime", endText, RegistryValueKind.String);

                    // 组3：质量更新暂停（月度安全补丁等）
                    key.SetValue("PauseQualityUpdatesStartTime", startText, RegistryValueKind.String);
                    key.SetValue("PauseQualityUpdatesEndTime", endText, RegistryValueKind.String);
                }

                // 通知 Windows Update 服务刷新设置，使注册表修改立即生效
                RunHidden("UsoClient.exe", "RefreshSettings");

                // 记录日志并刷新界面
                Log("已暂停 Windows 更新至 " + end.ToLocalTime().ToString("yyyy-MM-dd"));
                RefreshStatus();

                MessageBox.Show("已暂停更新至：\n" + end.ToLocalTime().ToString("yyyy年MM月dd日"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("权限不足，请右键选择“以管理员身份运行”。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("暂停更新失败。\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 显示确认对话框，用户确认后清除暂停设置。
        /// 仅在管理员模式下可调用。
        /// </summary>
        private void ConfirmAndClearPause()
        {
            if (!_isAdmin)
            {
                MessageBox.Show("请先以管理员身份运行本程序。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("是否立即恢复 Windows 更新？", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            ClearPause();
        }

        /// <summary>
        /// 清除注册表中的所有暂停设置，恢复 Windows 自动更新。
        ///
        /// 注意：这里使用 DeleteValue 而非设置为空字符串，
        /// 因为 Windows Update 系统通过键值是否存在来判断是否暂停。
        /// 键值不存在 = 未暂停。
        ///
        /// DeleteValue 的第二个参数 false 表示：如果键值不存在不抛异常。
        /// </summary>
        private void ClearPause()
        {
            try
            {
                // 需要删除的所有暂停相关键值（与 PauseUpdates 中写入的对应）
                string[] keysToDelete = new string[]
                {
                    "PauseUpdatesStartTime", "PauseUpdatesExpiryTime",
                    "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime",
                    "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime"
                };

                // 以可写模式打开注册表键
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SettingsPath, true))
                {
                    if (key != null)
                    {
                        foreach (string name in keysToDelete)
                        {
                            key.DeleteValue(name, false);              // false = 键值不存在时不抛异常
                        }
                    }
                }

                // 通知 Windows Update 刷新
                RunHidden("UsoClient.exe", "RefreshSettings");
                Log("已恢复 Windows 更新");
                RefreshStatus();

                MessageBox.Show("已恢复 Windows 自动更新。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("权限不足，请右键选择“以管理员身份运行”。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("恢复更新失败。\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 从注册表读取当前暂停状态并更新界面。
        ///
        /// 判断逻辑：
        ///   1. 打开注册表键
        ///   2. 读取 PauseUpdatesExpiryTime 值
        ///   3. 解析为 DateTime
        ///   4. 如果到期时间大于当前 UTC 时间，则处于暂停状态
        /// </summary>
        private void RefreshStatus()
        {
            DateTime expiry;
            _isPaused = TryGetPauseExpiry(out expiry);

            if (_isPaused)
            {
                // 已暂停：显示橙色圆点 + 截止日期
                _statusDot.BackColor = Color.FromArgb(218, 130, 38);    // 橙色圆点
                _statusTitle.Text = "Windows 更新已暂停";
                _statusDetail.Text = "暂停截止时间：" + expiry.ToLocalTime().ToString("yyyy年MM月dd日 HH:mm");
                _btnPause.Text = "修改暂停时间";                          // 按钮文字改为"修改"
                _btnResume.Enabled = _isAdmin;                           // 恢复按钮在管理员模式下可用
            }
            else
            {
                // 正常运行：显示绿色圆点
                _statusDot.BackColor = AdminText;
                _statusTitle.Text = "Windows 更新正常运行";
                _statusDetail.Text = "当前未设置更新暂停，可选择时长后暂停更新。";
                _btnPause.Text = "暂停更新";                              // 按钮文字恢复为"暂停"
                _btnResume.Enabled = false;                              // 恢复按钮禁用（没有暂停就不需要恢复）
            }

            // 根据管理员权限控制按钮状态
            _btnPause.Enabled = _isAdmin;                                // 仅管理员可暂停
            _btnResume.Visible = _isAdmin;                               // 管理员模式下显示恢复按钮
            _btnAdmin.Visible = !_isAdmin;                               // 非管理员模式下显示"以管理员运行"按钮
            _btnRefresh.Enabled = true;                                  // 刷新按钮始终可用

            // 更新右上角管理员状态标签
            _adminHint.Text = _isAdmin ? "管理员模式" : "需要管理员权限";
            _adminHint.ForeColor = _isAdmin ? AdminText : WarningText;
            _adminHint.BackColor = _isAdmin ? AdminFill : WarningFill;

            // 重新设置所有按钮颜色
            ApplyButtonPalette(_btnPause);
            ApplyButtonPalette(_btnResume);
            ApplyButtonPalette(_btnAdmin);
            ApplyButtonPalette(_btnRefresh);

            // 更新最后刷新时间
            _lastLine.Text = "状态刷新于 " + DateTime.Now.ToString("HH:mm:ss");
        }

        /// <summary>
        /// 输入框内容改变时的处理。
        /// 实时更新预计截止日期预览，并高亮匹配的预设按钮。
        /// </summary>
        private void OnDaysTextChanged()
        {
            if (_editingText) return;                                    // 代码修改文本时不触发

            string days;
            if (!TryReadPositiveIntegerDays(out days))
            {
                _previewLine.Text = "请输入正整数";                       // 无效输入时显示提示
                UpdateChipSelection(null);                               // 取消所有预设按钮的高亮
                return;
            }

            UpdatePreview(days);                                         // 更新预览
            UpdateChipSelection(days);                                   // 更新预设按钮高亮
        }

        /// <summary>
        /// 输入框失去焦点时规范化输入。
        /// 如果输入无效，自动恢复为默认值 30 天。
        /// </summary>
        private void NormalizeDaysText()
        {
            string days;
            if (!TryReadPositiveIntegerDays(out days))
            {
                SetDays("30");                                           // 无效输入 → 恢复默认 30
                return;
            }

            SetDays(days);                                               // 规范化显示（去前导零等）
        }

        /// <summary>
        /// 设置天数值并同步更新 UI（预览、预设按钮高亮）。
        /// 这个方法由外部调用，会设置 _editingText 标志位防止 TextChanged 递归。
        /// </summary>
        private void SetDays(string days)
        {
            _editingText = true;                                         // 加锁，避免修改文本框时触发 OnDaysTextChanged
            _daysText.Text = days;                                       // 更新文本框内容
            _editingText = false;                                        // 解锁

            string value = NormalizePositiveIntegerText(days);           // 规范化（去空格、去前导零）
            if (value != null)
            {
                UpdatePreview(value);                                    // 更新预计截止日期预览
                UpdateChipSelection(value);                              // 更新预设按钮高亮
            }
        }

        /// <summary>
        /// 尝试从文本框中读取正整数天数。
        /// 返回 true 表示读取成功，days 为规范化后的数字字符串。
        /// 返回 false 表示输入无效（空、含非数字字符、为 0）。
        /// </summary>
        private bool TryReadPositiveIntegerDays(out string days)
        {
            days = NormalizePositiveIntegerText(_daysText.Text);
            return days != null;
        }

        /// <summary>
        /// 将用户输入规范化：
        ///   1. 去除首尾空格
        ///   2. 检查是否全为数字
        ///   3. 去除前导零（例如 "007" → "7"）
        ///   4. 空字符串视为无效
        ///
        /// 返回 null 表示输入无效；返回非 null 为规范化后的正整数字符串。
        /// </summary>
        private string NormalizePositiveIntegerText(string text)
        {
            if (text == null) return null;

            text = text.Trim();                                          // 去除首尾空格
            if (text.Length == 0) return null;                           // 空字符串 → 无效

            // 检查每个字符是否都是数字
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9') return null;        // 含非数字字符 → 无效
            }

            text = text.TrimStart('0');                                  // 去除前导零
            if (text.Length == 0) return null;                           // 全是0的情况（如 "000"）→ 无效
            return text;
        }

        /// <summary>
        /// 比较两个正整数字符串的大小。
        /// 先按长度比较（长的更大），长度相同按字典序比较。
        ///
        /// 返回值：>0 表示 left > right，<0 表示 left < right，0 表示相等。
        /// </summary>
        private int ComparePositiveIntegerText(string left, string right)
        {
            left = NormalizePositiveIntegerText(left);
            right = NormalizePositiveIntegerText(right);

            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            // 先比较位数（更长的数字一定更大）
            if (left.Length > right.Length) return 1;
            if (left.Length < right.Length) return -1;

            // 位数相同，按字典序比较
            return string.CompareOrdinal(left, right);
        }

        /// <summary>
        /// 更新预计截止日期预览文字。
        /// 如果天数超过 DateTime 上限，显示 "预计至 9999年12月31日"。
        /// </summary>
        private void UpdatePreview(string days)
        {
            // 计算今天到 DateTime.MaxValue 的最大天数范围
            long maxDays = (long)((DateTime.MaxValue.Date - DateTime.Now.Date).TotalDays - 1);

            // 超出系统范围的特殊处理
            if (ComparePositiveIntegerText(days, maxDays.ToString()) > 0)
            {
                _previewLine.Text = "预计至 9999年12月31日";
                return;
            }

            DateTime end = DateTime.Now.AddDays(double.Parse(days));
            _previewLine.Text = "预计至 " + end.ToString("yyyy年MM月dd日");
        }

        /// <summary>
        /// 根据当前天数更新预设按钮的高亮状态。
        /// 如果用户输入 "30"，则 30 天按钮高亮；如果输入不匹配任何预设，全部取消高亮。
        /// days 为 null 时取消所有高亮。
        /// </summary>
        private void UpdateChipSelection(string days)
        {
            if (_chips == null) return;

            string normalized = NormalizePositiveIntegerText(days);
            for (int i = 0; i < _chips.Length; i++)
            {
                // 比较规范化后的天数是否匹配
                _chips[i].Selected = normalized == _chips[i].Days.ToString();
                _chips[i].Invalidate();                                  // 触发重绘
            }
        }

        /// <summary>
        /// 尝试从注册表读取暂停到期时间。
        /// 返回 true 表示当前处于暂停状态（且未过期），expiry 为到期时间。
        /// 返回 false 表示未暂停或读取失败。
        ///
        /// 注意：注册表中的时间是 UTC 时间，比较时也需要转成 UTC 时间。
        /// </summary>
        private bool TryGetPauseExpiry(out DateTime expiry)
        {
            expiry = DateTime.MinValue;
            try
            {
                // 以只读模式打开注册表键
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SettingsPath))
                {
                    object value = key == null ? null : key.GetValue("PauseUpdatesExpiryTime");
                    if (value == null) return false;                     // 键值不存在 → 未暂停

                    DateTime parsed;
                    if (!DateTime.TryParse(value.ToString(), out parsed)) return false; // 解析失败 → 未暂停

                    expiry = parsed;

                    // 到期时间必须大于当前 UTC 时间才算有效暂停
                    return expiry.ToUniversalTime() > DateTime.UtcNow;
                }
            }
            catch
            {
                return false;                                            // 任何异常都视为未暂停
            }
        }

        /// <summary>
        /// 以管理员身份重新启动本程序。
        /// 使用 ShellExecute + "runas" 动词触发 UAC 提权。
        /// 启动新实例后关闭当前实例。
        ///
        /// 如果用户拒绝 UAC 或提权失败，catch 块会显示友好提示而不崩溃。
        /// </summary>
        private void RestartAsAdministrator()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Application.ExecutablePath;              // 本程序的路径
                info.UseShellExecute = true;                             // 使用 ShellExecute（必须设为 true 才能提权）
                info.Verb = "runas";                                     // "runas" = 触发 UAC 以管理员运行
                Process.Start(info);                                     // 启动新实例
                Close();                                                 // 关闭当前非管理员实例
            }
            catch
            {
                // 用户取消 UAC 或提权失败时不崩溃，仅显示提示
                MessageBox.Show("未能以管理员身份重新启动。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 检测当前进程是否以管理员权限运行。
        /// 通过 WindowsIdentity + WindowsPrincipal 检查 Administrator 角色。
        ///
        /// 这是 Windows .NET 标准的权限检测方式，不需要 P/Invoke。
        /// </summary>
        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();         // 获取当前用户身份
                WindowsPrincipal principal = new WindowsPrincipal(identity);     // 创建安全主体
                return principal.IsInRole(WindowsBuiltInRole.Administrator);     // 检查 Administrator 角色
            }
            catch
            {
                return false;                                                    // 异常时保守假设非管理员
            }
        }

        /// <summary>
        /// 尝试加载窗口图标。
        /// 按优先级依次尝试：
        ///   1. 程序目录下的 icon.ico 文件
        ///   2. 当前工作目录下的 icon.ico 文件
        ///   3. 从本 exe 中提取默认图标
        ///
        /// 如果全部失败则使用系统默认图标。
        /// </summary>
        private void TryLoadIcon()
        {
            try
            {
                // 优先尝试程序所在目录下的 icon.ico
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");

                // 其次尝试当前工作目录下的 icon.ico
                if (!File.Exists(ico))
                    ico = Path.Combine(Environment.CurrentDirectory, "icon.ico");

                // 如果 icon.ico 存在就使用，否则提取 exe 自带的图标
                Icon = File.Exists(ico) ? new Icon(ico) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }                                                    // 图标加载失败不影响程序功能
        }

        /// <summary>
        /// 初始化日志文件。
        /// 日志存放在程序目录下的 logs 文件夹中，按日期命名：yyyy-MM-dd.log。
        /// 如果 logs 文件夹不存在则自动创建。
        ///
        /// 日志格式：yyyy-MM-dd HH:mm:ss  消息内容
        /// </summary>
        private string InitLogFile()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);                              // 确保目录存在
            return Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        }

        /// <summary>
        /// 写入一条日志记录。
        /// 日志包含时间戳和消息内容，追加到当天的日志文件中。
        /// 写入失败时静默忽略（日志不是关键功能）。
        /// </summary>
        private void Log(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;
            try { File.AppendAllText(_logFile, line + Environment.NewLine); } catch { }
        }

        /// <summary>
        /// 在后台无窗口运行一个可执行文件。
        ///
        /// 主要用于调用 UsoClient.exe RefreshSettings 来刷新 Windows Update 设置。
        /// 等待最多 5 秒，超时则放弃等待。
        /// </summary>
        /// <param name="file">可执行文件名（如 "UsoClient.exe"）</param>
        /// <param name="args">命令行参数（如 "RefreshSettings"）</param>
        private static void RunHidden(string file, string args)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = file;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.CreateNoWindow = true;              // 不创建控制台窗口
                    process.StartInfo.UseShellExecute = false;           // 不使用 ShellExecute，直接创建进程
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden; // 隐藏窗口
                    process.Start();
                    process.WaitForExit(5000);                           // 等待最多 5 秒
                }
            }
            catch { }                                                    // 调用失败不影响主流程
        }

        /// <summary>
        /// 后台检查 GitHub 是否有新版本。
        /// 使用 ETag 条件请求避免触发 API 限流（未认证 60次/小时 → 304 不计入）。
        /// silent=true: 仅在发现新版本时弹窗（启动时自动检查）。
        /// silent=false: 始终弹窗告知结果（手动点击按钮）。
        /// </summary>
        private void CheckForUpdates(bool silent = false)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ReleasesApiUrl);
                request.UserAgent = "WindowsUpdatePauser";
                request.Timeout = 8000;

                // 读取缓存的 ETag，发送条件请求减少 API 消耗
                string etagFile = Path.Combine(Path.GetTempPath(), "wup_update_etag");
                if (File.Exists(etagFile))
                {
                    string cachedEtag = File.ReadAllText(etagFile).Trim();
                    if (!string.IsNullOrEmpty(cachedEtag))
                        request.Headers["If-None-Match"] = cachedEtag;
                }

                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        // 保存新 ETag
                        string newEtag = response.Headers["ETag"];
                        if (!string.IsNullOrEmpty(newEtag))
                            File.WriteAllText(etagFile, newEtag);

                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string json = reader.ReadToEnd();

                            // 从 JSON 中提取 tag_name（例如 "v1.0"）
                            int tagIndex = json.IndexOf("\"tag_name\"");
                            if (tagIndex < 0) { if (!silent) BeginInvoke(new Action(() => ShowUpdateResult(false, "无法获取版本信息"))); return; }

                            int colon = json.IndexOf(':', tagIndex);
                            int start = json.IndexOf('"', colon + 1) + 1;
                            int end = json.IndexOf('"', start);
                            string tag = json.Substring(start, end - start).TrimStart('v');

                            if (IsNewerVersion(CurrentVersion, tag))
                            {
                                string finalTag = tag;
                                BeginInvoke(new Action(() =>
                                {
                                    DialogResult result = MessageBox.Show(
                                        "发现新版本 v" + finalTag + "，请前往 GitHub 下载体验吧！\n\n" +
                                        "当前版本：v" + CurrentVersion + "\n" +
                                        "最新版本：v" + finalTag,
                                        AppName + " - 版本更新",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Information);

                                    if (result == DialogResult.Yes)
                                    {
                                        try { Process.Start(new ProcessStartInfo(GitHubUrl + "/releases/latest") { UseShellExecute = true }); }
                                        catch { }
                                    }
                                }));
                            }
                            else if (!silent)
                            {
                                BeginInvoke(new Action(() => ShowUpdateResult(true, null)));
                            }
                        }
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
                    if (errorResponse != null)
                    {
                        if (errorResponse.StatusCode == HttpStatusCode.NotModified)
                        {
                            // 304 = 无新版本（ETag 命中，不计入限流）
                            if (!silent)
                                BeginInvoke(new Action(() => ShowUpdateResult(true, null)));
                            return;
                        }
                        if ((int)errorResponse.StatusCode == 429 || errorResponse.StatusCode == HttpStatusCode.Forbidden)
                        {
                            if (!silent)
                                BeginInvoke(new Action(() => ShowUpdateResult(false, "请求过于频繁，请稍后再试")));
                            return;
                        }
                    }
                    throw;
                }
            }
            catch
            {
                if (!silent)
                {
                    BeginInvoke(new Action(() => ShowUpdateResult(false, "网络错误，无法检查更新")));
                }
            }
        }

        /// <summary>
        /// 显示版本检查结果。
        /// isLatest=true → 当前已是最新；isLatest=false → 出错，显示 reason。
        /// </summary>
        private void ShowUpdateResult(bool isLatest, string reason)
        {
            if (isLatest)
            {
                MessageBox.Show(
                    "当前已是最新版本 v" + CurrentVersion + "。",
                    AppName + " - 版本更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    (reason ?? "检查失败") + "。\n当前版本：v" + CurrentVersion,
                    AppName + " - 版本更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 比较两个语义化版本号。
        /// 返回 true 表示 latest > current（有新版本）。
        /// 版本号格式：major.minor.patch（如 1.2.0）。
        /// </summary>
        private static bool IsNewerVersion(string current, string latest)
        {
            try
            {
                string[] curParts = current.Split('.');
                string[] newParts = latest.Split('.');
                int len = Math.Max(curParts.Length, newParts.Length);
                for (int i = 0; i < len; i++)
                {
                    int cur = i < curParts.Length ? int.Parse(curParts[i]) : 0;
                    int nxt = i < newParts.Length ? int.Parse(newParts[i]) : 0;
                    if (nxt > cur) return true;
                    if (nxt < cur) return false;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// 创建圆角矩形 GraphicsPath。
        /// 这是一个 UI 辅助方法，被 CleanCard 和 ChipButton 的 OnPaint 使用。
        ///
        /// 参数：
        ///   bounds - 矩形区域
        ///   radius - 圆角半径（像素）
        ///
        /// 返回值可直接用于 Graphics.FillPath() 和 Graphics.DrawPath()。
        /// </summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();

            // 依次绘制四个圆角弧线：左上 → 右上 → 右下 → 左下
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);              // 左上角
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);      // 右上角
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); // 右下角
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);      // 左下角

            path.CloseFigure();                                          // 闭合图形
            return path;
        }
    }

    /// <summary>
    /// 圆角卡片面板控件。
    /// 继承自 Panel，重写 OnPaint 来绘制圆角矩形背景和边框。
    ///
    /// 可自定义属性：
    ///   FillColor   - 填充颜色（默认白色）
    ///   BorderColor - 边框颜色
    ///   Radius      - 圆角半径（默认 14px）
    ///
    /// 注意：DoubleBuffered = true 可以消除自绘控件闪烁。
    /// </summary>
    internal sealed class CleanCard : Panel
    {
        /// <summary>卡片填充颜色</summary>
        public Color FillColor { get; set; }

        /// <summary>卡片边框颜色</summary>
        public Color BorderColor { get; set; }

        /// <summary>卡片圆角半径（像素）</summary>
        public int Radius { get; set; }

        public CleanCard()
        {
            FillColor = Color.White;
            BorderColor = Color.FromArgb(226, 232, 240);
            Radius = 14;
            BackColor = Color.Transparent;                               // 设为透明，让父容器背景透出
            DoubleBuffered = true;                                       // 双缓冲消除闪烁
        }

        /// <summary>
        /// 自定义绘制：绘制圆角背景 + 圆角边框。
        /// 先用父容器背景色清除（实现伪透明效果），再绘制圆角填充和边框。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;          // 抗锯齿，让圆角边缘平滑

            // 先用父容器背景色填充，实现 Panel 控件的"透明"效果
            e.Graphics.Clear(Parent == null ? Color.Transparent : Parent.BackColor);

            // 绘制圆角填充和边框
            using (GraphicsPath path = MainForm.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.FillPath(brush, path);                        // 填充圆角矩形内部
                e.Graphics.DrawPath(pen, path);                          // 绘制圆角矩形边框
            }

            base.OnPaint(e);                                             // 调用基类绘制子控件
        }
    }

    /// <summary>
    /// 按钮模式枚举。
    ///   Action  - 普通操作按钮（蓝色系）
    ///   Warning - 警告类按钮（橙色系，如"以管理员运行"）
    /// </summary>
    internal enum ButtonMode
    {
        Action,
        Warning
    }

    /// <summary>
    /// 风格化按钮控件（基于 Label）。
    ///
    /// 支持两种模式：
    ///   Action  → 蓝色系配色
    ///   Warning → 橙色系配色
    ///
    /// 通过 OnEnabledChanged 重写实现启用/禁用时的自动颜色切换。
    ///
    /// 如果你要修改按钮颜色，修改 OnEnabledChanged 中和 ApplyButtonPalette 中的色值即可。
    /// </summary>
    internal sealed class CleanButton : Label
    {
        /// <summary>按钮模式（Action=蓝, Warning=橙）</summary>
        public ButtonMode Mode { get; set; }

        public CleanButton()
        {
            Mode = ButtonMode.Action;
            AutoSize = false;                                            // 手动控制大小
            TextAlign = ContentAlignment.MiddleCenter;                   // 文字居中
            Cursor = Cursors.Hand;                                       // 鼠标悬停时显示手型光标
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            BackColor = Color.FromArgb(225, 241, 247);                   // 默认蓝色背景
            ForeColor = Color.FromArgb(31, 93, 116);                     // 默认蓝色文字
            BorderStyle = BorderStyle.None;

            // 注册鼠标进入/离开事件实现悬停效果
            this.MouseEnter += (s, e) => this.Cursor = Cursors.Hand;
            this.MouseLeave += (s, e) => this.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// 当 Enabled 状态改变时自动调整按钮颜色。
        /// 禁用时变灰，启用时根据 Mode 恢复对应配色。
        /// </summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            if (Enabled)
            {
                if (Mode == ButtonMode.Warning)
                {
                    BackColor = Color.FromArgb(255, 246, 230);           // 橙色背景
                    ForeColor = Color.FromArgb(150, 86, 26);             // 橙色文字
                }
                else
                {
                    BackColor = Color.FromArgb(225, 241, 247);           // 蓝色背景
                    ForeColor = Color.FromArgb(31, 93, 116);             // 蓝色文字
                }
                Cursor = Cursors.Hand;                                   // 可交互 → 手型光标
            }
            else
            {
                BackColor = Color.FromArgb(241, 243, 246);               // 禁用灰色背景
                ForeColor = Color.FromArgb(145, 153, 164);               // 禁用灰色文字
                Cursor = Cursors.Default;                                // 不可交互 → 默认光标
            }
        }
    }

    /// <summary>
    /// 预设天数选择按钮（Chip/标签样式）。
    ///
    /// 属性：
    ///   Days     - 对应的天数（用于判断是否匹配用户输入）
    ///   Selected - 是否处于选中高亮状态
    ///
    /// 目前 Selected 属性仅用于标记状态，ChipButton 本身不重写 OnPaint，
    /// 因此选中和非选中外观一致。如果需要选中高亮效果，需要为 ChipButton
    /// 添加 OnPaint 重写来根据 Selected 绘制不同样式。
    /// </summary>
    internal sealed class ChipButton : Label
    {
        /// <summary>此按钮对应的天数（7/14/30/365）</summary>
        public int Days { get; set; }

        /// <summary>是否处于选中状态（当前输入的天数与此按钮匹配）</summary>
        public bool Selected { get; set; }

        public ChipButton()
        {
            AutoSize = false;
            TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            BackColor = Color.FromArgb(236, 246, 244);                   // 默认浅绿色背景
            ForeColor = Color.FromArgb(43, 105, 96);                     // 默认深绿色文字
            BorderStyle = BorderStyle.None;
        }
    }
}
