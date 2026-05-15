using Battleship.Core.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    /// <summary>
    /// מייצג ירייה בודדת במשחק.
    /// 
    /// תיקון מהגרסה הקודמת:
    /// כל השדות היו private ללא getters –
    /// עכשיו הכל חשוף דרך Properties.
    /// </summary>
    public class Shot
    {
        /// <summary>מספר מזהה ייחודי של הירייה.</summary>
        public int ShotId { get; private set; }

        /// <summary>המיקום שאליו נורתה הירייה.</summary>
        public GridLocation TargetLocation { get; private set; }

        /// <summary>האם הירייה פגעה בצוללת.</summary>
        public bool IsHit { get; private set; }

        /// <summary>האם הירייה גרמה לטביעת צוללת.</summary>
        public bool IsSunk { get; private set; }

        /// <summary>ה-ID של השחקן שירה. חשוב לסנכרון Firebase.</summary>
        public int ShooterId { get; private set; }

        /// <summary>
        /// יוצר ירייה חדשה.
        /// </summary>
        public Shot(int id, GridLocation location, ShotResult result, int shooterId)
        {
            ShotId = id;
            TargetLocation = location;
            ShooterId = shooterId;

            // המרת ShotResult לשדות ברורים
            IsHit = result == ShotResult.Hit || result == ShotResult.Sunk;
            IsSunk = result == ShotResult.Sunk;
        }
    }
}
