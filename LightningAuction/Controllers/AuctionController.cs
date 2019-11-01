using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using LightningAuction.Services;
using LightningAuction.Models;
using Microsoft.Extensions.Configuration;

namespace LightningAuction.Controllers
{

    [Route("api/[controller]")]
    public class AuctionController : Controller
    {

        private readonly IAuctionService _auctionService;
        private readonly ILndService _lndService;
        private readonly string AuthorizedPubkey;
        public AuctionController(IAuctionService auctionService,IConfiguration config, ILndService lndService)
        {
            _auctionService = auctionService;
            _lndService = lndService;
            AuthorizedPubkey = config.GetValue<string>("admin_pub");
        }

        [HttpGet("/auction/entries")]
        public AuctionEntries GetEntries()
        {
            var entries = _auctionService.GetAuctionEntries();
            if (entries == null)
                return new AuctionEntries();
            var res = new AuctionEntries()
            {
                entries = new List<Entry>()
            };
            foreach(var entry in entries)
            {
                res.entries.Add(new Entry() { AcceptedAt = entry.ActivatedAt, amount = entry.amount, pHash = entry.invoice.RHash.ToStringUtf8() });
            }
            return res;
        }

        [HttpGet("/auction/invoice/{amount}/{text}")]
        public async Task<string> RequestHodlInvoice(string text, long amount)
        {
            var res = await _auctionService.RequestAuctionEntry(amount, text);
            return res;
        }

        [HttpGet("/auction/winner")]
        public string GetWinningText()
        {
            return _auctionService.GetWinningMessage();
        }


        [HttpGet("/auction/start/{message}/{signature}")]
        public async void StartNewAuction(string message, string signature)
        {
            (bool valid, string pubkey) = await _lndService.VerifyMessage(message, signature);
            if(valid && pubkey == AuthorizedPubkey && _auctionService.AuctionFinished())
            {

                _auctionService.StartAuction();
            }
        }
        [HttpGet("/auction/end/{message}/{signature}")]
        public async void EndAuction(string message, string signature)
        {
            (bool valid, string pubkey) = await _lndService.VerifyMessage(message, signature);
            if (valid && pubkey == AuthorizedPubkey)
            {
                await _auctionService.EndAuction();
            }
        }
    }

    [Serializable]
    public struct AuctionEntries
    {
        public List<Entry> entries { get; set; }
    }

    [Serializable]
    public struct Entry
    {
        public long amount { get; set; }
        public string pHash { get; set; }
        public DateTime AcceptedAt { get; set; }
    }
}
