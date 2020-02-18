using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lnrpc;
using Invoicesrpc;
using Microsoft.Extensions.Configuration;
using System.IO;
using Grpc.Core;
using System.Threading;
using Newtonsoft.Json;
using LightningAuction.Models;
using System.Security.Cryptography;


namespace LightningAuction.Services
{
    public class LndService :ILndService
    {
        public Lightning.LightningClient lightningClient;
        public Invoices.InvoicesClient invoicesClient;

        public event InvoiceActiveEventHandler OnHoldInvoiceActivated;
        private event InvoiceCreatedEventHandler OnInvoiceCreated;
        private event InvoicePaidEventHandler OnInvoicePaid;

        private RNGCryptoServiceProvider provider;
        private Grpc.Core.Channel lndChannel;
        public LndService(IConfiguration config)
        {
            var directory = Environment.CurrentDirectory;
            var tls = File.ReadAllText(directory + "/tls.cert");
            var rpc = config.GetValue<string>("rpc");

            var macaroonCallCredentials = MacaroonCallCredentials.FromFile(File.ReadAllBytes(directory + "/admin.macaroon"));

            var channelCreds = ChannelCredentials.Create(new SslCredentials(tls), macaroonCallCredentials.credentials);

            lndChannel = new Grpc.Core.Channel(rpc, channelCreds);
            lightningClient = new Lightning.LightningClient(lndChannel);
            invoicesClient = new Invoices.InvoicesClient(lndChannel);

            var getInfo = lightningClient.GetInfo(new GetInfoRequest());
            provider = new RNGCryptoServiceProvider();
            OnInvoiceCreated += LndService_OnInvoiceCreated;
            Console.WriteLine(getInfo.ToString());
            //CancelExistingInvoices();

            ListenInvoices();
        }

        private void LndService_OnInvoiceCreated(object sender, Invoice invoice, byte[] preImage)
        {
            Console.WriteLine("ON INVOICE CREATED");
            SubscribeHoldInvoices(invoice.RHash, preImage);
            Console.WriteLine("ON INVOICE Done");
        }
        public async void ListenInvoices()
        {
            var request = new InvoiceSubscription();

            using (var _invoiceStream = lightningClient.SubscribeInvoices(request))
            {
                while (!lndChannel.ShutdownToken.IsCancellationRequested && await _invoiceStream.ResponseStream.MoveNext())
                {
                    var invoice = _invoiceStream.ResponseStream.Current;
                    if (invoice.State == Invoice.Types.InvoiceState.Settled)
                    {
                        if(OnInvoicePaid != null)
                            OnInvoicePaid(invoice);
                    }

                }
            }
            if (!lndChannel.ShutdownToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
                ListenInvoices();
            }


        }

        


        public async Task<HoldInvoiceResponse> GetHoldInvoice(long amount, string message, long expiry)
        {
            var preImage = GetRandomBytes();
            var rHash = GetRHash(preImage);
            var invoice = new Invoice { Value = amount, Memo = message, RHash = Google.Protobuf.ByteString.CopyFrom(rHash), RPreimage = Google.Protobuf.ByteString.CopyFrom(preImage), Expiry= expiry };

            OnInvoiceCreated.Invoke(this, invoice, preImage);
            var res = await invoicesClient.AddHoldInvoiceAsync(new AddHoldInvoiceRequest { Value = amount, Memo = message,Hash = invoice.RHash, CltvExpiry = 144 });
            var holdInvoiceResponse = new HoldInvoiceResponse
            {
                paymentHash = rHash,
                preImage = preImage,
                payreq = res.PaymentRequest,
                
            };
            return holdInvoiceResponse;
        }

