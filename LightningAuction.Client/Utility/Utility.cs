using Bbh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LightningAuction.Utility
{
    public static class Utility
    {
        public static int DateTimeToUnix(DateTime dateTime)
        {
            return (Int32)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public static DateTime UnixTimeToDateTime(Int32 unixTime)
        {
            return new DateTime(1970, 1, 1).AddSeconds(unixTime);
        }

        public static Player[] SortPlayersBounty(Player[] players, bool reverse)
        {
            var comparer = new PlayerComparer(PlayerComparer.SortBy.Bounty, reverse);
            Array.Sort(players, comparer);
            return players;
        }
        public static Player[] SortPlayersKill(Player[] players, bool reverse)
        {
            var comparer = new PlayerComparer(PlayerComparer.SortBy.Kills, reverse);
            Array.Sort(players, comparer);
            return players;
        }
        public static Player[] SortPlayersDeaths(Player[] players, bool reverse)
        {
            var comparer = new PlayerComparer(PlayerComparer.SortBy.Deaths, reverse);
            Array.Sort(players, comparer);
            return players;
        }

        public static Player[] SortPlayers(Player[] players, PlayerComparer.SortBy sortBy, bool reverse)
        {
            var comparer = new PlayerComparer(sortBy, reverse);
            Array.Sort(players, comparer);
            return players;
        }
    }

    public class PlayerComparer : IComparer<Player>
    {
        public PlayerComparer(SortBy sortby, bool reverse)
        {
            compareField = sortby;
            this.reverse = reverse;
        }
        public enum SortBy
        {
            Bounty,
            Kills,
            Deaths
        }

        private SortBy compareField;
        private bool reverse;

        public int Compare(Player x, Player y)
        {
            if (!reverse)
            {

                switch (compareField)
                {
                    case SortBy.Bounty:
                        return x.CurrentBounty.CompareTo(y.CurrentBounty);
                    case SortBy.Kills:
                        return x.CurrentKills.CompareTo(y.CurrentKills);
                    case SortBy.Deaths:
                        return x.CurrentDeaths.CompareTo(y.CurrentDeaths);
                }
            }
            else
            {
                switch (compareField)
                {
                    case SortBy.Bounty:
                        return y.CurrentBounty.CompareTo(x.CurrentBounty);
                    case SortBy.Kills:
                        return y.CurrentKills.CompareTo(x.CurrentKills);
                    case SortBy.Deaths:
                        return y.CurrentDeaths.CompareTo(x.CurrentDeaths);
                }
            }
            return x.CurrentBounty.CompareTo(y.CurrentBounty);
        }
    }
}
