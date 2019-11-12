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
        Task<Auction> AbortAuction(Guid auctionId);
        Task<Auction> EndAuction(Guid auctionId);
        Task<Auction[]> GetAllAuctions(bool onlyFinished, bool onlyActive);
        Auction GetAuction(Guid auctionId);
        Task<AuctionInvoice> GetWinningEntry(Guid auctionId);
        Task<AuctionEntry> RequestAuctionEntryInvoice(Guid auctionId, long amount, string winningMessage);
        Task<Auction> StartAuction(int duration);
        Task<AuctionEntry> CancelAuctionEntry(Guid entryId);
        Task<AuctionEntry> GetBid(Guid entryId);
        Task<Auction> GetActiveAuction();
    }

    public class AuctionService : IAuctionService
    {

        private readonly ILndService _lndService;
        public AuctionService( ILndService lndService)
        {

            _lndService = lndService;
            _lndService.AddHoldInvoiceListener(LndService_OnHoldInvoiceActivated);
            Task.Run(async () => await CleanUpEntries());
        }

        private async Task CleanUpEntries()
        {
            using(var context = new AuctionContext())
            {
                var entries = context.AuctionEntries.Where(ae => ae.State == AuctionEntryState.ACTIVATED);
                foreach(var entry in entries)
                {
                    var auction = GetAuction(entry.AuctionId);
                    if(auction.FinishedAt != 0)
                    {
                        await CancelAuctionEntry(entry);
                    }


                }
            }
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
        public async Task<Auction> EndAuction(Guid auctionId)
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

        public async Task<Auction> AbortAuction(Guid auctionId)
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

        public async Task<Auction> GetActiveAuction()
        {
            var auctions = await GetAllAuctions(false, true);
            if (auctions.Length < 1)
                return null;
            return auctions[0];
        }


        public async Task<AuctionEntry> RequestAuctionEntryInvoice(Guid auctionId, long amount, string winningMessage)
        {
            var auction = GetAuction(auctionId);
            if (auction == null)
                return null;
            var guid = Guid.NewGuid();
            var auctionInvoice = new AuctionInvoice
            {
                Amount = amount,
                AuctionId = auction.Id.ToString(),
                WinningMessage = winningMessage,
                AuctionEntryId = guid.ToString(),
            };
            
            var description = JsonSerializer.Serialize<AuctionInvoice>(auctionInvoice);
            var expiry = (auction.Duration + auction.StartedAt) - Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
            if (expiry < 1)
                return null;
            var invoice = await _lndService.GetHoldInvoice(amount, description, expiry);
            var auctionEntry = new AuctionEntry
            {
                Id = guid,
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
        public async Task<AuctionInvoice> GetWinningEntry(Guid auctionId)
        {
            Auction auction;
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Include(r => r.WinningEntry).FirstOrDefault(r => r.Id == auctionId);
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

            Console.WriteLine("In Hodle Activated {0}", invoice);
            try
            {
                var auctionInvoice = JsonSerializer.Deserialize<AuctionInvoice>(invoice.Memo);
                using (var context = new AuctionContext())
                {
                    var auctionEntry = context.AuctionEntries.FirstOrDefault(e => e.PaymentRequest == invoice.PaymentRequest);
                    var auction = GetAuction(Guid.Parse(auctionInvoice.AuctionId));              
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
            }catch(Exception e)
            {

                Console.WriteLine("ERROR AT HOLD INVOICE ACTIVATED {0}", invoice);

                await _lndService.CancelHodlInvoice(invoice.RHash.ToByteArray());
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
        public Auction GetAuction(Guid auctionId)
        {

            Auction auction;
            using (var context = new AuctionContext())
            {
                auction = context.Auctions.Include(r => r.AuctionEntries).FirstOrDefault(r => r.Id == auctionId);
                if (auction == null)
                    return null;
            }

            return auction;
        }

        public async Task<AuctionEntry> CancelAuctionEntry(Guid entryId)
        {
            AuctionEntry auctionEntry;
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.FirstOrDefault(r => r.Id == entryId);
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
        public async Task<AuctionEntry> GetBid(Guid entryId)
        {
            AuctionEntry auctionEntry;
            using (var context = new AuctionContext())
            {
                auctionEntry = context.AuctionEntries.FirstOrDefault(r => r.Id == entryId);
                if (auctionEntry == null)
                    return null;
            }
            return auctionEntry;
        }
    }



}
