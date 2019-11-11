using Lnrpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LightningAuction.Models;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Grpc.Core.Logging;
using LightningAuction.Utility;

namespace LightningAuction.Services
{
    public interface IAuctionService
    {
        Task<Auction> AbortAuction(string auctionId);
        Task<Auction> EndAuction(string auctionId);
        Task<Auction[]> GetAllAuctions(bool onlyFinished, bool onlyActive);
        Auction GetAuction(string auctionId);
        Task<AuctionInvoice> GetWinningEntry(string auctionId);
        Task<AuctionEntry> RequestAuctionEntryInvoice(string auctionId, long amount, string winningMessage);
        Task<Auction> StartAuction(int duration);
        Task<AuctionEntry> CancelAuctionEntry(string entryId);
        Task<AuctionEntry> GetBid(string entryId);
    }

    public class AuctionService : IAuctionService
    {

        private readonly ILndService _lndService;
        public AuctionService( ILndService lndService)
        {

            _lndService = lndService;
            _lndService.AddHoldInvoiceListener(LndService_OnHoldInvoiceActivated);
        }

        public async Task<Auction> StartAuction(int duration)
        {
            var now = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
            var auction = new Auction()
            {
                StartedAt = now,
                Duration = duration

            };
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Add(auction).Entity;
                await context.SaveChangesAsync();
            }

            Console.WriteLine("starting auction {0}", auction);
            return auction;
        }
        public async Task<Auction> EndAuction(string auctionId)
        {

            var auction = GetAuction(auctionId);
            if (auction == null)
                return null;

            Console.WriteLine("ending auction {0}", auction);
            await HandleAuctionEnd(auction);
            await HandleAuctionEntries(auction);
            auction.FinishedAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
            await UpdateAuction(auction);

            Console.WriteLine("succesfully ended auction {0}", auction);
            return auction;

        }

        public async Task<Auction> AbortAuction(string auctionId)
        {

            var auction = GetAuction(auctionId);
            if (auction == null)
                return null;

            Console.WriteLine("aborting auction {0}", auction);
            if (auction.AuctionEntries != null && auction.AuctionEntries.Count > 0)
            {
                auction.AuctionEntries.RemoveAll(e => e.State != AuctionEntryState.ACTIVATED);
                if (auction.AuctionEntries.Count > 0)
                {
                    foreach (var entry in auction.AuctionEntries)
                    {
                        await CancelAuctionEntry(entry);
                    }
                }
            }
            auction.FinishedAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
            await UpdateAuction(auction);

            Console.WriteLine("succesfully aborted auction {0}", auction);
            return auction;
        }

        public async Task<Auction[]> GetAllAuctions(bool onlyFinished, bool onlyActive)
        {
            using (var context = new AuctionContext())
            {
                Auction[] auctions;
                if (onlyFinished)
                    auctions = await context.Auctions.Where(a => a.FinishedAt != 0).ToArrayAsync();
                else if (onlyActive)
                    auctions = await context.Auctions.Where(a => a.FinishedAt == 0).ToArrayAsync();
                else
                    auctions = await context.Auctions.ToArrayAsync();
                return auctions;
            }
        }


