using System.Collections.ObjectModel;
using Battleship.Core.AI;
using Battleship.Core.Logic;
using Battleship.Core.Models;

namespace Project
{
    public partial class GamePage : ContentPage
    {
        private const int Cols = 10;
        private const int GameRows = 10;
        private const int ExtraRowsTop = 1;
        private const int ExtraRowsBottom = 7;
        private const int TotalRows = GameRows + ExtraRowsTop + ExtraRowsBottom;
        private const int SubStorageRows = 4;

        private double _cellSize;
        private double _boardStartY;
        private double _gameAreaTop;
        private double _gameAreaBottom;
        private double _boardWidth;
        private bool _boardsBuilt = false;

        private readonly Dictionary<View, Point> _playerOrigPos = new();
        private readonly Dictionary<View, Point> _lastTotal = new();

        private readonly Game _game;
        private readonly Player _humanPlayer;
        private readonly Player _aiPlayerModel;
        private readonly AIPlayer _ai;
        private readonly FirebaseService _firebase;
        private readonly bool _isGuest;
        private readonly bool _isMulti;
        private readonly bool _isGemini;
        private readonly GeminiService _gemini;
        private readonly string _opponentName;

        // ?? תיקון מולטיפלייר: האם זה תורי כרגע ??
        private bool _isMyTurn;

        private enum GamePhase { Placement, Playing, Finished }
        private GamePhase _phase = GamePhase.Placement;

        private readonly Dictionary<View, Submarine> _subViewToModel = new();
        private readonly Dictionary<View, (int row, int col)> _aiCellToGrid = new();
        private readonly Dictionary<(int row, int col), Border> _playerCellViews = new();
        private readonly Dictionary<(int row, int col), Border> _aiCellViews = new();
        private readonly Dictionary<int, View> _playerSubViews = new();

        private const int TurnSeconds = 30;
        private int _timeLeft = TurnSeconds;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _pollCts;
        private int _knownOpponentShots = 0;
        private int _knownChatMessages = 0;

        public GamePage(
            Player player,
            FirebaseService firebase,
            bool isGuest = false,
            bool isMulti = false,
            bool isGemini = false,
            string opponentName = "יריב")
        {
            InitializeComponent();
            _humanPlayer = player;
            _aiPlayerModel = new Player(0, opponentName);
            _game = new Game(1);
            _ai = new AIPlayer();
            _firebase = firebase;
            _isGuest = isGuest;
            _isMulti = isMulti;
            _isGemini = isGemini;
            _gemini = new GeminiService();
            _opponentName = opponentName;

            if (isMulti)
                AIAreaLabel.Text = opponentName;

            if (isGemini)
                AIAreaLabel.Text = "Gemini AI ??";

            this.SizeChanged += OnPageSizeChanged;
        }

        // מונע בנייה כפולה של הלוח — רץ רק פעם אחת
        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (_boardsBuilt || this.Width <= 0 || this.Height <= 0) return;
            _boardsBuilt = true;

            // מחשב גודל תא לפי רוחב המסך
            double halfWidth = (this.Width - 3) / 2.0 - 20;
            _cellSize = Math.Floor(halfWidth / Cols);
            _boardWidth = _cellSize * Cols;

            // מחשב גבולות אזור המשחק
            _boardStartY = SubStorageRows * _cellSize + 8;
            _gameAreaTop = _boardStartY + ExtraRowsTop * _cellSize;
            _gameAreaBottom = _gameAreaTop + GameRows * _cellSize;

            // בונה את שני הלוחות — של השחקן ושל היריב
            BuildArea(PlayerArea, isPlayer: true);
            BuildArea(AIArea, isPlayer: false);

            // מגדיר גובה ורוחב לשני הלוחות
            double totalHeight = _boardStartY + TotalRows * _cellSize + 8;
            PlayerArea.HeightRequest = totalHeight;
            AIArea.HeightRequest = totalHeight;
            PlayerArea.WidthRequest = _boardWidth;
            AIArea.WidthRequest = _boardWidth;
        }

