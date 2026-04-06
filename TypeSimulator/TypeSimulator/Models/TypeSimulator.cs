using System;
using System.Collections.Generic;
using System.Windows.Threading;
using TypeSimulator.Utilities;

namespace TypeSimulator.Models
{
    /// <summary>
    /// 模拟人类打字的引擎
    /// </summary>
    public class TypeSimulatorEngine
    {
        // 打字参数
        private string _textToType;
        private int _typingSpeed; // 字符/分钟
        private bool _enableRandomDelay;
        private bool _enableTypoSimulation;
        private int _typoChancePercent;
        private int _doubleHitChancePercent;
        private int _wholeWordRewriteChancePercent;
        private bool _enableTypoExtraPause;
        private int _typoExtraPauseChancePercent;
        private bool _enablePunctuationDazePause;
        private int _punctuationDazeDelayMs;
        private bool _enablePacingCurve;
        private int _pacingCurveStrengthPercent;
        private bool _enableFatigueCurve;
        private int _fatigueCurveStrengthPercent;

        // 状态追踪
        private int _currentPosition;
        private readonly DispatcherTimer _typingTimer;
        private readonly Random _random;
        private readonly object _lockObject = new object();
        private readonly Queue<TypingAction> _pendingActions;

        private const int MinTypoChancePercent = 1;
        private const int MaxTypoChancePercent = 90;
        private const int DoubleHitChancePercent = 4;
        private const int WholeWordRewriteChancePercent = 5;
        private const int MaxWholeWordRewriteLength = 14;
        private const int MinDoubleHitChancePercent = 0;
        private const int MaxDoubleHitChancePercent = 30;
        private const int MinWholeWordRewriteChancePercent = 0;
        private const int MaxWholeWordRewriteChancePercent = 30;
        private const int MinTypoExtraPauseChancePercent = 0;
        private const int MaxTypoExtraPauseChancePercent = 100;
        private const int DefaultTypoExtraPauseChancePercent = 100;
        private const int MinPunctuationDazeDelayMs = 100;
        private const int MaxPunctuationDazeDelayMs = 3000;
        private const int DefaultPunctuationDazeDelayMs = 900;
        private const int MinCurveStrengthPercent = 10;
        private const int MaxCurveStrengthPercent = 200;
        private const double NeighborDistanceThreshold = 1.5;
        private static readonly Dictionary<char, char[]> NeighborKeyMap = BuildNeighborKeyMap();
        private static readonly Dictionary<char, char> ShiftSymbolBaseMap = new Dictionary<char, char>
        {
            ['!'] = '1', ['@'] = '2', ['#'] = '3', ['$'] = '4', ['%'] = '5',
            ['^'] = '6', ['&'] = '7', ['*'] = '8', ['('] = '9', [')'] = '0',
            ['_'] = '-', ['+'] = '=', ['{'] = '[', ['}'] = ']', ['|'] = '\\',
            [':'] = ';', ['"'] = '\'', ['<'] = ',', ['>'] = '.', ['?'] = '/'
        };

        private enum TypingActionKind
        {
            TypeCharacter,
            Backspace
        }

        private sealed class TypingAction
        {
            public TypingActionKind Kind { get; set; }
            public char Character { get; set; }
            public bool CommitCharacter { get; set; }
            public bool IsTypoMistake { get; set; }
        }

        // 状态属性
        public bool IsTyping { get; private set; }
        public bool IsPaused { get; private set; }
        public int CharactersTyped => _currentPosition;
        public int TotalCharacters => _textToType?.Length ?? 0;

        // 事件
        public event EventHandler<char> CharacterTyped;
        public event EventHandler TypingCompleted;

        public TypeSimulatorEngine()
        {
            _typingTimer = new DispatcherTimer();
            _typingTimer.Tick += TypeNextCharacter;
            _random = new Random();
            _pendingActions = new Queue<TypingAction>();
            _textToType = string.Empty;

            IsTyping = false;
            IsPaused = false;
            _enableTypoSimulation = false;
            _typoChancePercent = 3;
            _doubleHitChancePercent = DoubleHitChancePercent;
            _wholeWordRewriteChancePercent = WholeWordRewriteChancePercent;
            _enableTypoExtraPause = true;
            _typoExtraPauseChancePercent = DefaultTypoExtraPauseChancePercent;
            _enablePunctuationDazePause = false;
            _punctuationDazeDelayMs = DefaultPunctuationDazeDelayMs;
            _enablePacingCurve = true;
            _pacingCurveStrengthPercent = 100;
            _enableFatigueCurve = true;
            _fatigueCurveStrengthPercent = 100;
        }

