using System;
using Xunit;
using LightningAuction.Models;
using System.Collections.Generic;

namespace LightningText.Tests
{
    public class AuctionEntryTest
    {
        [Fact]
        public void Test1()
        {
            var entry1 = new AuctionEntry()
            {
                invoice = null,
                amount = 100,
                ActivatedAt = DateTime.Now
            };

            var entry2 = new AuctionEntry()
            {
                invoice = null,
                amount = 110,
                ActivatedAt = DateTime.Now
            };

            var entry3 = new AuctionEntry()
            {
                invoice = null,
                amount = 100,
                ActivatedAt = DateTime.MaxValue
            };

            var auctionList = new List<AuctionEntry>()
            {
                entry1,entry2,entry3
            };

            auctionList.Sort();

            Assert.Equal(auctionList[0], entry2);
            Assert.Equal(auctionList[1], entry1);
            Assert.Equal(auctionList[2], entry3);
        }
    }
}
