using LightningAuction.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LightningAuction.Models
{
    

    [Serializable]
    public class Raffle
    {
        public Guid Id { get; set; }
        public long StartedAt { get; set; }
        public long FinishedAt { get; set; }
        public List<RaffleEntry> RaffleEntries { get; set; }

    }
    [Serializable]
    public class RaffleEntry
    {
        public Guid Id { get; set; }
        public long Amount { get; set; }
        public string Memo { get; set; }
        public Raffle Raffle { get; set; }
        public Guid RaffleId { get; set; }
    }
    [Serializable]
    public struct RaffleInvoice
    {
        public string RaffleId { get; set; }
        public string Memo { get; set; }
        public long Amount { get; set; }
    }
}
