using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    /// <summary>
    /// מייצג תא בודד בלוח המשחק.
    /// כל תא יודע:
    ///   1. האם יש בו צוללת (ואיזו)
    ///   2. האם כבר ירו עליו
    ///   3. מה התוצאה של הירייה (פגיעה / פספוס)
    /// </summary>
    public class BoardCell
    {
        /// <summary>
        /// האם יש צוללת בתא זה.
        /// נקבע כאשר מניחים צוללת על הלוח.
        /// </summary>
        public bool HasSubmarine { get; private set; }

        /// <summary>
        /// ה-ID של הצוללת שנמצאת בתא.
        /// -1 אם אין צוללת.
        /// </summary>
        public int SubmarineId { get; private set; }

        /// <summary>
        /// האם כבר ירו על תא זה.
        /// לא ניתן לירות על אותו תא פעמיים.
        /// </summary>
        public bool WasShot { get; private set; }

        /// <summary>
        /// האם הירייה על תא זה פגעה בצוללת.
        /// רלוונטי רק אם WasShot == true.
        /// </summary>
        public bool IsHit { get; private set; }

        public BoardCell()
        {
            HasSubmarine = false;
            SubmarineId = -1;
            WasShot = false;
            IsHit = false;
        }

        /// <summary>
        /// מניח צוללת על תא זה.
        /// נקרא מ-Board.PlaceSubmarine בלבד.
        /// </summary>
        public void PlaceSubmarine(int submarineId)
        {
            HasSubmarine = true;
            SubmarineId = submarineId;
        }

        /// <summary>
        /// מעבד ירייה על תא זה.
        /// מחזיר true אם פגע בצוללת, false אם פספס.
        /// זורק חריגה אם ירו על התא כבר.
        /// </summary>
        public bool ReceiveShot()
        {
            if (WasShot)
                throw new InvalidOperationException("כבר ירו על תא זה.");

            WasShot = true;
            IsHit = HasSubmarine;

            return IsHit;
        }
    }
}
