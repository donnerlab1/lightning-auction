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
    public class RaffleController : Controller
    {
        private readonly IRaffleService _raffleService;
        private readonly ILndService _lndService;
        private readonly string AuthorizedPubkey;
        public RaffleController(IConfiguration config, ILndService lndService, IRaffleService raffleService)
        {
            _lndService = lndService;
            _raffleService = raffleService;
            AuthorizedPubkey = config.GetValue<string>("admin_pub");
        }

        [HttpGet("/raffle/entries/{raffleid}")]
        public async Task<RaffleEntries> GetEntries(string raffleId)
        {
            var entries = await _raffleService.ListRaffleEntries(raffleId);
            if (entries == null)
                return new RaffleEntries();
            var res = new RaffleEntries()
            {
                Entries = new List<RaffleEntryRest>()
            };
            foreach (var entry in entries)
            {
                res.Entries.Add(new RaffleEntryRest() {  amount = entry.Amount, text = entry.Memo});
            }
            return res;
        }

        [HttpGet("/raffle/invoice/{raffleid}/{amount}/{text}")]
        public async Task<string> RequestRaffleInvoice(string raffleId, string text, long amount)
        {
            var res = await _raffleService.GetRaffleInvoice(raffleId, text, amount);
            return res;
        }

        [HttpGet("/raffle/start/{message}/{signature}")]
        public async Task<RaffleRest> StartNewRaffle(string message, string signature)
        {
            (bool valid, string pubkey) = await _lndService.VerifyMessage(message, signature);
            Console.WriteLine("Requesting start auction");
            if (valid && pubkey == AuthorizedPubkey)
            {
                var res = await _raffleService.StartRaffle();
                var raffle = new RaffleRest
                {
                    FinishedAt = DateTime.FromFileTimeUtc(res.FinishedAt),
                    StartedAt = DateTime.FromFileTimeUtc(res.StartedAt),
                    Id = res.Id.ToString()
                };
                return raffle;
            }
            return new RaffleRest();
        }
        [HttpGet("/raffle/end/{raffleid}/{message}/{signature}")]
        public async Task<RaffleRest> EndRaffle(string raffleId, string message, string signature)
        {

            Console.WriteLine("Requesting end auction");
            (bool valid, string pubkey) = await _lndService.VerifyMessage(message, signature);
            if (valid && pubkey == AuthorizedPubkey)
            {
                var res = await _raffleService.EndRaffle(raffleId);
                var raffle = new RaffleRest
                {
                    FinishedAt = DateTime.FromFileTimeUtc(res.FinishedAt),
                    StartedAt = DateTime.FromFileTimeUtc(res.StartedAt),
                    Id = res.Id.ToString()
                };
                return raffle;
            }
            return new RaffleRest();
        }
    }
    [Serializable]
    public struct RaffleEntries
    {
        public List<RaffleEntryRest> Entries { get; set; }
    }

    [Serializable]
    public struct RaffleEntryRest
    {
        public long amount { get; set; }
        public string text { get; set; }
    }

    [Serializable]

    public struct RaffleRest
    {
        public string Id;
        public DateTime StartedAt;
        public DateTime FinishedAt;
    }
}
