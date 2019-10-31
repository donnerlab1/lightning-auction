using Lnrpc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace LightningAuction.Models
{

    [Serializable]
    public class AuctionEntry : IComparable<AuctionEntry>
    {
        public Invoice invoice;
        public long amount;
        public DateTime ActivatedAt;

        public int CompareTo([AllowNull] AuctionEntry other)
        {
            if(other.amount > this.amount)
            {
                return 1;
            } else if(other.amount<this.amount)
            {
                return 0;
            } else
            {
                return DateTime.Compare(this.ActivatedAt, other.ActivatedAt);
            }

            
        }
    }
    [Serializable]
    public struct InvoiceData
    {
        
        public string memo;
        
    }
}
