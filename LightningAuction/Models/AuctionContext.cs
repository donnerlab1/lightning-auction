using LightningAuction.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LightningAuction.Models
{
    public class AuctionContext : DbContext
    {
        public DbSet<Raffle> Raffles { get; set; }
        public DbSet<RaffleEntry> RaffleEntries { get; set; }
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<AuctionEntry> AuctionEntries { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=auctions.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<Auction>().HasMany<AuctionEntry>(a => a.AuctionEntries).WithOne(e => e.AuctionId);
        }
    }

    [Serializable]
    public class Auction
    {
        public Guid Id { get; set; }
        public Int32 StartedAt { get; set; }
        public Int32 FinishedAt { get; set; }
        public Int32 Duration{ get; set; }

        public string WinningEntry { get; set; }
        public List<AuctionEntry> AuctionEntries { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    [Serializable]
    public class AuctionEntry : IComparable<AuctionEntry>
    {
        public Guid Id { get; set; }
        public AuctionEntryState State { get; set; }

        public Int32 CreatedAt { get; set; }
        public Int32 ActivatedAt { get; set; }
        public Int32 SettledAt { get; set; }

        public Int32 CanceledAt { get; set; }
        public string PaymentRequest { get; set; }
        public byte[] PaymentHash { get; set; }
        public byte[] Preimage { get; set; }
        public long Amount { get; set; }
        public string Description { get; set; }
        public string Message { get; set; }
        public Guid AuctionId { get; set; }


        public int CompareTo([AllowNull] AuctionEntry other)
        {
            if (other.Amount > this.Amount)
            {
                return 1;
            }
            else if (other.Amount < this.Amount)
            {
                return 0;
            }
            else
            {
                return DateTime.Compare(DateTime.FromFileTimeUtc(this.ActivatedAt), DateTime.FromFileTimeUtc(other.ActivatedAt));
            }


        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    [Serializable]
    public enum AuctionEntryState
    {
        CREATED,
        ACTIVATED,
        CANCELED,
        SETTLED
    }
    [Serializable]
    public class AuctionInvoice
    {
        public string AuctionId { get; set; }
        public long Amount { get; set; }
        public string WinningMessage { get; set; }

        public string AuctionEntryId { get; set; }
    }
}
