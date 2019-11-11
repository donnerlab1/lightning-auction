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
    }
}
