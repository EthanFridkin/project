using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Project
{

    public class GameRecord
    {
        public string Date = string.Empty;
        public bool Won = false;
        public int Shots = 0;
        public double Accuracy = 0;
    }

    public class UserProfile
    {
        public string Username = string.Empty;
        public int TotalWins = 0;
        public int TotalLosses = 0;
        public int BestWin = 0;
    }

    public class RoomData
    {
        public string Player1Id = string.Empty;
        public string Player1Name = string.Empty;
        public string Player2Id = string.Empty;
        public string Player2Name = string.Empty;
        public string Status = "waiting";
        public string CurrentTurn = "player1";
        public string Winner = string.Empty;
        public long CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool Player1Ready = false;
        public bool Player2Ready = false;
    }

    public class MultiShot
    {
        public int Row = 0;
        public int Col = 0;
        public string Result = string.Empty;
        public long ShotTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public class ChatMessage
    {
        public string Sender = string.Empty;
        public string Text = string.Empty;
        public string Time = string.Empty;
    }

    public class FirebaseService
    {
        private const string ApiKey = "AIzaSyBLlRgUtNhJBfQMCSI_jNontGkTKtVHXtk";
        private const string DatabaseUrl = "https://submarine-dc0df-default-rtdb.firebaseio.com/";

        private readonly HttpClient _http;

        // תיקון: IncludeFields=true כדי לסדר fields ולא רק properties
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public string? IdToken { get; private set; }
        public string? UserId { get; private set; }
        public string? Username { get; private set; }
        public string? CurrentRoomId { get; private set; }
        public bool IsPlayer1 { get; private set; }

        public bool IsLoggedIn => IdToken != null;

        public FirebaseService()
        {
            _http = new HttpClient();
        }

        // עזר: שליחת JSON עם תמיכה ב-fields 
        private StringContent ToJson<T>(T obj)
            => new StringContent(JsonSerializer.Serialize(obj, _opts), Encoding.UTF8, "application/json");

        private async Task<string> GetJson(string url)
        {
            var r = await _http.GetAsync(url);
            return await r.Content.ReadAsStringAsync();
        }

        private async Task PutJson<T>(string url, T obj)
            => await _http.PutAsync(url, ToJson(obj));

        private async Task PostJson<T>(string url, T obj)
            => await _http.PostAsync(url, ToJson(obj));

        private async Task PatchJson<T>(string url, T obj)
        {
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = ToJson(obj)
            };
            await _http.SendAsync(req);
        }

        //  הרשמה
        public async Task<(bool success, string error)> RegisterAsync(string username, string password)
        {
            try
            {
                string email = $"{username.ToLower()}@battleship.app";
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
                var body = new { email, password, returnSecureToken = true };

                var response = await _http.PostAsJsonAsync(url, body);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var err = JsonDocument.Parse(json)
                        .RootElement.GetProperty("error")
                        .GetProperty("message").GetString();

                    return (false, err switch
                    {
                        "EMAIL_EXISTS" => "שם המשתמש תפוס",
                        "WEAK_PASSWORD" => "סיסמה חלשה מדי (לפחות 6 תווים)",
                        _ => "שגיאת הרשמה"
                    });
                }

                var doc = JsonDocument.Parse(json).RootElement;
                IdToken = doc.GetProperty("idToken").GetString();
                UserId = doc.GetProperty("localId").GetString();
                Username = username;

                await SaveUserProfileAsync(username);
                return (true, string.Empty);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }


        public async Task<(bool success, string error)> LoginAsync(string username, string password)
        {
            try
            {
                // יצירת אימייל פיקטיבי: Firebase Auth דורש אימייל, ואני רציתי שהמשתמש יתחבר רק עם שם משתמש פשוט
                string email = $"{username.ToLower()}@battleship.app";
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
                var body = new { email, password, returnSecureToken = true };

                // שליחת בקשת התחברות לשרת ה-REST של Firebase
                var response = await _http.PostAsJsonAsync(url, body);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // במקרה של שגיאה, אני מחלץ את הודעת השגיאה המדויקת מתוך ה-JSON שחזר מהשרת
                    var err = JsonDocument.Parse(json)
                        .RootElement.GetProperty("error")
                        .GetProperty("message").GetString();

                    // תרגום קודי השגיאה של Firebase לעברית ידידותית למשתמש כדי להציג באפליקציה
                    return (false, err switch
                    {
                        "EMAIL_NOT_FOUND" => "שם המשתמש לא קיים",
                        "INVALID_PASSWORD" => "סיסמה שגויה",
                        "INVALID_LOGIN_CREDENTIALS" => "שם משתמש או סיסמה שגויים",
                        _ => "שגיאת התחברות"
                    });
                }

                // ההתחברות הצליחה - אני שומר את הטוקן (לצורך הרשאות לדאטאבייס) ואת מזהה המשתמש
                var doc = JsonDocument.Parse(json).RootElement;
                IdToken = doc.GetProperty("idToken").GetString();
                UserId = doc.GetProperty("localId").GetString();
                Username = username;

                // צור פרופיל אם לא קיים
                await EnsureProfileExistsAsync(username);

                return (true, string.Empty);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // שמירת פרופיל 
        private async Task SaveUserProfileAsync(string username)
        {
            // יצירת אובייקט פרופיל ושמירתו בנתיב של המשתמש בעזרת PUT, כדי לאפשר יצירה או דריסה במיקום מדויק
            var profile = new UserProfile { Username = username };
            var url = $"{DatabaseUrl}users/{UserId}/profile.json?auth={IdToken}";
            await PutJson(url, profile);
        }

        private async Task EnsureProfileExistsAsync(string username)
        {
            // אני קודם בודק אם הפרופיל כבר קיים, כדי לא למחוק או לדרוס למשתמש נתונים (כמו סטטיסטיקות) בהתחברות חוזרת
            var url = $"{DatabaseUrl}users/{UserId}/profile.json?auth={IdToken}";
            var json = await GetJson(url);
            if (json == "null")
                await SaveUserProfileAsync(username);
        }

        // שמירת תוצאת משחק
        public async Task<(bool saved, bool newRecord)> SaveGameResultAsync(bool won, int shots, double accuracy)
        {
            if (!IsLoggedIn) return (false, false);
            try
            {
                // אני שומר את נתוני המשחק שהסתיים, כולל חותמת זמן כדי שנוכל להציג היסטוריה למשתמש
                var record = new GameRecord
                {
                    Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    Won = won,
                    Shots = shots,
                    Accuracy = Math.Round(accuracy, 1)
                };

                // שימוש ב-POST כדי להוסיף רשומה לרשימת המשחקים (POST מייצר ID ייחודי אוטומטי לכל משחק ב-Firebase)
                var gamesUrl = $"{DatabaseUrl}users/{UserId}/games.json?auth={IdToken}";
                await PostJson(gamesUrl, record);

                // במקביל שומר את התוצאה להיסטוריה, אני גם מעדכן את הסטטיסטיקה הכללית של השחקן (כמו סך ניצחונות)
                bool newRecord = await UpdateStatsAsync(won, shots);
                return (true, newRecord);
            }
            catch { return (false, false); }
        }

        //  עדכון סטטיסטיקות 
        private async Task<bool> UpdateStatsAsync(bool won, int shots)
        {
            var profileUrl = $"{DatabaseUrl}users/{UserId}/profile.json?auth={IdToken}";
            var json = await GetJson(profileUrl);

            UserProfile profile;
            try
            {
                // שליפת הפרופיל הקיים, או יצירת אובייקט בסיסי חדש במקרה של שגיאת פיענוח
                profile = JsonSerializer.Deserialize<UserProfile>(json, _opts)
                    ?? new UserProfile { Username = Username ?? "" };
            }
            catch { profile = new UserProfile { Username = Username ?? "" }; }

            // קידום מוני הניצחונות/הפסדים בהתאם לתוצאת המשחק הנוכחי
            if (won) profile.TotalWins++;
            else profile.TotalLosses++;

            bool newRecord = false;
            // אני בודק אם המשתמש שבר את השיא של עצמו (ניצחון במינימום יריות)
            if (won && (profile.BestWin == 0 || shots < profile.BestWin))
            {
                profile.BestWin = shots;
                newRecord = true;
            }

            // שומר את הפרופיל המעודכן בחזרה לדאטאבייס
            await PutJson(profileUrl, profile);
            return newRecord;
        }

        // שליפת סטטיסטיקות 
        public async Task<(UserProfile? profile, List<GameRecord> games)> GetUserStatsAsync()
        {
            if (!IsLoggedIn) return (null, new List<GameRecord>());
            try
            {
                // 1. קריאת נתוני הפרופיל הכלליים מהשרת
                var profileUrl = $"{DatabaseUrl}users/{UserId}/profile.json?auth={IdToken}";
                var profileJson = await GetJson(profileUrl);
                var profile = JsonSerializer.Deserialize<UserProfile>(profileJson, _opts);

                // 2. קריאת היסטוריית המשחקים מהשרת
                var gamesUrl = $"{DatabaseUrl}users/{UserId}/games.json?auth={IdToken}";
                var gamesJson = await GetJson(gamesUrl);

                var games = new List<GameRecord>();
                if (gamesJson != "null")
                {
                    // Firebase מחזיר מילון (Dictionary) אז אני מפענח אותו, לוקח רק את הערכים וממיין אותם לפי תאריך יורד
                    var dict = JsonSerializer.Deserialize<Dictionary<string, GameRecord>>(gamesJson, _opts);
                    if (dict != null)
                        games = dict.Values.OrderByDescending(g => g.Date).ToList();
                }

                return (profile, games);
            }
            catch { return (null, new List<GameRecord>()); }
        }

        // 
        // מולטיפלייר
        //

        public async Task<(string roomId, bool isPlayer1, string opponentName)> FindOrCreateRoomAsync()
        {
            var roomsUrl = $"{DatabaseUrl}rooms.json?auth={IdToken}";
            var json = await GetJson(roomsUrl);

            // קודם כל אני בודק אם יש חדרים קיימים כדי לנסות לשדך למשחק פעיל
            if (json != "null")
            {
                var rooms = JsonSerializer.Deserialize<Dictionary<string, RoomData>>(json, _opts);

                if (rooms != null)
                {
                    // אני מחפש חדר שסטטוס שלו "waiting" ושחקן אחר יצר אותו (לא אני)
                    var waiting = rooms.FirstOrDefault(r =>
                        r.Value.Status == "waiting" &&
                        r.Value.Player1Id != UserId);

                    if (waiting.Key != null)
                    {
                        // מצאתי חדר פנוי! אני מצטרף אליו בתור שחקן 2 ומשנה את הסטטוס ל-"placing" (הצבת צוללות)
                        var joinData = new { Player2Id = UserId, Player2Name = Username, Status = "placing" };
                        await PatchJson($"{DatabaseUrl}rooms/{waiting.Key}.json?auth={IdToken}", joinData);

                        CurrentRoomId = waiting.Key;
                        IsPlayer1 = false;
                        return (waiting.Key, false, waiting.Value.Player1Name);
                    }
                }
            }

            // אם אין חדר פנוי, אני יוצר חדר חדש ואוטומטית מוגדר להיות שחקן 1
            var newRoom = new RoomData
            {
                Player1Id = UserId!,
                Player1Name = Username!,
                Status = "waiting",
                CurrentTurn = "player1", // שחקן 1 הוא זה שמתחיל את המשחק תמיד
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() // שומר זמן כדי שנוכל לנקות חדרים נטושים מאוחר יותר
            };

            var createResp = await _http.PostAsync(roomsUrl, ToJson(newRoom));
            var createJson = await createResp.Content.ReadAsStringAsync();

            // מחלץ את המזהה הייחודי שהופק לחדר ב-Firebase
            var roomId = JsonDocument.Parse(createJson).RootElement
                                 .GetProperty("name").GetString()!;

            CurrentRoomId = roomId;
            IsPlayer1 = true;
            return (roomId, true, string.Empty);
        }

        public async Task<(bool joined, string opponentName)> CheckRoomStatusAsync()
        {
            if (CurrentRoomId == null) return (false, string.Empty);
            try
            {
                // הפונקציה הזו משמשת לפולינג של שחקן 1 - היא בודקת אם החדר התמלא ושחקן 2 הצטרף
                var json = await GetJson($"{DatabaseUrl}rooms/{CurrentRoomId}.json?auth={IdToken}");
                var room = JsonSerializer.Deserialize<RoomData>(json, _opts);

                if (room == null) return (false, string.Empty);
                bool joined = room.Status == "placing" && !string.IsNullOrEmpty(room.Player2Id);
                return (joined, room.Player2Name);
            }
            catch { return (false, string.Empty); }
        }

        public async Task SetPlayerReadyAsync()
        {
            if (CurrentRoomId == null) return;

            // כאן אני מעדכן דינמית את השדה הנכון בשרת בהתאם לסוג השחקן שלי (כדי להגיד שסיימתי להציב צוללות)
            var field = IsPlayer1 ? "Player1Ready" : "Player2Ready";
            await PatchJson($"{DatabaseUrl}rooms/{CurrentRoomId}.json?auth={IdToken}",
                new Dictionary<string, bool> { { field, true } });
        }

        public async Task<bool> BothPlayersReadyAsync()
        {
            if (CurrentRoomId == null) return false;
            try
            {
                // בדיקה מול השרת האם שני השחקנים סיימו להציב צוללות (כדי שנוכל לעבור למסך הקרב עצמו)
                var json = await GetJson($"{DatabaseUrl}rooms/{CurrentRoomId}.json?auth={IdToken}");
                var room = JsonSerializer.Deserialize<RoomData>(json, _opts);
                return room?.Player1Ready == true && room?.Player2Ready == true;
            }
            catch { return false; }
        }

        public async Task SendShotAsync(int row, int col, string result)
        {
            if (CurrentRoomId == null) return;

            // אני מכין את המפתחות לשליחה - מזהה לאיזו רשימת יריות אני כותב, ולמי אני מעביר את התור
            var shotKey = IsPlayer1 ? "shots1" : "shots2";
            var turnKey = IsPlayer1 ? "player2" : "player1";

            var shot = new MultiShot
            {
                Row = row,
                Col = col,
                Result = result,
                ShotTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // רושם את הירייה בשרת (POST) ומיד מעדכן באמצעות PATCH שהתור עובר ליריב שלי
            await PostJson($"{DatabaseUrl}rooms/{CurrentRoomId}/{shotKey}.json?auth={IdToken}", shot);
            await PatchJson($"{DatabaseUrl}rooms/{CurrentRoomId}.json?auth={IdToken}",
                new { CurrentTurn = turnKey });
        }

        public async Task<MultiShot?> GetLatestOpponentShotAsync(int knownShotCount)
        {
            if (CurrentRoomId == null) return null;
            try
            {
                // אני בודק את רשימת היריות של היריב (אם אני 1 אני בודק את 2, ולהפך)
                var shotKey = IsPlayer1 ? "shots2" : "shots1";
                var json = await GetJson($"{DatabaseUrl}rooms/{CurrentRoomId}/{shotKey}.json?auth={IdToken}");

                if (json == "null") return null;

                var shots = JsonSerializer.Deserialize<Dictionary<string, MultiShot>>(json, _opts);

                // ייעול: אם כמות היריות בשרת זהה לכמות שאני כבר מכיר באפליקציה, זה אומר שאין ירייה חדשה
                if (shots == null || shots.Count <= knownShotCount) return null;

                // מחזיר רק את הירייה הכי עדכנית (שנשלפה לפי הזמן)
                return shots.Values.OrderByDescending(s => s.ShotTime).First();
            }
            catch { return null; }
        }

        public async Task SendChatMessageAsync(string text)
        {
            if (CurrentRoomId == null) return;
            var msg = new ChatMessage
            {
                Sender = Username ?? "שחקן",
                Text = text,
                Time = DateTime.Now.ToString("HH:mm")
            };
            // שימוש ב-POST כדי להוסיף הודעה חדשה לרשימת ההודעות של החדר בלי לדרוס הודעות קודמות
            await PostJson($"{DatabaseUrl}rooms/{CurrentRoomId}/chat.json?auth={IdToken}", msg);
        }

        public async Task<List<ChatMessage>> GetChatMessagesAsync(int knownCount)
        {
            if (CurrentRoomId == null) return new List<ChatMessage>();
            try
            {
                var json = await GetJson($"{DatabaseUrl}rooms/{CurrentRoomId}/chat.json?auth={IdToken}");
                if (json == "null") return new List<ChatMessage>();

                var dict = JsonSerializer.Deserialize<Dictionary<string, ChatMessage>>(json, _opts);
                if (dict == null || dict.Count <= knownCount) return new List<ChatMessage>();

                // אני משתמש ב-Skip כדי להחזיר רק את ההודעות החדשות שעוד לא טענתי למסך
                return dict.Values.OrderBy(m => m.Time).Skip(knownCount).ToList();
            }
            catch { return new List<ChatMessage>(); }
        }

        public async Task FinishRoomAsync(bool iWon)
        {
            if (CurrentRoomId == null) return;

            // אני בודק מי מנצח לפי סוג השחקן שלי והפרמטר שהתקבל (האם אני המנצח)
            var winner = iWon
                ? (IsPlayer1 ? "player1" : "player2")
                : (IsPlayer1 ? "player2" : "player1");

            // מעדכן את סטטוס החדר לגמור ומכניס את מי שניצח
            await PatchJson($"{DatabaseUrl}rooms/{CurrentRoomId}.json?auth={IdToken}",
                new { Status = "finished", Winner = winner });

            CurrentRoomId = null;
        }

        public async Task CleanOldRoomsAsync()
        {
            try
            {
                var json = await GetJson($"{DatabaseUrl}rooms.json?auth={IdToken}");
                if (json == "null") return;

                var rooms = JsonSerializer.Deserialize<Dictionary<string, RoomData>>(json, _opts);
                if (rooms == null) return;

                // כדי שהדאטאבייס לא יתמלא בחדרים נטושים, אני בודק אילו חדרים נוצרו לפני יותר משעתיים ומוחק אותם
                long twoHoursAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7200;
                foreach (var room in rooms.Where(r => r.Value.CreatedAt < twoHoursAgo))
                    await _http.DeleteAsync($"{DatabaseUrl}rooms/{room.Key}.json?auth={IdToken}");
            }
            catch { }
        }
    }
}