using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Battleship.Core.Models;
using Battleship.Core.Logic;
namespace Battleship.Core.AI
{

    /// שחקן AI עם שני מצבים:
    /// 
    ///   מצב חיפוש (Hunt):
    ///     יורה באקראי על רשת שחמט –
    ///     כיסוי מקסימלי, אף צוללת לא תחמוק.
    /// 
    ///   מצב ציד (Target):
    ///     מופעל אחרי פגיעה ראשונה.
    ///     מנסה 4 כיוונים, נועל כיוון אחרי פגיעה שנייה.
    ///     אחרי טביעה – חוזר למצב חיפוש.
    public class AIPlayer
    {
        private const int BoardSize = 10;

        private enum AIMode { Hunt, Target }
        private AIMode _mode;

        // תור תאים לנסות במצב ציד
        private readonly Queue<GridLocation> _targetQueue;

        // הפגיעה הראשונה – לחישוב כיוון
        private GridLocation _firstHit;

        // האם נעלנו כיוון (אחרי פגיעה שנייה)
        private bool _directionLocked;
        private (int dr, int dc) _lockedDirection;

        // תאים זמינים לחיפוש אקראי
        private readonly List<GridLocation> _availableCells;

        private readonly Random _random;

        public AIPlayer()
        {
            _mode = AIMode.Hunt;
            _targetQueue = new Queue<GridLocation>();
            _random = new Random();
            _availableCells = new List<GridLocation>();

            // בניית רשת שחמט – סכום שורה+עמודה זוגי
            // מכסה את כל הלוח ביעילות מקסימלית
            for (int r = 0; r < BoardSize; r++)
                for (int c = 0; c < BoardSize; c++)
                    if ((r + c) % 2 == 0)
                        _availableCells.Add(new GridLocation(r, c));

            Shuffle(_availableCells);
        }

        //פעולה ראשית 

        /// <summary>
        /// מחליט על הירייה הבאה של ה-AI.
        /// </summary>
        public GridLocation DecideNextShot(Board opponentBoard)
        {
            if (_mode == AIMode.Target)
                return DecideTargetShot(opponentBoard);
            else
                return DecideHuntShot(opponentBoard);
        }


        /// מעדכן את ה-AI לאחר ירייה.
        /// חייבים לקרוא לזה אחרי כל ירייה של ה-AI.

        public void ProcessShotResult(GridLocation loc, ShotResult result)
        {
            RemoveFromAvailable(loc);

            switch (result)
            {
                case ShotResult.Miss:
                    // פספוס במצב ציד עם כיוון נעול – ינסה הפוך
                    if (_mode == AIMode.Target && _directionLocked)
                        TryReverseDirection();
                    break;

                case ShotResult.Hit:
                    HandleHit(loc);
                    break;

                case ShotResult.Sunk:
                    // צוללת טבעה – יחזור לחיפוש
                    ResetToHunt();
                    break;
            }
        }

        //  מצב חיפוש 

        private GridLocation DecideHuntShot(Board board)
        {
            _availableCells.RemoveAll(loc =>
                !board.CanShootAt(loc.GetRow(), loc.GetColumn()));

            if (_availableCells.Count > 0)
                return _availableCells[0];

            // גיבוי: סרוק את כל הלוח
            for (int r = 0; r < BoardSize; r++)
                for (int c = 0; c < BoardSize; c++)
                    if (board.CanShootAt(r, c))
                        return new GridLocation(r, c);

            throw new InvalidOperationException("אין תאים זמינים לירייה.");
        }

        //  מצב ציד

        private GridLocation DecideTargetShot(Board board)
        {
            while (_targetQueue.Count > 0)
            {
                var next = _targetQueue.Dequeue();
                if (board.CanShootAt(next.GetRow(), next.GetColumn()))
                    return next;
            }

            // התור ריק אבל יש פגיעה ידועה  ינסה את כל הכיוונים מחדש
            if (_firstHit != null)
            {
                _directionLocked = false;
                AddAdjacentToQueue(_firstHit);

                while (_targetQueue.Count > 0)
                {
                    var next = _targetQueue.Dequeue();
                    if (board.CanShootAt(next.GetRow(), next.GetColumn()))
                        return next;
                }
            }

            // אין מה לנסות – חזור לחיפוש
            ResetToHunt();
            return DecideHuntShot(board);
        }

        private void HandleHit(GridLocation loc)
        {
            _mode = AIMode.Target;

            if (_firstHit == null)
            {
                // פגיעה ראשונה – הוסף 4 כיוונים
                _firstHit = loc;
                _directionLocked = false;
                AddAdjacentToQueue(loc);
            }
            else if (!_directionLocked)
            {
                // פגיעה שנייה – נעל כיוון
                _directionLocked = true;
                _lockedDirection = (
                    loc.GetRow() - _firstHit.GetRow(),
                    loc.GetColumn() - _firstHit.GetColumn()
                );

                _targetQueue.Clear();
                EnqueueInDirection(loc, _lockedDirection);
            }
            else
            {
                // פגיעה נוספת – המשך בכיוון הנעול
                EnqueueInDirection(loc, _lockedDirection);
            }
        }

        private void AddAdjacentToQueue(GridLocation loc)
        {
            int r = loc.GetRow();
            int c = loc.GetColumn();
            TryEnqueue(r - 1, c);   // מעלה
            TryEnqueue(r + 1, c);   // מטה
            TryEnqueue(r, c - 1);   // שמאל
            TryEnqueue(r, c + 1);   // ימין
        }

        private void EnqueueInDirection(GridLocation from, (int dr, int dc) dir)
        {
            TryEnqueue(from.GetRow() + dir.dr, from.GetColumn() + dir.dc);
        }


        /// כשנחסמנו בכיוון – נסה את הכיוון ההפוך מהפגיעה הראשונה.

        private void TryReverseDirection()
        {
            if (_firstHit == null) return;

            _targetQueue.Clear();
            var reversed = (-_lockedDirection.dr, -_lockedDirection.dc);
            EnqueueInDirection(_firstHit, reversed);
        }

        //  עזרים

        private void TryEnqueue(int r, int c)
        {
            if (r >= 0 && r < BoardSize && c >= 0 && c < BoardSize)
                _targetQueue.Enqueue(new GridLocation(r, c));
        }

        private void RemoveFromAvailable(GridLocation loc)
        {
            _availableCells.RemoveAll(
                l => l.GetRow() == loc.GetRow() && l.GetColumn() == loc.GetColumn());
        }

        private void ResetToHunt()
        {
            _mode = AIMode.Hunt;
            _firstHit = null;
            _directionLocked = false;
            _targetQueue.Clear();
        }

        private void Shuffle(List<GridLocation> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}