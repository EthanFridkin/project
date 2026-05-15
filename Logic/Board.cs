using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Battleship.Core.Models;
namespace Battleship.Core.Logic
{
    /// <summary>
    /// מייצג לוח משחק של שחקן אחד.
    /// אחראי על:
    ///   1. הנחת צוללות (עם בדיקת תקינות)
    ///   2. קבלת יריות ועדכון מצב
    ///   3. בדיקה האם כל הצוללות הושמדו
    /// </summary>
    public class Board
    {
        private const int Size = 10;

        private readonly BoardCell[,] _cells;
        private readonly List<Submarine> _submarines;
        private readonly Dictionary<int, int> _hitCount;

        public Board()
        {
            _cells = new BoardCell[Size, Size];
            _submarines = new List<Submarine>();
            _hitCount = new Dictionary<int, int>();

            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    _cells[r, c] = new BoardCell();
        }

        public bool IsValidPlacement(Submarine sub)
        {
            int row = sub.location.GetRow();
            int col = sub.location.GetColumn();

            for (int i = 0; i < sub.Length; i++)
            {
                int r = row + (sub.Orientation == Orientation.Vertical ? i : 0);
                int c = col + (sub.Orientation == Orientation.Horizontal ? i : 0);

                if (r < 0 || r >= Size || c < 0 || c >= Size)
                    return false;

                if (_cells[r, c].HasSubmarine)
                    return false;
            }

            return true;
        }

        public bool PlaceSubmarine(Submarine sub)
        {
            if (!IsValidPlacement(sub))
                return false;

            int row = sub.location.GetRow();
            int col = sub.location.GetColumn();

            for (int i = 0; i < sub.Length; i++)
            {
                int r = row + (sub.Orientation == Orientation.Vertical ? i : 0);
                int c = col + (sub.Orientation == Orientation.Horizontal ? i : 0);

                _cells[r, c].PlaceSubmarine(sub.SubmarineId);
            }

            _submarines.Add(sub);
            _hitCount[sub.SubmarineId] = 0;

            return true;
        }

        public ShotResult ReceiveShot(GridLocation loc)
        {
            int row = loc.GetRow();
            int col = loc.GetColumn();

            if (row < 0 || row >= Size || col < 0 || col >= Size)
                throw new ArgumentOutOfRangeException("מיקום הירייה מחוץ לגבולות הלוח.");

            if (_cells[row, col].WasShot)
                throw new InvalidOperationException("כבר ירו על תא זה.");

            bool isHit = _cells[row, col].ReceiveShot();

            if (!isHit)
                return ShotResult.Miss;

            int subId = _cells[row, col].SubmarineId;
            _hitCount[subId]++;

            Submarine sub = _submarines.Find(s => s.SubmarineId == subId);

            if (_hitCount[subId] >= sub.Length)
            {
                sub.IsDestroyed = true;
                return ShotResult.Sunk;
            }

            return ShotResult.Hit;
        }

        public bool AreAllSubmarinesDestroyed()
        {
            foreach (var sub in _submarines)
                if (!sub.IsDestroyed)
                    return false;

            return true;
        }

        public BoardCell GetCell(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                throw new ArgumentOutOfRangeException("מיקום מחוץ לגבולות הלוח.");

            return _cells[row, col];
        }

        public bool CanShootAt(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                return false;

            return !_cells[row, col].WasShot;
        }

        /// <summary>
        /// מחזיר את כל מיקומי התאים של צוללת מסוימת לפי ID.
        /// משמש לחשיפת הצוללת כולה כאשר היא טובעת.
        /// </summary>
        public List<(int row, int col)> GetSubmarineCells(int submarineId)
        {
            var cells = new List<(int, int)>();

            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (_cells[r, c].SubmarineId == submarineId)
                        cells.Add((r, c));

            return cells;
        }

        /// <summary>
        /// מחזיר את ה-ID של הצוללת בתא מסוים.
        /// -1 אם אין צוללת.
        /// </summary>
        public int GetSubmarineIdAt(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                return -1;

            return _cells[row, col].SubmarineId;
        }

        public int GetSize() => Size;
    }

    public enum ShotResult
    {
        Miss,
        Hit,
        Sunk
    }
}