        // בונה לוח גרפי — של שחקן או של יריב
        private void BuildArea(AbsoluteLayout area, bool isPlayer)
        {
            area.Children.Clear();

            // קובע צבעים לפי סוג הלוח (שחקן/יריב/Gemini)
            var gameColor = Color.FromArgb(isPlayer ? "#001F3F" : "#1F0000");
            var borderColor = Color.FromArgb(isPlayer ? "#00111F" : "#110000");
            var strokeColor = Color.FromArgb(isPlayer ? "#4488AA" : "#AA4444");
            var subColor = Color.FromArgb(isPlayer ? "#009ADA" : "#AA3333");

            // אם מצב Gemini — שנה צבעים ללוח היריב לירוק
            if (!isPlayer && _isGemini)
            {
                gameColor = Color.FromArgb("#002200");
                borderColor = Color.FromArgb("#001100");
                strokeColor = Color.FromArgb("#44AA44");
                subColor = Color.FromArgb("#228833");
            }

            // בונה תאים — לכל שורה ועמודה
            for (int row = 0; row < TotalRows; row++)
            {
                bool isGameRow = row >= ExtraRowsTop && row < ExtraRowsTop + GameRows;

                for (int col = 0; col < Cols; col++)
                {
                    // יוצר תא גרפי עם צבע וגבול
                    var cell = new Border
                    {
                        BackgroundColor = isGameRow ? gameColor : borderColor,
                        Stroke = new SolidColorBrush(strokeColor),
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
                        InputTransparent = isPlayer  // לוח שחקן לא מגיב ללחיצות
                    };

                    // ממקם את התא בלוח לפי חישוב פיקסלים
                    AbsoluteLayout.SetLayoutBounds(cell, new Rect(
                        col * _cellSize,
                        _boardStartY + row * _cellSize,
                        _cellSize, _cellSize));

                    // תאי היריב — מוסיף זיהוי לחיצה לכל תא
                    if (!isPlayer && isGameRow)
                    {
                        int capturedRow = row - ExtraRowsTop;
                        int capturedCol = col;
                        var capturedCell = cell;

                        // שומר מיפוי: view ? קואורדינטות
                        _aiCellToGrid[cell] = (capturedRow, capturedCol);
                        _aiCellViews[(capturedRow, capturedCol)] = cell;

                        // מחבר אירוע לחיצה לכל תא
                        var tap = new TapGestureRecognizer();
                        tap.Tapped += (_, _) => OnAICellTapped(capturedCell, capturedRow, capturedCol);
                        cell.GestureRecognizers.Add(tap);
                    }

                    // תאי השחקן — שומר מיפוי לצביעה עתידית
                    if (isPlayer && isGameRow)
                    {
                        int gameRow = row - ExtraRowsTop;
                        _playerCellViews[(gameRow, col)] = cell;
                    }

                    area.Children.Add(cell);
                }
            }

            // מכאן — רק לוח השחקן, מוסיף צוללות לגרירה
            if (!isPlayer) return;

            var fleet = FleetFactory.CreateDefaultFleet();
            double subX = 0, subY = 0;

            foreach (var s in fleet)
            {
                // מחשב גודל הצוללת לפי כיוון ואורך
                bool isHoriz = s.Orientation == Orientation.Horizontal;
                double w = isHoriz ? s.Length * _cellSize : _cellSize;
                double h = isHoriz ? _cellSize : s.Length * _cellSize;

                // עובר לשורה חדשה אם אין מקום
                if (subX + w > _boardWidth && subX > 0)
                {
                    subX = 0;
                    subY += _cellSize + 2;
                }

                double clampedY = Math.Min(subY, _boardStartY - h - 2);

                // יוצר את הצוללת הגרפית
                var border = new Border
                {
                    BackgroundColor = subColor,
                    StrokeThickness = 0,
                    WidthRequest = w,
                    HeightRequest = h,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    TranslationX = 0,
                    TranslationY = 0
                };

                var pos = new Point(subX, clampedY);
                AbsoluteLayout.SetLayoutBounds(border, new Rect(pos.X, pos.Y, w, h));

                // שומר מיפוי: view ? מודל צוללת
                _playerOrigPos[border] = pos;
                _subViewToModel[border] = s;
                _playerSubViews[s.SubmarineId] = border;

                // מוסיף אירוע גרירה לכל צוללת
                var captured = border;
                var pan = new PanGestureRecognizer();
                pan.PanUpdated += (_, args) => OnSubmarinePan(captured, args);
                border.GestureRecognizers.Add(pan);

                area.Children.Add(border);
                subX += w + 4;
            }
        }

