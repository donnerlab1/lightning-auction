using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Core;
using LightningAuction.Services;
using Microsoft.Extensions.Configuration;
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
            Guid auctionId;
            if(!Guid.TryParse(request.AuctionId, out auctionId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "auctionid is not a guid"));
            }
            if(request.Amount < 1)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "amount must be larger than 0"));
            }
            if(request.Message.Length > 140)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "message must be smaller than 140 characters"));
            }
            var entry = await _auctionService.RequestAuctionEntryInvoice(auctionId, request.Amount, request.Message);
            if (entry == null)
                return new BidResponse();
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
            Guid entryId;
            if (!Guid.TryParse(request.EntryId, out entryId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "entryid is not a guid"));
            }
            var entry = await _auctionService.CancelAuctionEntry(entryId);
            if(entry == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "could not cancel bid"));
            }
            var res = new CancelBidResponse
            { 
                
                Ok = true
            };
            return res;
        }

        public override async Task<GetAuctionResponse> GetAuction(GetAuctionRequest request, ServerCallContext context)
        {
            Models.Auction auction;
            if(request.AuctionId == "active")
            {
                auction = await _auctionService.GetActiveAuction();
                if(auction == null )
                    throw new RpcException(new Status(StatusCode.NotFound, "no active auction running"));
            }else
            {
                Guid auctionId;
                if (!Guid.TryParse(request.AuctionId, out auctionId))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "entryid is not a guid"));
                }
                auction = _auctionService.GetAuction(auctionId);
            }
            
            if(auction == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "auction not found"));
            }
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

        public override async Task<GetBidResponse> GetBid(GetBidRequest request, ServerCallContext context)
        {
            Guid entryId;
            if (!Guid.TryParse(request.EntryId, out entryId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "entryid is not a guid"));
            }
            var entry = await _auctionService.GetBid(entryId);
            if (entry == null)
            {
                return new GetBidResponse();
            }
                
            var res = new GetBidResponse
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
    

        public override async Task<ListAuctionsResponse> ListAuctions(ListAuctionsRequest request, ServerCallContext context)
        {
            var auctions = await _auctionService.GetAllAuctions(request.OnlyFinished, request.OnlyActive);
            var res = new ListAuctionsResponse();
            foreach (var auction in auctions)
            {
                var auctionItem = new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt
                };

                try
                {
                    var winningEntry = JsonSerializer.Deserialize<LightningAuction.Models.AuctionInvoice>(auction.WinningEntry);
                    auctionItem.WinningEntry = new AuctionEntry
                    {

                        Amount = winningEntry.Amount,
                        Message = winningEntry.WinningMessage


                    };
                }
                catch
                {

                }

                res.Auctions.Add(auctionItem);
            }
            return res;
        }

    
    }

    public class LightningAuctionAdminService : LightningAuctionAdmin.LightningAuctionAdminBase
    {
        IAuctionService _auctionService;
        ILndService _lnd;
        readonly string AuthorizedPubkey;
        readonly string MessageToSign;
        public LightningAuctionAdminService(IConfiguration config, IAuctionService auctionService, ILndService lndService)
        {
            _auctionService = auctionService;
            _lnd = lndService;
            AuthorizedPubkey = config.GetValue<string>("admin_pub");
            MessageToSign = config.GetValue<string>("message");
        }

        private async Task<bool> CheckPassword(ServerCallContext context)
        {
            
            var signature = context.RequestHeaders.FirstOrDefault(h => h.Key == "signature");
            if (signature == null || signature.Value == "")
                return false;
            var verify = await _lnd.VerifyMessage(MessageToSign, signature.Value);
            if (verify.Item1 == true && verify.Item2 == AuthorizedPubkey)
                return true;
            return false;
        }

        public override async Task<EndAuctionResponse> EndAuction(EndAuctionRequest request, ServerCallContext context)
        {
            if (!(await CheckPassword(context))){
                throw new RpcException(new Status(StatusCode.PermissionDenied, "you are not authorized"));
            }
            Guid auctionId;
            if (!Guid.TryParse(request.AuctionId, out auctionId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "entryid is not a guid"));
            }
            var auction = await _auctionService.EndAuction(auctionId);
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
                    Message = winningEntry.WinningMessage

                };
            }


            return res;
        }

        public override async Task<AdminGetAuctionResponse> GetAuction(AdminGetAuctionRequest request, ServerCallContext context)
        {
            if (!(await CheckPassword(context)))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "you are not authorized"));
            }
            Guid auctionId;
            if (!Guid.TryParse(request.AuctionId, out auctionId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "entryid is not a guid"));
            }
            var auction = _auctionService.GetAuction(auctionId);
            
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

                var auctionEntry = new AuctionEntry
                {
                    Amount = ae.Amount,
                    Id = ae.Id.ToString(),
                    Description = ae.Description,
                    Message = ae.Message,
                    PaymentRequest = ae.PaymentRequest
                };
                    res.AuctionEntries.Add(auctionEntry);
                    
                }

            return res;
        }

        public override async Task<ListAuctionsResponse> ListAuctions(ListAuctionsRequest request, ServerCallContext context)
        {
            if (!(await CheckPassword(context)))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "you are not authorized"));
            }
            var auctions = await _auctionService.GetAllAuctions(request.OnlyFinished, request.OnlyActive);
            var res = new ListAuctionsResponse();
            foreach(var auction in auctions)
            {
                var auctionItem = new Auction
                {
                    Id = auction.Id.ToString(),
                    StartedAt = auction.StartedAt,
                    Duration = auction.Duration,
                    FinishedAt = auction.FinishedAt
                };
                
                try
                {
                    var winningEntry = JsonSerializer.Deserialize<LightningAuction.Models.AuctionInvoice>(auction.WinningEntry);
                    auctionItem.WinningEntry = new AuctionEntry
                    {

                        Amount = winningEntry.Amount,
                        Message = winningEntry.WinningMessage


                    };
                }
                catch
                {

                }

                res.Auctions.Add(auctionItem);
            }
            return res;
        }

        public override async Task<StartAuctionResponse> StartAuction(StartAuctionRequest request, ServerCallContext context)
        {
            if (!(await CheckPassword(context)))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "you are not authorized"));
            }
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
