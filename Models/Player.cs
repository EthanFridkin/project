using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    /// <summary>
    /// מייצג שחקן במשחק.
    /// 
    /// שינויים מהגרסה הקודמת:
    ///   - הסרת Password (Firebase Auth מנהל זאת)
    ///   - הוספת AddHit / AddMiss לסטטיסטיקה שוטפת
    ///   - תיקון UpdateBestWin לעדכן רק אם זה שיא חדש
    /// </summary>
    public class Player
    {
        public int PlayerId { get; private set; }
        public string UserName { get; private set; }

        // סטטיסטיקה מצטברת (לאורך כל המשחקים, תישמר ב-Firebase)
        public int TotalWins { get; private set; }
        public int TotalLosses { get; private set; }
        public int BestWinMoves { get; private set; }   // מינימום יריות לניצחון

        // סטטיסטיקה של המשחק הנוכחי בלבד
        public int CurrentHits { get; private set; }
        public int CurrentMisses { get; private set; }

        public Player(int playerId, string username)
        {
            PlayerId = playerId;
            UserName = username;
            BestWinMoves = 0;   // 0 = עדיין לא ניצח
        }

        // ─── סטטיסטיקה שוטפת ───────────────────────────────────

        /// <summary>מוסיף פגיעה. נקרא מ-Game.ProcessShot.</summary>
        public void AddHit() => CurrentHits++;

        /// <summary>מוסיף פספוס. נקרא מ-Game.ProcessShot.</summary>
        public void AddMiss() => CurrentMisses++;

        /// <summary>
        /// מחשב אחוז דיוק במשחק הנוכחי.
        /// </summary>
        public double GetCurrentAccuracy()
        {
            int total = CurrentHits + CurrentMisses;
            if (total == 0) return 0;
            return (double)CurrentHits / total * 100;
        }

        // ─── סטטיסטיקה מצטברת ──────────────────────────────────

        public void AddWin() => TotalWins++;
        public void AddLoss() => TotalLosses++;

        /// <summary>
        /// מעדכן שיא מהלכים – רק אם זה שיא חדש.
        /// </summary>
        public void UpdateBestWin(int moves)
        {
            // BestWinMoves == 0 אומר שזה הניצחון הראשון
            if (BestWinMoves == 0 || moves < BestWinMoves)
                BestWinMoves = moves;
        }

        /// <summary>
        /// מאפס סטטיסטיקת משחק נוכחי לקראת משחק חדש.
        /// הסטטיסטיקה המצטברת לא מאופסת.
        /// </summary>
        public void ResetCurrentStats()
        {
            CurrentHits = 0;
            CurrentMisses = 0;
        }

        public int GetPlayerId() => PlayerId;
        public string GetUserName() => UserName;
    }
}
