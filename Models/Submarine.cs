using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    public class Submarine
    {
        public int SubmarineId;
        public GridLocation location;
        public int Length;            /* שיניתי את התכונות של הצוללת ל- public כדי שיהיה אפשר לגשת אליהם מחוץ למחלקה, כמו ב- FleetFactory.cs*/
        public Orientation Orientation;
        public bool IsDestroyed;

        public Submarine(int id, GridLocation loc, int length, Orientation orientation)
        {
            this.SubmarineId = id;
            this.location = loc;
            this.Length = length;
            this.Orientation = orientation;
            this.IsDestroyed = false;
        }

        public void SetLocation(GridLocation loc)
        {
            this.location = loc;
        }

        public void SetOrientation(Orientation orientation)
        {
            this.Orientation = orientation;
        }
    }
}
