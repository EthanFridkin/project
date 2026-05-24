using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Battleship.Core.Models;
namespace Battleship.Core.Logic
{
    public class FleetFactory
    {
        public static List<Submarine> CreateDefaultFleet()
        {
            return new List<Submarine>
            {
                new Submarine(1, new GridLocation(0, 0), 4, Orientation.Horizontal),
                new Submarine(2, new GridLocation(0, 0), 3, Orientation.Horizontal),
                new Submarine(3, new GridLocation(0, 0), 2, Orientation.Horizontal),
                new Submarine(4, new GridLocation(0, 0), 3, Orientation.Vertical),
                new Submarine(5, new GridLocation(0, 0), 2, Orientation.Vertical),
            };
        }
    }
}
