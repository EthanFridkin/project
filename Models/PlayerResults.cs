using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battleship.Core.Models
{
    public class PlayerResults
    {
        private int PlayerId;
        private int GamePlayed;
        private int wins;
        private int losses;
        private int totalMoves;
        private int TotalHits;
        private int TotalMisses;

        public PlayerResults(int playerId, int gameId)
        {
            this.PlayerId = playerId;
            this.GamePlayed = gameId;
            this.wins = 0;
            this.losses = 0;
            this.totalMoves = 0;
            this.TotalHits = 0;
            this.TotalMisses = 0;
        }

        public void UpdateWins()
        {
            this.wins += 1;
        }
        public void UpdateLosses()
        {
            this.losses += 1;
        }
        public void UpdateTotalMoves(int moves)
        {
            this.totalMoves += moves;
        }
        public void UpdateTotalHits(int hits)
        {
            this.TotalHits += hits;
        }
        public void UpdateTotalMisses(int misses)
        {
            this.TotalMisses += misses;
        }

    }
}
