using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    public class GridLocation
    {
        private int row;
        private int column;

        public GridLocation(int row, int column)
        {
            this.row = row;
            this.column = column;
        }

        public int GetRow()
        {
            return this.row;
        }

        public int GetColumn()
        {
            return this.column;
        }
    }
}
