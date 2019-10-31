using Lnrpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LightningAuction.Models;

namespace LightningAuction.Services
{
    public interface IAuctionService
    {
        Task EndAuction();
        List<AuctionEntry> GetAuctionEntries();
        Task<string> RequestAuctionEntry(long amount, string text);
        void StartAuction();
        string GetWinningMessage();
        bool AuctionFinished();
    }

    public class AuctionService : IAuctionService
    {

        private readonly ILndService lndService;


        public string WinnerMessage;
        private Auction currentAuction;
        private Auction lastAuction;
        public AuctionService(ILndService lndService)
        {
            this.lndService = lndService;
            WinnerMessage = "";
            lndService.AddHoldInvoiceListener(LndService_OnHoldInvoiceActivated);
            StartAuction();
            
        }

        public async Task<string> RequestAuctionEntry(long amount, string text)
        {
            return await lndService.GetHoldInvoice(amount, text);
        }

        private void LndService_OnHoldInvoiceActivated(object sender, Invoice invoice)
        {
            Console.WriteLine(" ADDING INVOICE TO ACCEPTED " + invoice.Memo);
            currentAuction.AddEntry(new AuctionEntry { amount = invoice.Value, invoice = invoice, ActivatedAt = DateTime.Now });
        }

        public void StartAuction()
        {
            currentAuction = new Auction();
        }

        public async Task EndAuction()
        {
            if (currentAuction == null)
                return;
            lastAuction = currentAuction;
            lastAuction.CloseAuction();
            await lndService.SettleHodlInvoice(lastAuction.winningEntry.invoice);
            lastAuction.Entries.Remove(lastAuction.winningEntry);
            foreach (var entry in lastAuction.Entries)
            {
               await lndService.CancelHodlInvoice(entry.invoice);
            }
            WinnerMessage = lastAuction.winningEntry.invoice.Memo;
            StartAuction();
        }

        public List<AuctionEntry> GetAuctionEntries()
        {
            if (currentAuction == null)
                return null;
            return currentAuction.Entries;
        }

        public string GetWinningMessage()
        {
            return WinnerMessage;
        }

        public bool AuctionFinished()
        {
            if(currentAuction == null || currentAuction.ClosedAt == null)
            {
                return true;
            }
            return false;
        }


    }

    public class Auction
    {
        public DateTime StartedAt { get; set; }
        public List<AuctionEntry> Entries;
        public DateTime ClosedAt;
        public AuctionEntry winningEntry;

        public Auction()
        {
            Entries = new List<AuctionEntry>();
            StartedAt = DateTime.Now;
        }

        public void AddEntry(AuctionEntry entry)
        {
            Entries.Add(entry);
            Entries.Sort();
        }

        public void CloseAuction()
        {
            ClosedAt = DateTime.Now;
            Entries.Sort();
            winningEntry = Entries[0];
        }

    }
}
