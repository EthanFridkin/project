using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    public class PlayerStatus
    {
        private int PlayerId;
        private int Hit;
        private int Miss;


        public PlayerStatus(int playerId)
        {
            this.PlayerId = playerId;
            this.Hit = 0;
            this.Miss = 0;
        }

        public void UpdateHit()
        {
            this.Hit += 1;
        }   
        public void UpdateMiss()
        {
            this.Miss += 1;
        }
    }
}