        private void OnSubmarinePan(View sub, PanUpdatedEventArgs e)
        {
            if (_phase != GamePhase.Placement) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // שומר את נקודת ההתחלה
                    _lastTotal[sub] = new Point(e.TotalX, e.TotalY);
                    break;

                case GestureStatus.Running:
                    {
                        var last = _lastTotal.GetValueOrDefault(sub, Point.Zero);
                        // מחשב כמה זזנו מהנקודה האחרונה
                        double dx = e.TotalX - last.X;
                        double dy = e.TotalY - last.Y;
                        _lastTotal[sub] = new Point(e.TotalX, e.TotalY);

                        var bounds = AbsoluteLayout.GetLayoutBounds(sub);
                        double absX = bounds.X + sub.TranslationX + dx;
                        double absY = bounds.Y + sub.TranslationY + dy;

                        absX = Math.Max(0, Math.Min(absX, _boardWidth - bounds.Width));
                        absY = Math.Max(0, Math.Min(absY, _boardStartY + TotalRows * _cellSize - bounds.Height));

                        // מזיז את הצוללת — עם הגבלת גבולות
                        sub.TranslationX = absX - bounds.X;
                        sub.TranslationY = absY - bounds.Y;
                        break;
                    }

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    {
                        _lastTotal.Remove(sub);
                        var bounds = AbsoluteLayout.GetLayoutBounds(sub);
                        double finalX = bounds.X + sub.TranslationX;
                        double finalY = bounds.Y + sub.TranslationY;
                        sub.TranslationX = 0;
                        sub.TranslationY = 0;
                        AbsoluteLayout.SetLayoutBounds(sub, new Rect(finalX, finalY, bounds.Width, bounds.Height));
                        // שחררנו — נסה לנצמד לתא
                        TrySnapToBoard(sub);
                        break;
                    }
            }
        }

        private void TrySnapToBoard(View sub)
        {
            //מחשב את מרכז הצוללת לפי מיקומה הנוכחי
            var cur = AbsoluteLayout.GetLayoutBounds(sub);
            double cx = cur.X + cur.Width / 2;
            double cy = cur.Y + cur.Height / 2;

            // בודק אם מרכז הצוללת נמצא בתוך אזור המשחק
            bool overGameArea =
                cx >= 0 && cx <= _boardWidth &&
                cy >= _gameAreaTop && cy <= _gameAreaBottom;

            if (overGameArea)
            {
                // מחשב לאיזה תא הצוללת הכי קרובה
                int col = (int)Math.Floor(cur.X / _cellSize);
                int row = (int)Math.Floor((cur.Y - _gameAreaTop) / _cellSize);

                // מוודא שהתא בגבולות הלוח
                col = Math.Clamp(col, 0, Cols - 1);
                row = Math.Clamp(row, 0, GameRows - 1);

                // מחשב את המיקום המדויק של התא
                double snapX = col * _cellSize;
                double snapY = _gameAreaTop + row * _cellSize;
                // בודק שהצוללת כולה נכנסת בתוך הלוח ולא חורגת
                bool fits =
                    snapX + cur.Width <= _boardWidth + 0.5 &&
                    snapY + cur.Height <= _gameAreaBottom + 0.5;

                if (fits)
                {
                    // מצמיד את הצוללת לתא ושומר את המיקום החדש
                    AbsoluteLayout.SetLayoutBounds(sub, new Rect(snapX, snapY, cur.Width, cur.Height));
                    _playerOrigPos[sub] = new Point(snapX, snapY);
                    return;
                }
            }
            //הצוללת מחוץ ללוח או לא נכנסת — חוזרת למקום המקורי
            ReturnToOrigin(sub);
        }

        private void ReturnToOrigin(View sub)
        {
            // בודק אם יש מיקום מקורי שמור לצוללת זו
            if (_playerOrigPos.TryGetValue(sub, out var origin))
            {
                // מחזיר את הצוללת למיקומה המקורי — שומר על הגודל אבל מאפס את המיקום
                var b = AbsoluteLayout.GetLayoutBounds(sub);
                AbsoluteLayout.SetLayoutBounds(sub, new Rect(origin.X, origin.Y, b.Width, b.Height));
            }
        }

        private async void StartGame_Click(object sender, EventArgs e)
        {
            // מאתחל את המשחק עם שני השחקנים
            _game.SetupPlayers(_humanPlayer, _aiPlayerModel);

            // מניח את צוללות השחקן — אם לא כולן על הלוח, עוצר
            bool allPlaced = PlacePlayerSubmarines();
            if (!allPlaced) return;

            // מניח את צוללות היריב אקראית ומתחיל את המשחק
            PlaceAISubmarines();
            _game.StartGame();
            _phase = GamePhase.Playing;

            StartGameBtn.IsVisible = false;
            if (_isMulti)
            {
                await _firebase.SetPlayerReadyAsync();
                UpdateStatusLabel("ממתין ליריב...", "#AA8800");

                while (!await _firebase.BothPlayersReadyAsync())
                    await Task.Delay(2000);

                _isMyTurn = _firebase.IsPlayer1;
                UpdateStatusLabel(_isMyTurn ? "תורך לירות" : $"תור {_opponentName}",
                                  _isMyTurn ? "#009ADA" : "#AA3333");
                StartPolling();
            }
            // מאפס ומפעיל את טיימר התור
            ResetTimer();
        }

        private bool PlacePlayerSubmarines()
        {
            // עובר על כל הצוללות שהשחקן הניח גרפית
            foreach (var (view, submarine) in _subViewToModel)
            {
                // מחשב את מרכז הצוללת הגרפית
                var bounds = AbsoluteLayout.GetLayoutBounds(view);
                double cx = bounds.X + bounds.Width / 2;
                double cy = bounds.Y + bounds.Height / 2;

                // בודק שהצוללת נמצאת בתוך גבולות הלוח
                bool onBoard =
                    cx >= 0 && cx <= _boardWidth &&
                    cy >= _gameAreaTop && cy <= _gameAreaBottom;

                if (!onBoard)
                {
                    DisplayAlert("שגיאה", "יש להניח את כל הצוללות על הלוח", "אישור");
                    return false;
                }

                // ממיר מיקום גרפי (פיקסלים) לקואורדינטות לוח (שורה/עמודה)
                int col = (int)Math.Floor(bounds.X / _cellSize);
                int row = (int)Math.Floor((bounds.Y - _gameAreaTop) / _cellSize);

                col = Math.Clamp(col, 0, Cols - 1);
                row = Math.Clamp(row, 0, GameRows - 1);

                // מעדכן את מיקום הצוללת בלוגיקה ומנסה להניח אותה
                submarine.SetLocation(new GridLocation(row, col));
                bool placed = _game.PlaceSubmarine(_humanPlayer, submarine);

                // אם שתי צוללות חופפות — שגיאה
                if (!placed)
                {
                    DisplayAlert("שגיאה", "שתי צוללות חופפות", "אישור");
                    return false;
                }
            }
            // כל הצוללות הונחו בהצלחה
            return true;
        }

        private void PlaceAISubmarines()
        {
            var random = new Random();
            //יצירת הצווללות
            var aiFleet = new List<Submarine>
            {
                new Submarine(10, new GridLocation(0, 0), 4, Orientation.Horizontal),
                new Submarine(11, new GridLocation(0, 0), 3, Orientation.Horizontal),
                new Submarine(12, new GridLocation(0, 0), 2, Orientation.Horizontal),
                new Submarine(13, new GridLocation(0, 0), 3, Orientation.Vertical),
                new Submarine(14, new GridLocation(0, 0), 2, Orientation.Vertical),
            };

            foreach (var sub in aiFleet)
            {
                bool placed = false;
                int attempts = 0;
                while (!placed && attempts < 500)
                {
                    attempts++;
                    int row = random.Next(0, GameRows);
                    int col = random.Next(0, Cols);
                    var orientation = (Orientation)random.Next(0, 2);
                    sub.SetLocation(new GridLocation(row, col));
                    sub.SetOrientation(orientation);
                    placed = _game.PlaceSubmarine(_aiPlayerModel, sub);
                }
            }
        }

        private async void OnAICellTapped(View cell, int row, int col)
        {
            // מוודא שהמשחק פעיל ושזה תור השחקן
            if (_phase != GamePhase.Playing) return;
            if (_isMulti && !_isMyTurn) return;
            if (!_isMulti && _game.CurrentPlayer != _humanPlayer) return;

            // בודק שלא ירו כבר על התא הזה
            var aiBoard = _game.GetOpponentBoardPublic(_humanPlayer);
            if (!aiBoard.CanShootAt(row, col)) return;

            // מבצע את הירייה בלוגיקה וצובע את התא בהתאם לתוצאה
            var result = _game.ProcessShot(new GridLocation(row, col));
            UpdateCellVisual(cell, result);

            // מציג הודעת Toast למשתמש לפי תוצאת הירייה
            string toastMsg = result switch
            {
                ShotResult.Miss => "פספסת",
                ShotResult.Hit => "פגעת",
                ShotResult.Sunk => "הטבעת צוללת",
                _ => ""
            };
            ShowToast(toastMsg, result);

            if (_isMulti)
            {
                _isMyTurn = false;
                await _firebase.SendShotAsync(row, col, result.ToString());
            }

            // בודק אם המשחק נגמר לאחר הירייה
            if (_game.Status == Game.GameStatus.Finished)
            {
                EndGame();
                return;
            }
            // מעדכן את תווית המצב — Gemini חושב או תור היריב
            UpdateStatusLabel(_isGemini ? "Gemini חושב... ??" : $"תור {_opponentName}", "#AA3333");
            ResetTimer();
            // מול AI בלבד — מפעיל את תור היריב
            if (!_isMulti)
            {
                if (_isGemini)
                {
                    // שולח את מצב הלוח ל-Gemini וממתין לתשובה
                    await GeminiTurnAsync();
                }
                else
                {
                    // מפעיל את ה-AI האלגוריתמי אחרי המתנה קצרה של 800ms
                    Dispatcher.StartTimer(TimeSpan.FromMilliseconds(800), () =>
                    {
                        AITurn();
                        return false;
                    });
                }
            }
        }

        private void AITurn()
        {
            var playerBoard = _game.GetOpponentBoardPublic(_aiPlayerModel);
            var location = _ai.DecideNextShot(playerBoard);
            var result = _game.ProcessShot(location);

            _ai.ProcessShotResult(location, result);

            int gameRow = location.GetRow();
            int gameCol = location.GetColumn();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AddHitMarkerToPlayerBoard(gameRow, gameCol, result);

                if (result == ShotResult.Sunk)
                    RevealSunkPlayerSubmarine(playerBoard, gameRow, gameCol);

                string toastMsg = result switch
                {
                    ShotResult.Miss => "יריב פספס",
                    ShotResult.Hit => "יריב פגע בך",
                    ShotResult.Sunk => "יריב הטביע צוללת",
                    _ => ""
                };
                ShowToast(toastMsg, result);

                if (_game.Status == Game.GameStatus.Finished)
                {
                    EndGame();
                    return;
                }

                UpdateStatusLabel("תורך לירות", "#009ADA");
                ResetTimer();
            });
        }

        private async Task GeminiTurnAsync()
        {
            // מקבל את לוח השחקן ומחליט לאן ה-AI יורה
            var playerBoard = _game.GetOpponentBoardPublic(_aiPlayerModel);
            var boardState = new int[10, 10];
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    var cell = playerBoard.GetCell(r, c);
                    if (!cell.WasShot)
                        boardState[r, c] = 0;
                    else if (!cell.IsHit)
                        boardState[r, c] = 1;
                    else
                        boardState[r, c] = 2;
                }
            }

            var shot = await _gemini.GetNextShotAsync(boardState);

            //מנגנון גיבוי במקרה והגמיני עושה מהלך אינו חוקי
            if (shot == null || !playerBoard.CanShootAt(shot.Value.row, shot.Value.col))
            {
                AITurn();
                return;
            }

            var location = new GridLocation(shot.Value.row, shot.Value.col);
            // מבצע את הירייה בלוגיקה ומעדכן את ה-AI עם התוצאה
            var result = _game.ProcessShot(location);

            int gameRow = shot.Value.row;
            int gameCol = shot.Value.col;
            // עדכון הממשק חייב לרוץ על Thread הראשי
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // מציג את סימן הירייה על לוח השחקן
                AddHitMarkerToPlayerBoard(gameRow, gameCol, result);

                // אם הצוללת הוטבעה — חושף את כל תאיה
                if (result == ShotResult.Sunk)
                    RevealSunkPlayerSubmarine(playerBoard, gameRow, gameCol);

                // מציג הודעת Toast לפי תוצאת הירייה
                string toastMsg = result switch
                {
                    ShotResult.Miss => "Gemini פספס ??",
                    ShotResult.Hit => "Gemini פגע בך! ??",
                    ShotResult.Sunk => "Gemini הטביע צוללת! ??",
                    _ => ""
                };
                ShowToast(toastMsg, result);

                // בודק אם ה-AI ניצח
                if (_game.Status == Game.GameStatus.Finished)
                {
                    EndGame();
                    return;
                }
                // מחזיר את התור לשחקן ומאפס את הטיימר
                UpdateStatusLabel("תורך לירות", "#009ADA");
                ResetTimer();
            });
        }

        private void StartPolling()
        {
            // יוצר CancellationToken לעצירת הלולאה בעת הצורך
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;

            // מפעיל לולאה ברקע — נפרד מה-Thread הראשי כדי לא לקפא את המסך
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);   /*חכה 2 שניות*/

                    // בודק אם היריב ירה — אם כן מציג את הירייה על המסך\
                    var shot = await _firebase.GetLatestOpponentShotAsync(_knownOpponentShots);
                    if (shot != null)
                    {
                        _knownOpponentShots++;
                        MainThread.BeginInvokeOnMainThread(() => ApplyOpponentShot(shot));
                    }

                    // בודק אם יש הודעות צ'אט חדשות — אם כן מוסיף לתצוגה
                    var messages = await _firebase.GetChatMessagesAsync(_knownChatMessages);
                    if (messages.Any())
                    {
                        _knownChatMessages += messages.Count;
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            foreach (var msg in messages)
                                ChatMessages.Text += $"{msg.Sender}: {msg.Text}\n";
                            // גולל את הצ'אט למטה להודעה האחרונה
                            await ChatScrollView.ScrollToAsync(0, ChatMessages.Height, true);
                        });
                    }
                }
            }, token);
        }

        private void ApplyOpponentShot(MultiShot shot)
        {
            // מוודא שהמשחק עדיין פעיל
            if (_phase != GamePhase.Playing) return;

            // ממיר את תוצאת הירייה מטקסט ל-ShotResult
            var result = shot.Result switch
            {
                "Hit" => ShotResult.Hit,
                "Sunk" => ShotResult.Sunk,
                _ => ShotResult.Miss
            };
            // מציג את סימן הירייה על לוח השחקן
            AddHitMarkerToPlayerBoard(shot.Row, shot.Col, result);

            // אם הצוללת הוטבעה — חושף את כל תאיה
            var playerBoard = _game.GetOpponentBoardPublic(_aiPlayerModel);
            if (result == ShotResult.Sunk)
                RevealSunkPlayerSubmarine(playerBoard, shot.Row, shot.Col);

            // מציג הודעת Toast עם שם היריב ותוצאת הירייה
            string toastMsg = result switch
            {
                ShotResult.Miss => $"{_opponentName} פספס",
                ShotResult.Hit => $"{_opponentName} פגע בך",
                ShotResult.Sunk => $"{_opponentName} הטביע צוללת",
                _ => ""
            };
            ShowToast(toastMsg, result);

            // מעדכן שעכשיו זה תורי ומאפס את הטיימר
            _isMyTurn = true;
            UpdateStatusLabel("תורך לירות", "#009ADA");
            ResetTimer();
        }

        private void AddHitMarkerToPlayerBoard(int gameRow, int gameCol, ShotResult result)
        {
            // מחשב גודל ומיקום של הסימון בתוך התא
            double markerSize = _cellSize * 0.7;
            double offset = _cellSize * 0.15;
            double x = gameCol * _cellSize + offset;
            double y = _gameAreaTop + gameRow * _cellSize + offset;

            // קובע צבע לפי תוצאת הירייה
            var color = result switch
            {
                ShotResult.Miss => Color.FromArgb("#88AAAAAA"),
                ShotResult.Hit => Color.FromArgb("#DDFF6600"),
                ShotResult.Sunk => Color.FromArgb("#DDFF0000"),
                _ => Colors.Transparent
            };

            // יוצר עיגול גרפי שמייצג את הירייה
            var marker = new BoxView
            {
                Color = color,
                WidthRequest = markerSize,
                HeightRequest = markerSize,
                CornerRadius = (float)(markerSize / 2),
                InputTransparent = true   // לא מגיב ללחיצות
            };

            // ממקם את הסימון על הלוח ומוסיף אותו לתצוגה
            AbsoluteLayout.SetLayoutBounds(marker, new Rect(x, y, markerSize, markerSize));
            PlayerArea.Children.Add(marker);
        }

        private void RevealSunkPlayerSubmarine(Board playerBoard, int hitRow, int hitCol)
        {
            // מוצא את ה-ID של הצוללת שהוטבעה לפי התא שנפגע
            int subId = playerBoard.GetSubmarineIdAt(hitRow, hitCol);
            if (subId == -1) return;  // אם לא נמצאה צוללת — יציאה

            // מקבל את כל התאים של הצוללת הזו
            var cells = playerBoard.GetSubmarineCells(subId);
            // צובע כל תא של הצוללת באדום ומוסיף סימון טביעה
            foreach (var (r, c) in cells)
            {
                if (_playerCellViews.TryGetValue((r, c), out var cellView))
                    cellView.BackgroundColor = Color.FromArgb("#88FF0000");
                AddHitMarkerToPlayerBoard(r, c, ShotResult.Sunk);
            }
        }

        private void UpdateCellVisual(View cell, ShotResult result)
        {
            // מוודא שה-View הוא Border — אחרת אין מה לצבוע
            if (cell is not Border border) return;
            // צובע את התא לפי תוצאת הירייה
            border.BackgroundColor = result switch
            {
                ShotResult.Miss => Color.FromArgb("#555555"),
                ShotResult.Hit => Color.FromArgb("#FF6600"),
                ShotResult.Sunk => Color.FromArgb("#FF0000"),
                _ => border.BackgroundColor
            };
        }

        private async void ShowToast(string message, ShotResult result)
        {
            // מגדיר את הטקסט וצבע הרקע של ה-Toast לפי תוצאת הירייה
            ToastLabel.Text = message;
            ToastFrame.BackgroundColor = result switch
            {
                ShotResult.Miss => Color.FromArgb("#CC333333"),
                ShotResult.Hit => Color.FromArgb("#CCAA4400"),
                ShotResult.Sunk => Color.FromArgb("#CCAA0000"),
                _ => Color.FromArgb("#CC000000")
            };
            // מציג את ה-Toast
            ToastFrame.IsVisible = true;
            ToastFrame.Opacity = 1;
            // ממתין 3 שניות ואז מעלים בהדרגה תוך 500ms
            await Task.Delay(3000);
            await ToastFrame.FadeTo(0, 500);
            // מסתיר לגמרי ומאפס שקיפות לשימוש הבא
            ToastFrame.IsVisible = false;
            ToastFrame.Opacity = 1;
        }

        private async void EndGame()
        {
            // מסמן שהמשחק הסתיים ועוצר את הטיימר והפולינג
            _phase = GamePhase.Finished;
            _cts?.Cancel();
            _pollCts?.Cancel();

            // אוסף את נתוני המשחק הנוכחי
            bool playerWon = _game.Winner == _humanPlayer;
            double accuracy = _humanPlayer.GetCurrentAccuracy();
            int shots = _game.GetShotCount(_humanPlayer);

            // שומר תוצאות ב-Firebase — רק למשתמש מחובר (לא אורח)
            bool newRecord = false;
            if (!_isGuest && _firebase.IsLoggedIn)
            {
                var (_, isNewRecord) = await _firebase.SaveGameResultAsync(playerWon, shots, accuracy);
                newRecord = isNewRecord;
                // במולטיפלייר — מסמן את החדר כגמור ב-Firebase
                if (_isMulti)
                    await _firebase.FinishRoomAsync(playerWon);
            }
            // עובר למסך הסטטיסטיקות עם כל נתוני המשחק
            await Navigation.PushAsync(new StatsPage(
                playerWon: playerWon,
                playerName: _humanPlayer.GetUserName(),
                playerShots: shots,
                playerAccuracy: accuracy,
                aiShots: _game.GetShotCount(_aiPlayerModel),
                aiAccuracy: _aiPlayerModel.GetCurrentAccuracy(),
                firebase: _firebase,
                isGuest: _isGuest,
                newRecord: newRecord,
                player: _humanPlayer
            ));
        }

        // מעדכן את תווית המצב — הטקסט והצבע שמוצגים בראש המסך
        private void UpdateStatusLabel(string text, string colorHex)
        {
            StatusLabel.Text = text;
            StatusLabel.TextColor = Color.FromArgb(colorHex);
        }

        private void StartTimer()
        {
            // יוצר CancellationToken לעצירת הטיימר בעת הצורך
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // מפעיל טיימר שמתעדכן כל שנייה
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                // אם הטיימר בוטל — עצור
                if (token.IsCancellationRequested) return false;
                // מקטין את הזמן שנותר ומעדכן את התצוגה
                _timeLeft--;
                MainThread.BeginInvokeOnMainThread(() => TimerLabel.Text = _timeLeft.ToString());
                // אם הזמן נגמר — מפעיל את פעולת פקיעת הזמן ועוצר
                if (_timeLeft <= 0)
                {
                    MainThread.BeginInvokeOnMainThread(OnTimeExpired);
                    return false;
                }
                return true;
            });
        }

        private void OnTimeExpired()
        {
            // מוודא שהמשחק עדיין פעיל
            if (_phase != GamePhase.Playing) return;

            if (_isMulti)
            {
                // במולטיפלייר — מחליף תור לפי _isMyTurn
                if (_isMyTurn)
                {
                    // הזמן שלי נגמר — עובר לתור היריב
                    _isMyTurn = false;
                    UpdateStatusLabel($"הזמן נגמר - תור {_opponentName}", "#AA3333");
                    ResetTimer();
                }
                else
                {
                    // הזמן של היריב נגמר — חוזר לתורי
                    _isMyTurn = true;
                    UpdateStatusLabel("תורך לירות", "#009ADA");
                    ResetTimer();
                }
            }
            else
            {
                if (_game.CurrentPlayer == _humanPlayer)
                {
                    // הזמן של השחקן נגמר — מחליף תור ומפעיל AI או Gemini
                    _game.SwitchTurn();
                    UpdateStatusLabel(_isGemini ? "Gemini חושב... ??" : $"הזמן נגמר - תור {_opponentName}", "#AA3333");
                    ResetTimer();

                    if (_isGemini)
                        Task.Run(async () => await GeminiTurnAsync());
                    else
                        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(800), () => { AITurn(); return false; });
                }
                else
                {
                    // הזמן של ה-AI נגמר — חוזר לתור השחקן
                    _game.SwitchTurn();
                    UpdateStatusLabel("תורך לירות", "#009ADA");
                    ResetTimer();
                }
            }
        }

        private void ResetTimer()
        {
            _cts?.Cancel();
            _timeLeft = TurnSeconds;
            TimerLabel.Text = _timeLeft.ToString();
            StartTimer();
        }

        // פותח או סוגר את חלון הצ'אט בכל לחיצה
        private void OnChatIconTapped(object sender, TappedEventArgs e)
            => ChatWindow.IsVisible = !ChatWindow.IsVisible;

        private async void SendMessage_Click(object sender, EventArgs e)
        {
            // מוודא שיש טקסט לשלוח — אם ריק, לא עושה כלום
            if (string.IsNullOrWhiteSpace(ChatEntry.Text)) return;

            // שומר את ההודעה ומנקה את שדה הטקסט
            string msg = ChatEntry.Text.Trim();
            ChatEntry.Text = string.Empty;

            // מציג את ההודעה מיד מקומית וגולל למטה
            ChatMessages.Text += $"אני: {msg}\n";
            await ChatScrollView.ScrollToAsync(0, ChatMessages.Height, true);

            // במולטיפלייר — שולח את ההודעה ל-Firebase לשחקן השני
            if (_isMulti)
                await _firebase.SendChatMessageAsync(msg);
        }

        // נקרא אוטומטית כשיוצאים מהמסך — עוצר את הטיימר והפולינג
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _cts?.Cancel();
            _pollCts?.Cancel();
        }
    }
}//xckkdx