        public async void SubscribeHoldInvoices(Google.Protobuf.ByteString rHash, byte[] preImage)
        {
            var cancellationToken = new CancellationTokenSource();
            var request = new SubscribeSingleInvoiceRequest { RHash = rHash };
           try
            {
                using(var invoiceStream = invoicesClient.SubscribeSingleInvoice(request))
                {

                    Console.WriteLine("Subscribing");
                    while (!cancellationToken.IsCancellationRequested && await invoiceStream.ResponseStream.MoveNext())
                    {
                        var invoice = invoiceStream.ResponseStream.Current;

                        Console.WriteLine("got invoice "+invoice.State.ToString());
                        if (invoice.State == Invoice.Types.InvoiceState.Accepted)
                        {

                            if (OnHoldInvoiceActivated != null)
                            {
                                OnHoldInvoiceActivated.Invoke(this, invoice,preImage);
                                cancellationToken.Cancel();
                            }
                        }
                    
                    }
                }
            }catch(RpcException e)
            {
                Console.WriteLine("ERROR SUBSCRIBTION: " + e.Message);
                await Task.Delay(100);
                SubscribeHoldInvoices(rHash, preImage);
            }
            Console.WriteLine("FINISH SUBSCRIPTION");

        }

        public async Task<string> AddInvoice(string description, long amount)
        {
            var res = await lightningClient.AddInvoiceAsync(new Invoice
            {
                Memo = description,
                Value = amount
            });
            return res.PaymentRequest;
        }
        public async Task<bool> SettleHodlInvoice(byte[] preimage)
        {

                Console.WriteLine("settling invoice");
                try { 
                await invoicesClient.SettleInvoiceAsync(new SettleInvoiceMsg { Preimage = Google.Protobuf.ByteString.CopyFrom(preimage) });
                return true;
                
                }catch (RpcException e)
                {
                Console.WriteLine("ERROR: " + e.Message);
                return false;
                }
            
        }

        private async Task<PayReq> DecodePayReq(string payreq)
        {
            var decoderes = await lightningClient.DecodePayReqAsync(new PayReqString { PayReq = payreq });
            return decoderes;
        }

        public async Task<bool> CancelHodlInvoice(byte[] paymentHash)
        {
            try
            {
                Console.WriteLine("cancling invoice");
                await invoicesClient.CancelInvoiceAsync(new CancelInvoiceMsg { PaymentHash = Google.Protobuf.ByteString.CopyFrom(paymentHash) });
                return true;
            }catch(RpcException e)
            {
                Console.WriteLine("ERROR: " + e.Message);
                return false;
            }
        }

        public void AddHoldInvoiceListener(InvoiceActiveEventHandler e)
        {
            this.OnHoldInvoiceActivated += e;
        }
        public void RemoveHoldInvoiceListener(InvoiceActiveEventHandler e)
        {
            this.OnHoldInvoiceActivated -= e;
        }

        public void ResetHoldInvoiceListener()
        {
            this.OnHoldInvoiceActivated = null;
        }

        public async Task<(bool, string)> VerifyMessage(string message, string signature)
        {
            var res = await lightningClient.VerifyMessageAsync(new VerifyMessageRequest { Msg = Google.Protobuf.ByteString.CopyFromUtf8(message), Signature = signature });
            return (res.Valid, res.Pubkey);
        }

        private byte[] GetRandomBytes()
        {
            var byteArray = new byte[32];

            provider.GetBytes(byteArray);
            return byteArray;
        }

        private byte[] GetRHash(byte[] rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(rawData);

                return bytes;
            }
        }

        public void AddInvoicePaidEventHandler(InvoicePaidEventHandler e)
        {
            this.OnInvoicePaid += e;
        }
    }



    public interface ILndService
    {
        Task<HoldInvoiceResponse> GetHoldInvoice(long amount, string message, long expiry);
        void AddHoldInvoiceListener(InvoiceActiveEventHandler e);

        void RemoveHoldInvoiceListener(InvoiceActiveEventHandler e);

        void ResetHoldInvoiceListener();
        Task<bool> SettleHodlInvoice(byte[] preimage);
        Task<bool> CancelHodlInvoice(byte[] paymentHash);

        Task<(bool, string)> VerifyMessage(string message, string signature);

        Task<string> AddInvoice(string description, long amount);

        void AddInvoicePaidEventHandler(InvoicePaidEventHandler e);


    }

    public delegate void InvoiceActiveEventHandler(object sender, Invoice invoice, byte[] preImage);
    public delegate void InvoiceCreatedEventHandler(object sender, Invoice invoice, byte[] preImage);
    public delegate void InvoicePaidEventHandler(Invoice invoice);

    public struct HoldInvoiceResponse
    {
        public string payreq { get; set; }
        public byte[] preImage { get; set; }
        public byte[] paymentHash { get; set; }
    }
}
