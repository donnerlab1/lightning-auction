using Grpc.Core.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LightningAuction.Utility;

namespace LightningAuction.Services
{
    public class TimerService : BackgroundService
    {
        IAuctionService _auctionService;
        public TimerService(IAuctionService auctionService)
        {
            _auctionService = auctionService;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
                var auctions = await _auctionService.GetAllAuctions(false, true);
                foreach(var auction in auctions)
                {
                    var now = Utility.Utility.DateTimeToUnix(DateTime.UtcNow);
                    if (now >= auction.StartedAt + auction.Duration)
                    {

                        Console.WriteLine("finishing auction {0}", auction);
                        await _auctionService.EndAuction(auction.Id);
                    }
                }
            }
        }
    }
}
