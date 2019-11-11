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
        public AuctionController(IAuctionService auctionService,IConfiguration config, ILndService lndService, IRaffleService raffleService)
        {
            _auctionService = auctionService;
            _lndService = lndService;
            AuthorizedPubkey = config.GetValue<string>("admin_pub");
        }

        [HttpGet("/auctions")]
        public async Task<ListAuctionResponse> ListAuctions()
        {
            var res = await _auctionService.GetAllAuctions(false, false);
            return new ListAuctionResponse { Auctions = res};
        }

        [HttpGet("/auction/invoice/{auctionid}/{amount}/{text}")]
        public async Task<string> RequestHodlInvoice(string auctionid, string text, long amount)
        {
            var res = await _auctionService.RequestAuctionEntryInvoice(auctionid, amount, text);
            return res.ToString();
        }
        [HttpGet("/auction/winner")]
        public string GetWinningText()
        {
            return "";
        }

        [HttpGet("/auction/start/{duration}")]
        public async Task<string> StartNewAuction(int duration)
        {
            var res = await _auctionService.StartAuction(duration);
            return res.ToString();
        }
        [HttpGet("/auction/end/{auctionid}")]
        public async Task<string> EndAuction(string auctionId)
        {
            var res = await _auctionService.EndAuction(auctionId);
            return res.ToString();
        }
        [HttpGet("/auction/abort/{auctionid}")]
        public async Task<string> AbortAuction(string auctionId)
        {
            var res = await _auctionService.AbortAuction(auctionId);
            return res.ToString();
        }
    }

    [Serializable]
    public struct AuctionEntries
    {
        public List<Entry> Entries { get; set; }
    }

    [Serializable]
    public struct Entry
    {
        public long amount { get; set; }
        public string pHash { get; set; }
        public DateTime AcceptedAt { get; set; }
    }
    [Serializable]
    public struct ListAuctionResponse
    {
        public Auction[] Auctions { get; set; }
    }
}
