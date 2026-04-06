using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TypeSimulator.Models;
using TypeSimulator.Services;
using TypeSimulator.Utilities;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace TypeSimulator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TextProcessor _textProcessor;
        private readonly TypeSimulatorEngine _typeSimulator;
        private readonly KeyMapper _keyMapper;
        private readonly KeyboardHookService _keyboardHook;
        private readonly LogService _logService;
        private readonly HotkeyManager _hotkeyManager;
        private readonly DispatcherTimer _settingsSaveTimer;
        private readonly string _settingsFilePath;
        private TypeSimulator.Models.Settings _settings;


        // 防止重复初始化标志
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // 初始化设置存储路径与自动保存计时器
                _settingsFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TypeSimulator",
                    "settings.xml");
                _settingsSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(450)
                };
                _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;

                // 加载设置
                _settings = TypeSimulator.Models.Settings.LoadFromFile(_settingsFilePath);
                if (_settings == null)
                {
                    _settings = new TypeSimulator.Models.Settings();
                }

                _textProcessor = new TextProcessor();
                _typeSimulator = new TypeSimulatorEngine();
                _keyboardHook = new KeyboardHookService();
                _logService = new LogService(LogTextBox);
                _keyMapper = new KeyMapper();
                _hotkeyManager = new HotkeyManager(this);

                // 注册事件
                _typeSimulator.TypingCompleted += TypeSimulator_TypingCompleted;
                _typeSimulator.CharacterTyped += TypeSimulator_CharacterTyped;
                _keyboardHook.KeyDown += KeyboardHook_KeyDown;
                // 在构造函数或初始化代码中添加
                _keyMapper.MappingCompleted += KeyMapper_MappingCompleted;

                // 初始化组件
                InitializeComponents();
                InputTextBox.TextChanged += InputTextBox_TextChanged;

                // 注册热键
                this.Loaded += (s, e) => RegisterHotkeys();

                _logService.Log("程序已启动，等待用户操作...");
                _isInitialized = true;
                _keyMapper.MappingStateChanged += KeyMapper_MappingStateChanged;
                UpdateTypingProgressDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"初始化程序失败：{ex.Message}\n\n请以管理员身份运行此程序。",
                    "初始化错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void KeyMapper_MappingStateChanged(object sender, MappingStateChangedEventArgs e)
        {
            Dispatcher.InvokeAsync(() => {
                UpdateMappingStatusDisplay();
            });
        }

        private void SettingsSaveTimer_Tick(object sender, EventArgs e)
        {
            _settingsSaveTimer.Stop();
            SaveSettingsNow();
        }

        private void QueueSettingsSave()
        {
            if (!_isInitialized || _settingsSaveTimer == null)
                return;

            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        private void SaveSettingsNow()
        {
            try
            {
                _settings?.SaveToFile(_settingsFilePath);
            }
            catch (Exception ex)
            {
                _logService?.Log($"保存设置失败：{ex.Message}", LogService.LogLevel.Warning);
            }
        }

        private static int EstimateDurationSeconds(int charCount, int speedCpm)
        {
            if (charCount <= 0)
                return 0;

            double charsPerSecond = Math.Max(1, speedCpm) / 60.0;
            return (int)Math.Ceiling(charCount / charsPerSecond);
        }

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "0秒";

            TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}小时{ts.Minutes}分";

            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}分{ts.Seconds}秒";

            return $"{ts.Seconds}秒";
        }

        private void UpdateTypingProgressDisplay()
        {
            if (TypingProgressBar == null || ProgressDetailText == null)
                return;

            if (_typeSimulator != null && _typeSimulator.IsTyping)
            {
                int total = Math.Max(_typeSimulator.TotalCharacters, 1);
                int typed = Math.Max(0, Math.Min(_typeSimulator.CharactersTyped, total));
                double percent = (typed * 100.0) / total;
                int remaining = Math.Max(0, total - typed);
                int remainingSeconds = EstimateDurationSeconds(remaining, _settings.TypingSpeed);

                TypingProgressBar.Value = percent;
                ProgressDetailText.Text = $"进度: {typed}/{total} ({percent:0}%) · 剩余约 {FormatDuration(remainingSeconds)}";
                return;
            }

            string previewText = _textProcessor.ProcessText(InputTextBox?.Text ?? string.Empty);
            int totalCharacters = previewText.Length;
            int estimatedSeconds = EstimateDurationSeconds(totalCharacters, _settings.TypingSpeed);

            TypingProgressBar.Value = 0;
            ProgressDetailText.Text = totalCharacters > 0
                ? $"待输入: {totalCharacters} 字符 · 预计耗时 {FormatDuration(estimatedSeconds)}"
                : "待输入: 0 字符";
        }

        private void InitializeComponents()
        {
            int safeTypoChance = Math.Max(1, Math.Min(90, _settings.TypoChancePercent));
            int safeDoubleHitChance = Math.Max(0, Math.Min(30, _settings.DoubleHitChancePercent));
            int safeWholeWordRewriteChance = Math.Max(0, Math.Min(30, _settings.WholeWordRewriteChancePercent));
            int safeTypoExtraPauseChance = Math.Max(0, Math.Min(100, _settings.TypoExtraPauseChancePercent));
            int safePacingStrength = Math.Max(10, Math.Min(200, _settings.PacingCurveStrengthPercent));
            int safeFatigueStrength = Math.Max(10, Math.Min(200, _settings.FatigueCurveStrengthPercent));
            _settings.TypoChancePercent = safeTypoChance;
            _settings.DoubleHitChancePercent = safeDoubleHitChance;
            _settings.WholeWordRewriteChancePercent = safeWholeWordRewriteChance;
            _settings.TypoExtraPauseChancePercent = safeTypoExtraPauseChance;
            _settings.PacingCurveStrengthPercent = safePacingStrength;
            _settings.FatigueCurveStrengthPercent = safeFatigueStrength;

            // 初始化界面控件状态
            SpeedSlider.Value = _settings.TypingSpeed;
            RandomDelayCheckBox.IsChecked = _settings.EnableRandomDelay;
            TypoSimulationCheckBox.IsChecked = _settings.EnableTypoSimulation;
            TypoChanceSlider.Value = safeTypoChance;
            TypoChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            TypoChanceValueText.Text = $"{safeTypoChance}%";
            DoubleHitChanceSlider.Value = safeDoubleHitChance;
            DoubleHitChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            DoubleHitChanceValueText.Text = $"{safeDoubleHitChance}%";
            WholeWordRewriteChanceSlider.Value = safeWholeWordRewriteChance;
            WholeWordRewriteChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            WholeWordRewriteChanceValueText.Text = $"{safeWholeWordRewriteChance}%";

            TypoExtraPauseCheckBox.IsChecked = _settings.EnableTypoExtraPause;
            TypoExtraPauseCheckBox.IsEnabled = _settings.EnableTypoSimulation;
            TypoExtraPauseChanceSlider.Value = safeTypoExtraPauseChance;
            TypoExtraPauseChanceSlider.IsEnabled = _settings.EnableTypoSimulation && _settings.EnableTypoExtraPause;
            TypoExtraPauseChanceValueText.Text = $"{safeTypoExtraPauseChance}%";

            PacingCurveCheckBox.IsChecked = _settings.EnablePacingCurve;
            PacingCurveStrengthSlider.Value = safePacingStrength;
            PacingCurveStrengthSlider.IsEnabled = _settings.EnablePacingCurve;
            PacingCurveStrengthValueText.Text = $"{safePacingStrength}%";

            FatigueCurveCheckBox.IsChecked = _settings.EnableFatigueCurve;
            FatigueCurveStrengthSlider.Value = safeFatigueStrength;
            FatigueCurveStrengthSlider.IsEnabled = _settings.EnableFatigueCurve;
            FatigueCurveStrengthValueText.Text = $"{safeFatigueStrength}%";
            EnableMappingCheckBox.IsChecked = _settings.MappingEnabled;

            // 设置初始按钮状态
            UpdateButtonStates(false);
            UpdateTypingProgressDisplay();

            _logService.Log("界面组件初始化完成");
        }

        private void UpdateButtonStates(bool isTyping)
        {
            StartTypingButton.IsEnabled = !isTyping;
            PauseTypingButton.IsEnabled = isTyping;
            StopTypingButton.IsEnabled = isTyping;
        }

        private void RegisterHotkeys()
        {
            try
            {
                _logService.Log("正在注册全局快捷键...");

                string typingShortcut = "Ctrl+Alt+T";
                string pauseMappingShortcut = "F10";  // 暂停/恢复映射
                string disableMappingShortcut = "F11"; // 禁用映射
                string resetShortcut = "F12";         // 重置应用

                // 保留打字切换快捷键
                bool typingHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.Control | ModifierKeys.Alt,
                    System.Windows.Forms.Keys.T, ToggleTyping);

                // F10键：暂停/恢复映射
                bool pauseMappingHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.None,
                    System.Windows.Forms.Keys.F10, ToggleMapping);

                // 如果不成功，尝试使用Ctrl+F10
                if (!pauseMappingHotkey)
                {
                    pauseMappingHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.Control,
                        System.Windows.Forms.Keys.F10, PauseResumeMapping);
                    if (pauseMappingHotkey)
                    {
                        pauseMappingShortcut = "Ctrl+F10";
                        _logService.Log("已使用替代快捷键: Ctrl+F10");
                    }
                }

                // F11键：完全禁用映射
                bool disableMappingHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.None,
                    System.Windows.Forms.Keys.F11, DisableMapping);

                // F12键：重置应用
                bool resetHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.None,
                    System.Windows.Forms.Keys.F12, ResetApplication);

                // 如果不成功，尝试使用Ctrl+F12
                if (!resetHotkey)
                {
                    resetHotkey = _hotkeyManager.RegisterHotKey(ModifierKeys.Control,
                        System.Windows.Forms.Keys.F12, ResetApplication);
                    if (resetHotkey)
                    {
                        resetShortcut = "Ctrl+F12";
                        _logService.Log("已使用替代快捷键: Ctrl+F12");
                    }
                }

                // 更新UI上的快捷键信息
                // 更新UI上的快捷键信息
                if (TypingShortcutText != null) TypingShortcutText.Text = $"打字功能切换: {typingShortcut}";
                if (MappingShortcutText != null) MappingShortcutText.Text = $"映射暂停/恢复: {pauseMappingShortcut}";
                if (DisableMappingShortcutText != null) DisableMappingShortcutText.Text = $"映射完全禁用: {disableMappingShortcut}";
                if (ResetShortcutText != null) ResetShortcutText.Text = $"重置应用: {resetShortcut}";

                // 同时更新弹出面板中的快捷键信息
                if (TypingShortcutInfoText != null) TypingShortcutInfoText.Text = $"打字功能切换: {typingShortcut}";
                if (MappingShortcutInfoText != null) MappingShortcutInfoText.Text = $"映射暂停/恢复: {pauseMappingShortcut}";
                if (DisableMappingShortcutInfoText != null) DisableMappingShortcutInfoText.Text = $"映射完全禁用: {disableMappingShortcut}";
                if (ResetShortcutInfoText != null) ResetShortcutInfoText.Text = $"重置应用: {resetShortcut}";

                // 显示一个通知，让用户知道哪些快捷键已经生效
                string shortcutStatus = "";
                if (!typingHotkey) shortcutStatus += "- 打字切换快捷键注册失败\n";
                if (!pauseMappingHotkey) shortcutStatus += "- 映射暂停/恢复快捷键注册失败\n";
                if (!disableMappingHotkey) shortcutStatus += "- 映射完全禁用快捷键注册失败\n";
                if (!resetHotkey) shortcutStatus += "- 重置应用快捷键注册失败\n";

                if (!string.IsNullOrEmpty(shortcutStatus))
                {
                    _logService.Log("部分快捷键注册失败", LogService.LogLevel.Warning);
                    MessageBox.Show(
                        $"以下快捷键注册失败:\n{shortcutStatus}\n请尝试查看帮助菜单了解当前可用的快捷键。",
                        "快捷键注册提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"注册快捷键失败：{ex.Message}", LogService.LogLevel.Error);
                MessageBox.Show(
                    $"无法注册全局快捷键：\n{ex.Message}\n\n某些功能可能无法正常使用。",
                    "快捷键错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        #region Event Handlers

        // 添加这两个方法到您的MainWindow类中
        private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutInfoPanel != null)
            {
                ShortcutInfoPanel.Visibility = Visibility.Visible;
            }
        }

        private void CloseShortcutInfo_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutInfoPanel != null)
            {
                ShortcutInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string clipboardText = ClipboardService.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    InputTextBox.Text = clipboardText;
                    _logService.Log($"已从剪贴板粘贴文本（{clipboardText.Length}个字符）");
                    UpdateTypingProgressDisplay();

                    // 预处理文本以备映射使用
                    if (_settings.MappingEnabled)
                    {
                        var mappableChars = _textProcessor.PrepareForKeyMapping(clipboardText);
                        _keyMapper.SetMappableCharacters(mappableChars);
                        _logService.Log($"已准备{mappableChars.Length}个字符用于按键映射");
                    }
                }
                else
                {
                    _logService.Log("剪贴板中没有文本内容", LogService.LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"粘贴失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void DisableMapping()
        {
            try
            {
                if (_settings.MappingEnabled)
                {
                    EnableMappingCheckBox.IsChecked = false;
                    // 注意：EnableMappingCheckBox_Changed事件处理器会处理后续逻辑
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"禁用映射功能失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void PauseResumeMapping()
        {
            try
            {
                _keyMapper.TogglePaused();

                // 同步钩子的暂停状态
                _keyboardHook.SetPaused(_keyMapper.IsPaused);

                // 更新界面状态
                UpdateMappingStatusDisplay();

                string status = _keyMapper.IsPaused ? "已暂停" : "已恢复";
                _logService.Log($"按键映射功能{status}");
            }
            catch (Exception ex)
            {
                _logService.Log($"切换映射暂停状态失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Clear();
            _logService.Log("已清空输入文本");

            // 清空映射字符
            _keyMapper.SetMappableCharacters(new char[0]);
            UpdateTypingProgressDisplay();
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized)
                return;

            // 映射开启时，文本变化后立即刷新映射缓存
            if (_settings.MappingEnabled)
            {
                var mappableChars = _textProcessor.PrepareForKeyMapping(InputTextBox.Text);
                _keyMapper.SetMappableCharacters(mappableChars);
            }

            UpdateTypingProgressDisplay();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedValueText != null)
            {
                int speed = (int)e.NewValue;
                SpeedValueText.Text = $"{speed} CPM";
                _settings.TypingSpeed = speed;
                UpdateTypingProgressDisplay();
                QueueSettingsSave();
            }
        }

        private void RandomDelayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _settings.EnableRandomDelay = RandomDelayCheckBox.IsChecked == true;
            QueueSettingsSave();
        }

        private void TypoSimulationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _settings.EnableTypoSimulation = TypoSimulationCheckBox.IsChecked == true;
            TypoChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            DoubleHitChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            WholeWordRewriteChanceSlider.IsEnabled = _settings.EnableTypoSimulation;
            TypoExtraPauseCheckBox.IsEnabled = _settings.EnableTypoSimulation;
            TypoExtraPauseChanceSlider.IsEnabled = _settings.EnableTypoSimulation && _settings.EnableTypoExtraPause;
            QueueSettingsSave();
        }

        private void TypoChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TypoChanceValueText != null)
            {
                int chance = Math.Max(1, Math.Min(90, (int)e.NewValue));
                TypoChanceValueText.Text = $"{chance}%";
                _settings.TypoChancePercent = chance;
                QueueSettingsSave();
            }
        }

        private void DoubleHitChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DoubleHitChanceValueText != null)
            {
                int chance = Math.Max(0, Math.Min(30, (int)e.NewValue));
                DoubleHitChanceValueText.Text = $"{chance}%";
                _settings.DoubleHitChancePercent = chance;
                QueueSettingsSave();
            }
        }

        private void WholeWordRewriteChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WholeWordRewriteChanceValueText != null)
            {
                int chance = Math.Max(0, Math.Min(30, (int)e.NewValue));
                WholeWordRewriteChanceValueText.Text = $"{chance}%";
                _settings.WholeWordRewriteChancePercent = chance;
                QueueSettingsSave();
            }
        }

        private void TypoExtraPauseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _settings.EnableTypoExtraPause = TypoExtraPauseCheckBox.IsChecked == true;
            TypoExtraPauseChanceSlider.IsEnabled = _settings.EnableTypoSimulation && _settings.EnableTypoExtraPause;
            QueueSettingsSave();
        }

        private void TypoExtraPauseChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TypoExtraPauseChanceValueText != null)
            {
                int chance = Math.Max(0, Math.Min(100, (int)e.NewValue));
                TypoExtraPauseChanceValueText.Text = $"{chance}%";
                _settings.TypoExtraPauseChancePercent = chance;
                QueueSettingsSave();
            }
        }

        private void PacingCurveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _settings.EnablePacingCurve = PacingCurveCheckBox.IsChecked == true;
            PacingCurveStrengthSlider.IsEnabled = _settings.EnablePacingCurve;
            QueueSettingsSave();
        }

        private void PacingCurveStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PacingCurveStrengthValueText != null)
            {
                int strength = Math.Max(10, Math.Min(200, (int)e.NewValue));
                PacingCurveStrengthValueText.Text = $"{strength}%";
                _settings.PacingCurveStrengthPercent = strength;
                QueueSettingsSave();
            }
        }

        private void FatigueCurveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _settings.EnableFatigueCurve = FatigueCurveCheckBox.IsChecked == true;
            FatigueCurveStrengthSlider.IsEnabled = _settings.EnableFatigueCurve;
            QueueSettingsSave();
        }

        private void FatigueCurveStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FatigueCurveStrengthValueText != null)
            {
                int strength = Math.Max(10, Math.Min(200, (int)e.NewValue));
                FatigueCurveStrengthValueText.Text = $"{strength}%";
                _settings.FatigueCurveStrengthPercent = strength;
                QueueSettingsSave();
            }
        }

        private void StartTypingButton_Click(object sender, RoutedEventArgs e)
        {
            StartTyping();
        }

        private void PauseTypingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_typeSimulator.IsPaused)
            {
                ResumeTyping();
            }
            else
            {
                PauseTyping();
            }
        }

        private void StopTypingButton_Click(object sender, RoutedEventArgs e)
        {
            StopTyping();
        }

        private void EnableMappingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _settings.MappingEnabled = EnableMappingCheckBox.IsChecked == true;

            try
            {
                // 当启用映射功能时，准备可映射字符
                if (_settings.MappingEnabled && !string.IsNullOrEmpty(InputTextBox.Text))
                {
                    var mappableChars = _textProcessor.PrepareForKeyMapping(InputTextBox.Text);
                    _keyMapper.SetMappableCharacters(mappableChars);
                    _logService.Log($"已准备{mappableChars.Length}个字符用于按键映射");

                    // 启用映射时阻止原始按键传递
                    _keyboardHook.SetBlockOriginalKeypress(true);
                    _keyboardHook.SetPaused(false);  // 确保钩子未暂停
                }
                else
                {
                    // 禁用映射时取消阻止原始按键
                    _keyboardHook.SetBlockOriginalKeypress(false);
                    _keyboardHook.SetPaused(false);  // 重置暂停状态
                }

                _keyMapper.SetEnabled(_settings.MappingEnabled);

                string status = _settings.MappingEnabled ? "已启用" : "已禁用";
                _logService.Log($"按键映射功能{status}");
                KeyPressStatusText.Text = $"按键状态: {(_settings.MappingEnabled ? "映射中" : "监听中")}";
                QueueSettingsSave();
            }
            catch (Exception ex)
            {
                _logService.Log($"设置按键映射失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void TypeSimulator_TypingCompleted(object sender, EventArgs e)
        {
            // 确保在UI线程上执行
            Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = "就绪";
                AppStatusText.Text = "打字任务已完成";
                UpdateButtonStates(false);
                TypingProgressBar.Value = 100;
                ProgressDetailText.Text = $"进度: {_typeSimulator.TotalCharacters}/{_typeSimulator.TotalCharacters} (100%) · 已完成";
                _logService.Log("模拟打字已完成");
            }, DispatcherPriority.Normal);
        }

        private void TypeSimulator_CharacterTyped(object sender, char character)
        {
            // 确保在UI线程上执行
            Dispatcher.InvokeAsync(() =>
            {
                _logService.UpdateLastLine($"正在输入: 已输出 {_typeSimulator.CharactersTyped}/{_typeSimulator.TotalCharacters} 个字符");
                UpdateTypingProgressDisplay();
            }, DispatcherPriority.Background);
        }

        private void KeyboardHook_KeyDown(object sender, TypeSimulator.Services.KeyboardHookEventArgs e)
        {
            // 键盘钩子可能在非UI线程触发
            if (_settings.MappingEnabled && !_typeSimulator.IsTyping)
            {
                try
                {
                    // 检查是否有修饰键被按下
                    if (e.HasModifiers)
                    {
                        // 有修饰键被按下，不执行映射
                        Dispatcher.InvokeAsync(() =>
                        {
                            _logService.Log($"检测到修饰键 ({e.ModifierKeys})，不执行按键映射");
                        }, DispatcherPriority.Background);
                        return;
                    }

                    char? mappedCharResult = _keyMapper.MapKey(e.Key);
                    if (mappedCharResult.HasValue)
                    {
                        char mappedChar = mappedCharResult.Value;
                        KeyboardSimulator.TypeCharacter(mappedChar);

                        // 确保在UI线程更新日志
                        Dispatcher.InvokeAsync(() =>
                        {
                            _logService.Log($"映射按键：{e.Key} -> '{mappedChar}'");
                        }, DispatcherPriority.Background);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        _logService.Log($"按键映射错误：{ex.Message}", LogService.LogLevel.Error);
                    }, DispatcherPriority.Background);
                }
            }
        }

        // 添加这个新方法
        private void KeyMapper_MappingCompleted(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _logService.Log("所有字符已映射完成，自动禁用映射功能");
                // 直接设置 CheckBox 状态，这会自动触发 EnableMappingCheckBox_Changed
                if (EnableMappingCheckBox.IsChecked == true)
                {
                    EnableMappingCheckBox.IsChecked = false;
                }
            });
        }

        #endregion

        #region Core Application Methods

        private void StartTyping()
        {
            try
            {
                if (string.IsNullOrEmpty(InputTextBox.Text))
                {
                    MessageBox.Show("请先输入或粘贴要模拟打字的文本", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string text = _textProcessor.ProcessText(InputTextBox.Text);
                if (string.IsNullOrEmpty(text))
                {
                    _logService.Log("处理后的文本为空，无法开始打字", LogService.LogLevel.Warning);
                    return;
                }

                _typeSimulator.Configure(
                    text,
                    _settings.TypingSpeed,
                    _settings.EnableRandomDelay,
                    _settings.EnableTypoSimulation,
                    _settings.TypoChancePercent,
                    _settings.DoubleHitChancePercent,
                    _settings.WholeWordRewriteChancePercent,
                    _settings.EnableTypoExtraPause,
                    _settings.TypoExtraPauseChancePercent,
                    _settings.EnablePacingCurve,
                    _settings.PacingCurveStrengthPercent,
                    _settings.EnableFatigueCurve,
                    _settings.FatigueCurveStrengthPercent
                );

                _typeSimulator.Start();

                UpdateButtonStates(true);
                StatusText.Text = "打字中...";
                AppStatusText.Text = "打字任务运行中";
                PauseTypingButton.Content = "暂停";
                UpdateTypingProgressDisplay();

                _logService.Log(
                    $"开始模拟打字，速度：{_settings.TypingSpeed} CPM，随机延迟：{(_settings.EnableRandomDelay ? "开" : "关")}，" +
                    $"错字模拟：{(_settings.EnableTypoSimulation ? $"开(错字{_settings.TypoChancePercent}%/双击{_settings.DoubleHitChancePercent}%/整词{_settings.WholeWordRewriteChancePercent}%)" : "关")}，" +
                    $"错字后额外停顿：{(_settings.EnableTypoExtraPause ? $"开({_settings.TypoExtraPauseChancePercent}%)" : "关")}，" +
                    $"节奏曲线：{(_settings.EnablePacingCurve ? $"开(强度{_settings.PacingCurveStrengthPercent}%)" : "关")}，" +
                    $"疲劳曲线：{(_settings.EnableFatigueCurve ? $"开(强度{_settings.FatigueCurveStrengthPercent}%)" : "关")}，" +
                    $"共 {text.Length} 个字符");
            }
            catch (Exception ex)
            {
                _logService.Log($"开始模拟打字失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void PauseTyping()
        {
            try
            {
                _typeSimulator.Pause();
                PauseTypingButton.Content = "继续";
                StatusText.Text = "已暂停";
                AppStatusText.Text = "打字任务已暂停";
                UpdateTypingProgressDisplay();
                _logService.Log("模拟打字已暂停");
            }
            catch (Exception ex)
            {
                _logService.Log($"暂停模拟打字失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void ResumeTyping()
        {
            try
            {
                _typeSimulator.Resume();
                PauseTypingButton.Content = "暂停";
                StatusText.Text = "打字中...";
                AppStatusText.Text = "打字任务运行中";
                UpdateTypingProgressDisplay();
                _logService.Log("继续模拟打字");
            }
            catch (Exception ex)
            {
                _logService.Log($"继续模拟打字失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void StopTyping()
        {
            try
            {
                _typeSimulator.Stop();
                UpdateButtonStates(false);
                PauseTypingButton.Content = "暂停";
                StatusText.Text = "就绪";
                AppStatusText.Text = "打字任务已停止";
                UpdateTypingProgressDisplay();
                _logService.Log("已停止模拟打字");
            }
            catch (Exception ex)
            {
                _logService.Log($"停止模拟打字失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void ToggleTyping()
        {
            try
            {
                if (_typeSimulator.IsTyping)
                {
                    if (_typeSimulator.IsPaused)
                        ResumeTyping();
                    else
                        PauseTyping();
                }
                else
                {
                    StartTyping();
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"切换打字状态失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        private void ToggleMapping()
        {
            if (!_settings.MappingEnabled)
            {
                // 如果当前是禁用状态，切换为启用
                EnableMappingCheckBox.IsChecked = true;
            }
            else if (!_keyMapper.IsPaused)
            {
                // 如果当前是启用且未暂停，则暂停
                _keyMapper.TogglePaused();
                _keyboardHook.SetPaused(true);  // 同步钩子暂停状态
                UpdateMappingStatusDisplay();
                _logService.Log("按键映射功能已暂停");
            }
            else
            {
                // 如果当前是暂停状态，则恢复
                _keyMapper.TogglePaused();
                _keyboardHook.SetPaused(false);  // 同步钩子暂停状态
                UpdateMappingStatusDisplay();
                _logService.Log("按键映射功能已恢复");
            }
        }

        private void UpdateMappingStatusDisplay()
        {
            string status = "监听中";
            if (_settings.MappingEnabled)
            {
                status = _keyMapper.IsPaused ? "映射已暂停" : "映射中";
            }

            // 在UI线程更新界面
            Dispatcher.InvokeAsync(() => {
                KeyPressStatusText.Text = $"按键状态: {status}";
            });
        }


        private void ResetApplication()
        {
            try
            {
                if (_typeSimulator.IsTyping)
                {
                    StopTyping();
                }

                // 重置按键映射
                _keyMapper.SetEnabled(false);
                EnableMappingCheckBox.IsChecked = false;

                _logService.Log("已重置程序状态");
                StatusText.Text = "就绪";
                AppStatusText.Text = "程序已准备就绪";
                UpdateTypingProgressDisplay();
            }
            catch (Exception ex)
            {
                _logService.Log($"重置程序失败：{ex.Message}", LogService.LogLevel.Error);
            }
        }

        #endregion

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                // 确保停止所有活动
                if (_typeSimulator != null && _typeSimulator.IsTyping)
                {
                    _typeSimulator.Stop();
                }

                // 显示关闭确认
                var result = MessageBox.Show(
                    "确定要退出程序吗？",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _settingsSaveTimer?.Stop();
                SaveSettingsNow();
                _logService?.Log("程序正在关闭...");
            }
            catch
            {
                // 忽略关闭时的异常
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 释放资源
                _keyboardHook?.Dispose();
                _hotkeyManager?.Dispose();
                _typeSimulator?.Stop();
                _settingsSaveTimer?.Stop();
            }
            catch
            {
                // 忽略关闭时的异常
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