        /// <summary>
        /// 配置模拟打字参数
        /// </summary>
        public void Configure(string text, int speed, bool enableRandomDelay,
            bool enableTypoSimulation = false, int typoChancePercent = 3,
            int doubleHitChancePercent = DoubleHitChancePercent, int wholeWordRewriteChancePercent = WholeWordRewriteChancePercent,
            bool enableTypoExtraPause = true, int typoExtraPauseChancePercent = DefaultTypoExtraPauseChancePercent,
            bool enablePacingCurve = true, int pacingCurveStrengthPercent = 100,
            bool enableFatigueCurve = true, int fatigueCurveStrengthPercent = 100,
            bool enablePunctuationDazePause = false, int punctuationDazeDelayMs = DefaultPunctuationDazeDelayMs)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "打字速度必须大于0");

            lock (_lockObject)
            {
                _textToType = text;
                _typingSpeed = speed;
                _enableRandomDelay = enableRandomDelay;
                _enableTypoSimulation = enableTypoSimulation;
                _typoChancePercent = Math.Max(MinTypoChancePercent, Math.Min(MaxTypoChancePercent, typoChancePercent));
                _doubleHitChancePercent = Math.Max(MinDoubleHitChancePercent, Math.Min(MaxDoubleHitChancePercent, doubleHitChancePercent));
                _wholeWordRewriteChancePercent = Math.Max(MinWholeWordRewriteChancePercent, Math.Min(MaxWholeWordRewriteChancePercent, wholeWordRewriteChancePercent));
                _enableTypoExtraPause = enableTypoExtraPause;
                _typoExtraPauseChancePercent = Math.Max(MinTypoExtraPauseChancePercent, Math.Min(MaxTypoExtraPauseChancePercent, typoExtraPauseChancePercent));
                _enablePunctuationDazePause = enablePunctuationDazePause;
                _punctuationDazeDelayMs = Math.Max(MinPunctuationDazeDelayMs, Math.Min(MaxPunctuationDazeDelayMs, punctuationDazeDelayMs));
                _enablePacingCurve = enablePacingCurve;
                _pacingCurveStrengthPercent = Math.Max(MinCurveStrengthPercent, Math.Min(MaxCurveStrengthPercent, pacingCurveStrengthPercent));
                _enableFatigueCurve = enableFatigueCurve;
                _fatigueCurveStrengthPercent = Math.Max(MinCurveStrengthPercent, Math.Min(MaxCurveStrengthPercent, fatigueCurveStrengthPercent));
                _currentPosition = 0;
                _pendingActions.Clear();

                // 计算打字速度的基本间隔（毫秒）
                int intervalMs = CalculateBaseIntervalForNextAction();
                _typingTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            }
        }

        /// <summary>
        /// 开始模拟打字
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(_textToType))
                    throw new InvalidOperationException("没有可打字的文本");

                if (IsTyping && !IsPaused)
                    return; // 已经在运行中

                IsTyping = true;
                IsPaused = false;
                _currentPosition = 0;
                _pendingActions.Clear();

                _typingTimer.Start();
            }
        }

        /// <summary>
        /// 暂停模拟打字
        /// </summary>
        public void Pause()
        {
            lock (_lockObject)
            {
                if (IsTyping && !IsPaused)
                {
                    _typingTimer.Stop();
                    IsPaused = true;
                }
            }
        }

        /// <summary>
        /// 继续模拟打字
        /// </summary>
        public void Resume()
        {
            lock (_lockObject)
            {
                if (IsTyping && IsPaused)
                {
                    _typingTimer.Start();
                    IsPaused = false;
                }
            }
        }

        /// <summary>
        /// 停止模拟打字
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                _typingTimer.Stop();
                IsTyping = false;
                IsPaused = false;
                _pendingActions.Clear();
            }
        }

        /// <summary>
        /// 定时器回调，输入下一个动作
        /// </summary>
        private void TypeNextCharacter(object sender, EventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_pendingActions.Count == 0)
                    {
                        if (_currentPosition >= _textToType.Length)
                        {
                            _typingTimer.Stop();
                            IsTyping = false;
                            IsPaused = false;
                            OnTypingCompleted();
                            return;
                        }

                        char targetChar = _textToType[_currentPosition];
                        EnqueueActionsForCharacter(targetChar);
                    }

                    if (_pendingActions.Count == 0)
                        return;

                    TypingAction action = _pendingActions.Dequeue();

                    if (action.Kind == TypingActionKind.TypeCharacter)
                    {
                        KeyboardSimulator.TypeCharacter(action.Character);
                    }
                    else if (action.Kind == TypingActionKind.Backspace)
                    {
                        KeyboardSimulator.PressBackspace();
                    }

                    if (action.CommitCharacter)
                    {
                        _currentPosition++;
                        OnCharacterTyped(action.Character);
                    }

                    ApplyNextInterval(action);
                }
            }
            catch (Exception ex)
            {
                // 记录错误并尝试继续
                Console.WriteLine($"输入字符时出错: {ex.Message}");

                // 如果发生错误，可能需要暂停或停止
                try
                {
                    Pause(); // 出错时暂停打字
                }
                catch
                {
                    // 忽略暂停时的错误
                }
            }
        }

        private void EnqueueActionsForCharacter(char targetChar)
        {
            int currentIndex = _currentPosition;
            bool willRewriteWholeWord = ShouldSimulateWholeWordRewrite(currentIndex, targetChar);
            bool typoAdded = false;

            if (ShouldSimulateTypo(targetChar))
            {
                typoAdded = true;
                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.TypeCharacter,
                    Character = GenerateWrongCharacter(targetChar),
                    CommitCharacter = false,
                    IsTypoMistake = true
                });

                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.Backspace,
                    Character = '\0',
                    CommitCharacter = false,
                    IsTypoMistake = false
                });
            }

            _pendingActions.Enqueue(new TypingAction
            {
                Kind = TypingActionKind.TypeCharacter,
                Character = targetChar,
                CommitCharacter = true,
                IsTypoMistake = false
            });

            if (!typoAdded && !willRewriteWholeWord && ShouldSimulateDoubleHit(targetChar))
            {
                // 偶发双击：重复敲一次，再退格修正
                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.TypeCharacter,
                    Character = targetChar,
                    CommitCharacter = false,
                    IsTypoMistake = false
                });

                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.Backspace,
                    Character = '\0',
                    CommitCharacter = false,
                    IsTypoMistake = false
                });
            }

            if (willRewriteWholeWord)
            {
                EnqueueWholeWordRewriteActions(currentIndex);
            }
        }

        private bool ShouldSimulateTypo(char targetChar)
        {
            if (!_enableTypoSimulation)
                return false;

            if (_typoChancePercent <= 0)
                return false;

            if (char.IsWhiteSpace(targetChar) || char.IsControl(targetChar))
                return false;

            int effectiveChance = GetEffectiveChancePercent(_typoChancePercent, 95);
            return _random.Next(100) < effectiveChance;
        }

        private bool ShouldSimulateDoubleHit(char targetChar)
        {
            if (!_enableTypoSimulation)
                return false;

            if (!IsWordCharacter(targetChar))
                return false;

            int effectiveChance = GetEffectiveChancePercent(_doubleHitChancePercent, 45);
            return _random.Next(100) < effectiveChance;
        }

        private bool ShouldSimulateWholeWordRewrite(int currentIndex, char targetChar)
        {
            if (!_enableTypoSimulation)
                return false;

            if (!IsWordCharacter(targetChar))
                return false;

            char? nextChar = GetNextOriginalChar(currentIndex);
            if (nextChar.HasValue && IsWordCharacter(nextChar.Value))
                return false;

            if (!TryGetWordEndingAt(currentIndex, out string word))
                return false;

            if (word.Length < 2 || word.Length > MaxWholeWordRewriteLength)
                return false;

            int effectiveChance = GetEffectiveChancePercent(_wholeWordRewriteChancePercent, 40);
            return _random.Next(100) < effectiveChance;
        }

        private void EnqueueWholeWordRewriteActions(int currentIndex)
        {
            if (!TryGetWordEndingAt(currentIndex, out string word))
                return;

            // 偶发整词删改：逐字符删除后重打，兼容性更高
            for (int i = 0; i < word.Length; i++)
            {
                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.Backspace,
                    Character = '\0',
                    CommitCharacter = false,
                    IsTypoMistake = false
                });
            }

            for (int i = 0; i < word.Length; i++)
            {
                _pendingActions.Enqueue(new TypingAction
                {
                    Kind = TypingActionKind.TypeCharacter,
                    Character = word[i],
                    CommitCharacter = false,
                    IsTypoMistake = false
                });
            }
        }

        private bool TryGetWordEndingAt(int index, out string word)
        {
            word = string.Empty;

            if (index < 0 || index >= _textToType.Length)
                return false;

            if (!IsWordCharacter(_textToType[index]))
                return false;

            int start = index;
            while (start > 0 && IsWordCharacter(_textToType[start - 1]))
            {
                start--;
            }

            int length = index - start + 1;
            if (length <= 0)
                return false;

            word = _textToType.Substring(start, length);
            return true;
        }

        private char? GetNextOriginalChar(int currentIndex)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex >= 0 && nextIndex < _textToType.Length)
                return _textToType[nextIndex];

            return null;
        }

        private static bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '\'';
        }

        private char GenerateWrongCharacter(char targetChar)
        {
            const string letters = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string symbols = "[]{}()<>?!@#$%^&*-_=+;:,./\\|";
            char lookupChar = char.ToLowerInvariant(targetChar);

            if (ShiftSymbolBaseMap.TryGetValue(lookupChar, out char shiftedBaseChar))
            {
                lookupChar = shiftedBaseChar;
            }

            if (NeighborKeyMap.TryGetValue(lookupChar, out char[] neighbors) && neighbors.Length > 0)
            {
                char neighborChar = neighbors[_random.Next(neighbors.Length)];
                if (char.IsUpper(targetChar))
                {
                    neighborChar = char.ToUpperInvariant(neighborChar);
                }

                if (neighborChar != targetChar)
                {
                    return neighborChar;
                }
            }

            if (char.IsLetter(targetChar))
            {
                char wrongChar = letters[_random.Next(letters.Length)];
                if (char.IsUpper(targetChar))
                    wrongChar = char.ToUpperInvariant(wrongChar);

                if (wrongChar == targetChar)
                    wrongChar = char.IsUpper(targetChar) ? 'X' : 'x';

                return wrongChar;
            }

            if (char.IsDigit(targetChar))
            {
                char wrongChar = digits[_random.Next(digits.Length)];
                if (wrongChar == targetChar)
                    wrongChar = targetChar == '0' ? '1' : '0';

                return wrongChar;
            }

            char symbol = symbols[_random.Next(symbols.Length)];
            return symbol == targetChar ? 'x' : symbol;
        }

        private void ApplyNextInterval(TypingAction action)
        {
            int baseInterval = CalculateBaseIntervalForNextAction();
            int nextDelay;

            if (!_enableRandomDelay)
            {
                nextDelay = baseInterval;
            }
            else if (action.Kind == TypingActionKind.Backspace)
            {
                // 退格动作通常比正常打字稍快
                nextDelay = _random.Next(45, 120);
            }
            else
            {
                nextDelay = RandomDelay.CalculateSpecialCharDelay(action.Character, baseInterval);
            }

            if (action.Kind == TypingActionKind.TypeCharacter && action.IsTypoMistake && ShouldApplyTypoExtraPause())
            {
                // 打错后可配置的额外犹豫停顿
                nextDelay += _random.Next(500, 1001);
            }

            nextDelay += CalculateContextPause(action);
            nextDelay += CalculatePunctuationDazePause(action);

            _typingTimer.Interval = TimeSpan.FromMilliseconds(nextDelay);
        }

        private int CalculateContextPause(TypingAction action)
        {
            if (action.Kind != TypingActionKind.TypeCharacter || !action.CommitCharacter)
                return 0;

            int extraDelay = 0;
            char currentChar = action.Character;
            char? previousChar = GetPreviousCommittedCharacter();
            char? nextChar = GetNextCharacterToType();
            double fatigueFactor = GetFatigueFactor();

            if (currentChar == '\n' || currentChar == '\r')
            {
                // 换行通常有明显停顿
                extraDelay += _random.Next(300, 950);
                return ApplyFatigueToPause(extraDelay, fatigueFactor);
            }

            if (IsChineseSentencePunctuation(currentChar))
            {
                // 中文句末标点通常停顿更明显
                extraDelay += _random.Next(320, 900);
            }
            else if (IsSentencePunctuation(currentChar))
            {
                extraDelay += _random.Next(220, 680);
            }
            else if (currentChar == '；' || currentChar == '：')
            {
                extraDelay += _random.Next(220, 520);
            }
            else if (currentChar == ';' || currentChar == ':')
            {
                extraDelay += _random.Next(140, 380);
            }
            else if (currentChar == '，' || currentChar == '、')
            {
                extraDelay += _random.Next(140, 330);
            }
            else if (currentChar == ',')
            {
                extraDelay += _random.Next(90, 260);
            }

            if (currentChar == ' ')
            {
                if (previousChar.HasValue && IsSentencePunctuation(previousChar.Value))
                {
                    // 句号后的空格更容易出现明显停顿
                    extraDelay += _random.Next(260, 780);
                }
                else if (previousChar.HasValue && char.IsLetterOrDigit(previousChar.Value) && _random.Next(100) < 35)
                {
                    // 普通词间停顿
                    extraDelay += _random.Next(45, 170);
                }

                return ApplyFatigueToPause(extraDelay, fatigueFactor);
            }

            if (char.IsLetterOrDigit(currentChar) && nextChar.HasValue && IsWordBoundary(nextChar.Value) && _random.Next(100) < 45)
            {
                // 词尾偶发停顿
                extraDelay += _random.Next(60, 220);
            }

            if (_enableFatigueCurve && IsLongTextForFatigue() && GetTypingProgress() > 0.55 && _random.Next(100) < 14)
            {
                // 长文本后半段出现轻微疲劳停顿
                extraDelay += _random.Next(35, 130);
            }

            return ApplyFatigueToPause(extraDelay, fatigueFactor);
        }

        private char? GetPreviousCommittedCharacter()
        {
            int index = _currentPosition - 2;
            if (index >= 0 && index < _textToType.Length)
                return _textToType[index];

            return null;
        }

        private char? GetNextCharacterToType()
        {
            if (_currentPosition >= 0 && _currentPosition < _textToType.Length)
                return _textToType[_currentPosition];

            return null;
        }

        private static bool IsSentencePunctuation(char c)
        {
            return c == '.' || c == '!' || c == '?';
        }

        private int CalculatePunctuationDazePause(TypingAction action)
        {
            if (!_enablePunctuationDazePause)
                return 0;

            if (action.Kind != TypingActionKind.TypeCharacter || !action.CommitCharacter)
                return 0;

            if (!IsDazePunctuation(action.Character))
                return 0;

            int maxPause = Math.Max(MinPunctuationDazeDelayMs, Math.Min(MaxPunctuationDazeDelayMs, _punctuationDazeDelayMs));
            return _random.Next(0, maxPause + 1);
        }

        private static bool IsChineseSentencePunctuation(char c)
        {
            return c == '。' || c == '！' || c == '？';
        }

        private static bool IsDazePunctuation(char c)
        {
            return c == '.' || c == ',' || c == '。' || c == '，';
        }

        private static bool IsWordBoundary(char c)
        {
            return char.IsWhiteSpace(c) || c == ',' || c == '.' || c == '!' || c == '?' || c == ';' || c == ':' ||
                   c == '，' || c == '。' || c == '！' || c == '？' || c == '；' || c == '：' || c == '、' ||
                   c == '\n' || c == '\r';
        }

        private int ApplyFatigueToPause(int extraDelay, double fatigueFactor)
        {
            if (extraDelay <= 0)
                return 0;

            return (int)(extraDelay * Math.Max(1.0, fatigueFactor));
        }

        private bool ShouldApplyTypoExtraPause()
        {
            if (!_enableTypoExtraPause || !_enableTypoSimulation)
                return false;

            return _random.Next(100) < _typoExtraPauseChancePercent;
        }

        private int GetEffectiveChancePercent(int baseChance, int maxCap)
        {
            int chance = baseChance;
            double fatigueFactor = GetFatigueFactor();
            if (fatigueFactor > 1.0)
            {
                chance = (int)Math.Round(baseChance * fatigueFactor);
            }

            return Math.Max(0, Math.Min(maxCap, chance));
        }

        private bool IsLongTextForFatigue()
        {
            return _textToType != null && _textToType.Length >= 120;
        }

        private double GetTypingProgress()
        {
            return (double)_currentPosition / Math.Max(_textToType.Length, 1);
        }

        private double GetFatigueFactor()
        {
            if (!_enableFatigueCurve || !IsLongTextForFatigue())
                return 1.0;

            double progress = GetTypingProgress();
            if (progress <= 0.45)
                return 1.0;

            double baseFactor;
            if (progress >= 1.0)
            {
                baseFactor = 1.35;
            }
            else
            {
                double normalized = (progress - 0.45) / 0.55;
                baseFactor = 1.0 + (normalized * 0.35);
            }

            double strength = _fatigueCurveStrengthPercent / 100.0;
            return 1.0 + ((baseFactor - 1.0) * strength);
        }

        private int CalculateBaseIntervalForNextAction()
        {
            return ApplyPacingCurve(CalculateTypingInterval(_typingSpeed));
        }

        private int ApplyPacingCurve(int intervalMs)
        {
            if (!_enablePacingCurve || _textToType == null || _textToType.Length < 12)
                return intervalMs;

            double progress = (double)_currentPosition / Math.Max(_textToType.Length, 1);
            double multiplier;

            if (progress < 0.2)
            {
                // 开头更慢
                multiplier = 1.4 - (0.4 * (progress / 0.2));
            }
            else if (progress < 0.8)
            {
                // 中段更快
                multiplier = 0.85;
            }
            else
            {
                // 结尾再次放慢
                multiplier = 1.0 + (0.5 * ((progress - 0.8) / 0.2));
            }

            double strength = _pacingCurveStrengthPercent / 100.0;
            double adjustedMultiplier = 1.0 + ((multiplier - 1.0) * strength);

            return Math.Max(10, (int)(intervalMs * adjustedMultiplier));
        }

        private static Dictionary<char, char[]> BuildNeighborKeyMap()
        {
            var keyboardRows = new[]
            {
                Tuple.Create("1234567890-=", 0.0),
                Tuple.Create("qwertyuiop[]\\", 0.5),
                Tuple.Create("asdfghjkl;'", 0.85),
                Tuple.Create("zxcvbnm,./", 1.2)
            };

            var keys = new List<Tuple<char, double, int>>();
            for (int rowIndex = 0; rowIndex < keyboardRows.Length; rowIndex++)
            {
                string rowKeys = keyboardRows[rowIndex].Item1;
                double offset = keyboardRows[rowIndex].Item2;

                for (int col = 0; col < rowKeys.Length; col++)
                {
                    keys.Add(Tuple.Create(char.ToLowerInvariant(rowKeys[col]), col + offset, rowIndex));
                }
            }

            var map = new Dictionary<char, List<char>>();
            for (int i = 0; i < keys.Count; i++)
            {
                char source = keys[i].Item1;
                double sourceX = keys[i].Item2;
                int sourceY = keys[i].Item3;

                if (!map.ContainsKey(source))
                    map[source] = new List<char>();

                for (int j = 0; j < keys.Count; j++)
                {
                    if (i == j)
                        continue;

                    char candidate = keys[j].Item1;
                    double dx = sourceX - keys[j].Item2;
                    double dy = sourceY - keys[j].Item3;
                    double distance = Math.Sqrt((dx * dx) + (dy * dy));

                    if (distance <= NeighborDistanceThreshold)
                    {
                        map[source].Add(candidate);
                    }
                }
            }

            var result = new Dictionary<char, char[]>();
            foreach (KeyValuePair<char, List<char>> item in map)
            {
                result[item.Key] = item.Value.ToArray();
            }

            return result;
        }

        /// <summary>
        /// 根据打字速度计算基本间隔时间
        /// </summary>
        private int CalculateTypingInterval(int charactersPerMinute)
        {
            // 将字符/分钟转换为毫秒/字符，确保不会除以零
            return 60000 / Math.Max(charactersPerMinute, 1);
        }

        protected virtual void OnCharacterTyped(char character)
        {
            try
            {
                CharacterTyped?.Invoke(this, character);
            }
            catch (Exception ex)
            {
                // 防止事件处理程序中的异常导致整个操作失败
                Console.WriteLine($"字符输入事件处理出错: {ex.Message}");
            }
        }

        protected virtual void OnTypingCompleted()
        {
            try
            {
                TypingCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // 防止事件处理程序中的异常导致整个操作失败
                Console.WriteLine($"打字完成事件处理出错: {ex.Message}");
            }
        }
    }
}
