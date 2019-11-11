using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Core;
using LightningAuction.Services;
using Microsoft.Extensions.Logging;

namespace LightningAuction.Delivery

{
    public class LightningAuctionService : LightningAuctionBidder.LightningAuctionBidderBase
    {

        private readonly ILogger<LightningAuctionService> _logger;
        private IAuctionService _auctionService;
        public LightningAuctionService(ILogger<LightningAuctionService> logger, IAuctionService auctionService)
        {
            _logger = logger;
            _auctionService = auctionService;
        }

        public override async Task<BidResponse> Bid(BidRequest request, ServerCallContext context)
        {
            var entry = await _auctionService.RequestAuctionEntryInvoice(request.AuctionId, request.Amount, request.Message);
            var res = new BidResponse
            {
                Entry = new AuctionEntry
                {
                    Amount = entry.Amount,
                    Description = entry.Description,
                    Id = entry.Id.ToString(),
                    Message = entry.Message,
                    PaymentRequest = entry.PaymentRequest,
                    State = (AuctionEntry.Types.State)((int)entry.State)
                }
            };
            return res;
        }

        public override async Task<CancelBidResponse> CancelBid(CancelBidRequest request, ServerCallContext context)
        {
            var entry = await _auctionService.CancelAuctionEntry(request.EntryId);
            var res = new CancelBidResponse
            { 
                
                Ok = entry == null? false:true
            };
            return res;
        }

        public override async Task<GetAuctionResponse> GetAuction(GetAuctionRequest request, ServerCallContext context)
        {
            var auction = _auctionService.GetAuction(request.AuctionId);
            var res = new GetAuctionResponse
            {
                Auction = new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt,
                }

            };

            return res;
        }

        public override async Task<ListAuctionsResponse> ListAuctions(ListAuctionsRequest request, ServerCallContext context)
        {
            var auctions = await _auctionService.GetAllAuctions(request.OnlyFinished, request.OnlyActive);
            var res = new ListAuctionsResponse();
            foreach (var auction in auctions)
            {
                res.Auctions.Add(new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt
                });
            }
            return res;
        }

        public override Task SubscribeToBid(SubscribeToBidRequest request, IServerStreamWriter<SubscribeToBidResponse> responseStream, ServerCallContext context)
        {
            return base.SubscribeToBid(request, responseStream, context);
        }
    }

    public class LightningAuctionAdminService : LightningAuctionAdmin.LightningAuctionAdminBase
    {
        IAuctionService _auctionService;
        public LightningAuctionAdminService(IAuctionService auctionService)
        {
            _auctionService = auctionService;
        }

        public override async Task<EndAuctionResponse> EndAuction(EndAuctionRequest request, ServerCallContext context)
        {
            var auction = await _auctionService.EndAuction(request.AuctionId);
            Models.AuctionInvoice winningEntry = null;
            try
            {
                winningEntry = JsonSerializer.Deserialize<LightningAuction.Models.AuctionInvoice>(auction.WinningEntry);
            }
            catch
            {

            }
            var res = new EndAuctionResponse
            {
                Auction = new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt,

                }
            };

            if (winningEntry != null)
            {
                res.Auction.WinningEntry = new AuctionEntry
                {
                    Amount = winningEntry.Amount,
                    Message = winningEntry.WinningMessage,

                };
            }


            return res;
        }

        public override async Task<AdminGetAuctionResponse> GetAuction(AdminGetAuctionRequest request, ServerCallContext context)
        {
            var auction = _auctionService.GetAuction(request.AuctionId);
            
                var res = new AdminGetAuctionResponse
                {
                    Auction = new Auction
                    {
                        Id = auction.Id.ToString(),
                        StartedAt = auction.StartedAt,
                        Duration = auction.Duration,
                        FinishedAt = auction.FinishedAt,
                    }

                };
                foreach (var ae in auction.AuctionEntries)
                {
                    res.AuctionEntries.Add(new AuctionEntry
                    {
                        Amount = ae.Amount,
                        Id = ae.Id.ToString(),
                        Description = ae.Description,
                        Message = ae.Message,
                        PaymentRequest = ae.PaymentRequest
                    });
                }

            return res;
        }

        public override async Task<ListAuctionsResponse> ListAuctions(ListAuctionsRequest request, ServerCallContext context)
        {
            var auctions = await _auctionService.GetAllAuctions(request.OnlyFinished, request.OnlyActive);
            var res = new ListAuctionsResponse();
            foreach(var auction in auctions)
            {
                res.Auctions.Add(new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt
                });
            }
            return res;
        }

        public override async Task<StartAuctionResponse> StartAuction(StartAuctionRequest request, ServerCallContext context)
        {
            var auction = await _auctionService.StartAuction(request.Duration);
            return new StartAuctionResponse
            {
                Auction = new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration 
                }
            };
        }
    }
}
