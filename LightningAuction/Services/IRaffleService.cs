using System.Collections.Generic;
using System.Threading.Tasks;
using LightningAuction.Models;

namespace LightningAuction.Services
{
    public interface IRaffleService
    {
        Task<Raffle> EndRaffle(string raffleId);
        Raffle GetRaffle(string raffleId);
        Task<string> GetRaffleInvoice(string raffleId, string description, long amount);
        Task<List<RaffleEntry>> ListRaffleEntries(string raffleId);
        Task<Raffle> StartRaffle();
    }
}