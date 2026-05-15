using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Battleship.Core.Models;
namespace Battleship.Core.Logic
{
    /// <summary>
    /// מנהל משחק שלם בין שני שחקנים.
    /// אחראי על:
    ///   1. הגדרת שחקנים ולוחות
    ///   2. עיבוד יריות
    ///   3. החלפת תורות
    ///   4. זיהוי ניצחון
    ///   5. ניהול סטטיסטיקה
    /// </summary>
    public class Game
    {
        public enum GameStatus
        {
            WaitingForPlayers,   // ממתין לשני שחקנים
            PlacingSubmarines,   // שחקנים מניחים צוללות
            Active,              // משחק פעיל
            Finished             // משחק הסתיים
        }

        public int GameId { get; private set; }
        public GameStatus Status { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        public Player CurrentPlayer { get; private set; }
        public Player Winner { get; private set; }

        private Board _boardPlayer1;
        private Board _boardPlayer2;

        private readonly List<Shot> _shotsPlayer1;
        private readonly List<Shot> _shotsPlayer2;

        private int _shotCounter;

        public Game(int id)
        {
            GameId = id;
            Status = GameStatus.WaitingForPlayers;
            _shotsPlayer1 = new List<Shot>();
            _shotsPlayer2 = new List<Shot>();
            _shotCounter = 0;
        }

        // ─── הגדרת שחקנים

        /// <summary>
        /// מגדיר את שני השחקנים ומכין לוחות.
        /// יש לקרוא לפני כל שלב אחר.
        /// </summary>
        public void SetupPlayers(Player p1, Player p2)
        {
            Player1 = p1;
            Player2 = p2;
            CurrentPlayer = p1;
            Winner = null;

            _boardPlayer1 = new Board();
            _boardPlayer2 = new Board();

            Status = GameStatus.PlacingSubmarines;
        }

        // הנחת צוללות 


        /// מניח צוללת על לוח שחקן.
        /// מחזיר false אם ההנחה לא תקינה.

        public bool PlaceSubmarine(Player player, Submarine sub)
        {
            return GetBoardForPlayer(player).PlaceSubmarine(sub);
        }


        /// מסמן שהמשחק מוכן להתחיל.

        public void StartGame()
        {
            if (Status != GameStatus.PlacingSubmarines)
                throw new InvalidOperationException("אי אפשר להתחיל – לא בשלב הנחת הצוללות.");

            Status = GameStatus.Active;
        }

        // עיבוד ירייה 


        /// מעבד ירייה של השחקן הנוכחי.

        ///   1. ירייה על לוח היריב
        ///   2. שמירת הירייה
        ///   3. עדכון סטטיסטיקה
        ///   4. בדיקת ניצחון
        ///   5. החלפת תור אם לא ניצח

        public ShotResult ProcessShot(GridLocation location)
        {
            if (Status != GameStatus.Active)
                throw new InvalidOperationException("המשחק לא פעיל.");

            Board targetBoard = GetOpponentBoard(CurrentPlayer);
            ShotResult result = targetBoard.ReceiveShot(location);

            // שמור ירייה
            _shotCounter++;
            var shot = new Shot(_shotCounter, location, result, CurrentPlayer.GetPlayerId());
            GetShotListForPlayer(CurrentPlayer).Add(shot);

            // עדכן סטטיסטיקה
            if (result == ShotResult.Miss)
                CurrentPlayer.AddMiss();
            else
                CurrentPlayer.AddHit();

            // בדוק ניצחון
            if (targetBoard.AreAllSubmarinesDestroyed())
            {
                Winner = CurrentPlayer;
                Status = GameStatus.Finished;

                int totalShots = GetShotListForPlayer(CurrentPlayer).Count;
                Winner.UpdateBestWin(totalShots);
                Winner.AddWin();

                Player loser = (CurrentPlayer == Player1) ? Player2 : Player1;
                loser.AddLoss();

                return result;
            }

            SwitchTurn();
            return result;
        }

        // החלפת תור 

        public void SwitchTurn()
        {
            CurrentPlayer = (CurrentPlayer == Player1) ? Player2 : Player1;
        }

        //  סטטיסטיקות

        /// מחשב אחוז דיוק של שחקן במשחק זה.
        public double GetAccuracy(Player player)
        {
            var shots = GetShotListForPlayer(player);
            if (shots.Count == 0) return 0;

            int hits = 0;
            foreach (var shot in shots)
                if (shot.IsHit) hits++;

            return (double)hits / shots.Count * 100;
        }

        /// מחזיר מספר יריות של שחקן במשחק זה.
        public int GetShotCount(Player player)
            => GetShotListForPlayer(player).Count;

        // עזרים 

        private Board GetBoardForPlayer(Player player)
        {
            if (player == Player1) return _boardPlayer1;
            if (player == Player2) return _boardPlayer2;
            throw new ArgumentException("שחקן לא מוכר.");
        }

        private Board GetOpponentBoard(Player player)
        {
            if (player == Player1) return _boardPlayer2;
            if (player == Player2) return _boardPlayer1;
            throw new ArgumentException("שחקן לא מוכר.");
        }

        private List<Shot> GetShotListForPlayer(Player player)
        {
            if (player == Player1) return _shotsPlayer1;
            if (player == Player2) return _shotsPlayer2;
            throw new ArgumentException("שחקן לא מוכר.");
        }

        public Board GetPlayerBoard(Player player) => GetBoardForPlayer(player);
        public Board GetOpponentBoardPublic(Player player) => GetOpponentBoard(player);
    }
}
