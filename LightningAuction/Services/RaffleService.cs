using LightningAuction.Models;
using Lnrpc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LightningAuction.Services
{
    public class RaffleService : IRaffleService
    {
        private readonly ILndService _lndService;
        public RaffleService(ILndService lndService)
        {
            _lndService = lndService;
            _lndService.AddInvoicePaidEventHandler(InvoicePaid);
        }
        private async void InvoicePaid(Invoice invoice)
        {
            var raffleEntyInvoice = JsonSerializer.Deserialize<RaffleInvoice>(invoice.Memo);
            if (raffleEntyInvoice.Amount == 0)
                return;
            await AddRaffleEntry(raffleEntyInvoice.RaffleId, raffleEntyInvoice.Amount, raffleEntyInvoice.Memo);
        }

        public async Task<Raffle> StartRaffle()
        {
            var raffle = new Raffle()
            {
                FinishedAt = -1,
                StartedAt = DateTime.UtcNow.ToFileTimeUtc(),
                RaffleEntries = new List<RaffleEntry>()
            };
            using (var context = new AuctionContext())
            {
                raffle = context.Raffles.Add(raffle).Entity;
                await context.SaveChangesAsync();
            }
            return raffle;
        }

        public async Task<string> GetRaffleInvoice(string raffleId, string description, long amount)
        {
            var raffle = GetRaffle(raffleId);
            if (raffle == null)
                return null;
            var raffleEntry = new RaffleInvoice
            {
                Amount = amount,
                Memo = description,
                RaffleId = raffle.Id.ToString(),
            };
            var raffleEntryJson = JsonSerializer.Serialize<RaffleInvoice>(raffleEntry);
            var invoice = await _lndService.AddInvoice(raffleEntryJson, amount);
            return invoice;
        }

        public async Task<List<RaffleEntry>> ListRaffleEntries(string raffleId)
        {
            var raffle = GetRaffle(raffleId);
            if (raffle == null)
                return null;
            return raffle.RaffleEntries;
        }

        public async Task<Raffle> EndRaffle(string raffleId)
        {
            var raffle = GetRaffle(raffleId);
            if (raffle == null)
                return null;
            raffle.FinishedAt = DateTime.UtcNow.ToFileTimeUtc();
            using (var context = new AuctionContext())
            {
                context.Raffles.Update(raffle);
                await context.SaveChangesAsync();
            }
            return raffle;
        }

        public async Task<RaffleEntry> AddRaffleEntry(string raffleId, long amount, string description)
        {
            var raffle = GetRaffle(raffleId);
            if (raffle == null)
                return null;
            if (raffle.FinishedAt != -1)
                return null;
            var raffleEntry = new RaffleEntry
            {
                Amount = amount,
                Memo = description,
                Raffle = raffle,
                RaffleId = raffle.Id,
            };
            raffle.RaffleEntries.Add(raffleEntry);
            using (var context = new AuctionContext())
            {
                context.Raffles.Update(raffle);
                raffleEntry = context.RaffleEntries.Add(raffleEntry).Entity;
                await context.SaveChangesAsync();
            }
            return raffleEntry;
        }

        public Raffle GetRaffle(string raffleId)
        {

            Raffle raffle;
            using (var context = new AuctionContext())
            {
                raffle = context.Raffles.Include(r => r.RaffleEntries).FirstOrDefault(r => r.Id == Guid.Parse(raffleId));
                if (raffle == null)
                    return null;

            }

            return raffle;
        }
    }
}