        public async Task<AuctionEntry> RequestAuctionEntryInvoice(string auctionId, long amount, string winningMessage)
        {
            var auction = GetAuction(auctionId);
            if (auction == null)
                return null;
            var auctionInvoice = new AuctionInvoice
            {
                Amount = amount,
                AuctionId = auction.Id.ToString(),
                WinningMessage = winningMessage
            };
            var description = JsonSerializer.Serialize<AuctionInvoice>(auctionInvoice);
            var invoice = await _lndService.GetHoldInvoice(amount, description);
            var auctionEntry = new AuctionEntry
            {
                State = AuctionEntryState.CREATED,
                Amount = amount,
                Description = description,
                AuctionId = auction.Id,
                PaymentHash = invoice.paymentHash,
                Preimage = invoice.preImage,
                PaymentRequest = invoice.payreq,
                Message = winningMessage,
                CreatedAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow)
        };
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.Add(auctionEntry).Entity;
                await context.SaveChangesAsync();
            }

            Console.WriteLine("created auction entry {0}", auctionEntry);
            return auctionEntry;
        }
        public async Task<AuctionInvoice> GetWinningEntry(string auctionId)
        {
            Auction auction;
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Include(r => r.WinningEntry).FirstOrDefault(r => r.Id == Guid.Parse(auctionId));
                if (auction == null)
                    return new AuctionInvoice();
                return JsonSerializer.Deserialize<AuctionInvoice>(auction.WinningEntry);
            }

        }

        private async Task<Auction> HandleAuctionEnd(Auction auction)
        {
            if (auction.AuctionEntries != null && auction.AuctionEntries.Count > 0)
            {
                auction.AuctionEntries.RemoveAll(e => e.State != AuctionEntryState.ACTIVATED);
                if (auction.AuctionEntries.Count > 0)
                {
                    auction.AuctionEntries.Sort();
                    var winningEntry = auction.AuctionEntries[0];
                    auction.WinningEntry = winningEntry.Description;
                }

            }

            return await UpdateAuction(auction);
        }

        private async Task<Auction> HandleAuctionEntries(Auction auction)
        {
            for (int i = 0; i < auction.AuctionEntries.Count; i++)
            {
                if (auction.AuctionEntries[i].State != AuctionEntryState.ACTIVATED)
                    continue;
                if (i == 0)
                {
                    await SettleAuctionEntry(auction.AuctionEntries[i]);
                }
                else
                {
                    await CancelAuctionEntry(auction.AuctionEntries[i]);
                }

            }

            return auction;
        }
        private async Task CancelAuctionEntry(AuctionEntry auctionEntry)
        {

            if (await _lndService.CancelHodlInvoice(auctionEntry.PaymentHash))
            {

                auctionEntry.CanceledAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
                auctionEntry.State = AuctionEntryState.CANCELED;
                await UpdateAuctionEntry(auctionEntry);
            }
        }

        private async Task SettleAuctionEntry(AuctionEntry auctionEntry)
        {

            if (await _lndService.SettleHodlInvoice(auctionEntry.Preimage))
            {
                auctionEntry.SettledAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
                auctionEntry.State = AuctionEntryState.SETTLED;
                await UpdateAuctionEntry(auctionEntry);
            }
        }

        private async void LndService_OnHoldInvoiceActivated(object sender, Invoice invoice, byte[] preImage)
        {
            var auctionInvoice = JsonSerializer.Deserialize<AuctionInvoice>(invoice.Memo);
            using (var context = new AuctionContext())
            {
                var auctionEntry = context.AuctionEntries.FirstOrDefault(e => e.PaymentRequest == invoice.PaymentRequest);
                if (auctionEntry == null)
                    return;

                var auction = GetAuction(auctionInvoice.AuctionId);
                if (auction == null)
                    return;
                if (auction.FinishedAt != 0)
                {
                    await _lndService.CancelHodlInvoice(invoice.RHash.ToByteArray());
                    return;
                }
                    
                auctionEntry.ActivatedAt = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
                auctionEntry.State = AuctionEntryState.ACTIVATED;
                context.AuctionEntries.Update(auctionEntry);

                Console.WriteLine("activated auction entry {0}", auctionEntry);
                await context.SaveChangesAsync();
            }
        }

        private async Task<Auction> UpdateAuction(Auction auction)
        {
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Update(auction).Entity;
                await context.SaveChangesAsync();
            }
            return auction;
        }
        private async Task<AuctionEntry> UpdateAuctionEntry(AuctionEntry auctionEntry)
        {
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.Update(auctionEntry).Entity;
                await context.SaveChangesAsync();
            }
            return auctionEntry;
        }
        public Auction GetAuction(string auctionId)
        {

            Auction auction;
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Include(r => r.AuctionEntries).FirstOrDefault(r => r.Id == Guid.Parse(auctionId));
                if (auction == null)
                    return null;
            }

            return auction;
        }

        public async Task<AuctionEntry> CancelAuctionEntry(string entryId)
        {
            AuctionEntry auctionEntry;
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.FirstOrDefault(r => r.Id == Guid.Parse(entryId));
                if (auctionEntry == null)
                    return null;
            }
            var res = await _lndService.CancelHodlInvoice(auctionEntry.PaymentHash);
            if (!res)
                return null;
            auctionEntry.State = AuctionEntryState.CANCELED;
            await UpdateAuctionEntry(auctionEntry);
            return auctionEntry;
        }
        public async Task<AuctionEntry> GetBid(string entryId)
        {
            AuctionEntry auctionEntry;
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.FirstOrDefault(r => r.Id == Guid.Parse(entryId));
                if (auctionEntry == null)
                    return null;
            }
            return auctionEntry;
        }
    }



}
