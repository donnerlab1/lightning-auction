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

        private RNGCryptoServiceProvider provider;
        private Dictionary<string,byte[]> preImages;
        public LndService(IConfiguration config)
        {
            var directory = Environment.CurrentDirectory;
            var tls = File.ReadAllText(directory + "/tls.cert");
            var rpc = config.GetValue<string>("rpc");

            var macaroonCallCredentials = MacaroonCallCredentials.FromFile(File.ReadAllBytes(directory + "/admin.macaroon"));

            var channelCreds = ChannelCredentials.Create(new SslCredentials(tls), macaroonCallCredentials.credentials);

            var lndChannel = new Grpc.Core.Channel(rpc, channelCreds);
            lightningClient = new Lightning.LightningClient(lndChannel);
            invoicesClient = new Invoices.InvoicesClient(lndChannel);

            var getInfo = lightningClient.GetInfo(new GetInfoRequest());
            provider = new RNGCryptoServiceProvider();
            OnInvoiceCreated += LndService_OnInvoiceCreated;
            Console.WriteLine(getInfo.ToString());
            preImages = new Dictionary<string, byte[]>();
        }

        private void LndService_OnInvoiceCreated(object sender, Invoice invoice)
        {
            Console.WriteLine("ON INVOICE CREATED");
            SubscribeHoldInvoices(invoice.RHash);
            Console.WriteLine("ON INVOICE Done");
        }


        public async Task<string> GetHoldInvoice(long amount, string message)
        {
            var preImage = GetRandomBytes();
            var rHash = GetRHash(preImage);
            var invoice = new Invoice { Value = amount, Memo = message, RHash = rHash, RPreimage = Google.Protobuf.ByteString.CopyFrom(preImage) };

            OnInvoiceCreated.Invoke(this, invoice);
            var res = await invoicesClient.AddHoldInvoiceAsync(new AddHoldInvoiceRequest { Value = amount, Memo = message,Hash = invoice.RHash });
            preImages.Add(res.PaymentRequest, preImage);
            return res.PaymentRequest;
        }

        public async void SubscribeHoldInvoices(Google.Protobuf.ByteString rHash)
        {
            var cancellationToken = new CancellationTokenSource();
            var request = new SubscribeSingleInvoiceRequest { RHash = rHash };
           try
            {
                using(var invoiceStream = invoicesClient.SubscribeSingleInvoice(request))
                {

                    Console.WriteLine("Subscribing");
                    while (await invoiceStream.ResponseStream.MoveNext() && !cancellationToken.IsCancellationRequested)
                    {
                        var invoice = invoiceStream.ResponseStream.Current;

                        Console.WriteLine("got invoice "+invoice.State.ToString());
                        if (invoice.State == Invoice.Types.InvoiceState.Accepted)
                        {
                            if(OnHoldInvoiceActivated != null)
                            {
                                OnHoldInvoiceActivated.Invoke(this, invoice);
                                cancellationToken.Cancel();
                            }
                        }
                    
                    }
                }
            }catch(RpcException e)
            {
                Console.WriteLine("ERROR SUBSCRIBTION: " + e.Message);
            }
        }

        public async Task SettleHodlInvoice(Invoice invoice)
        {
            if (preImages.ContainsKey(invoice.PaymentRequest))
            {
                var preImage = preImages[invoice.PaymentRequest];
                try { 
                await invoicesClient.SettleInvoiceAsync(new SettleInvoiceMsg { Preimage = Google.Protobuf.ByteString.CopyFrom(preImage) });
                }catch (RpcException e)
                {
                Console.WriteLine("ERROR: " + e.Message);
                }
            }
        }

        public async Task CancelHodlInvoice(Invoice invoice)
        {
            try
            {

                await invoicesClient.CancelInvoiceAsync(new CancelInvoiceMsg { PaymentHash = invoice.RHash });
            }catch(RpcException e)
            {
                Console.WriteLine("ERROR: " + e.Message);
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

        private Google.Protobuf.ByteString GetRHash(byte[] rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(rawData);

                return Google.Protobuf.ByteString.CopyFrom(bytes);
            }
        }


    }



    public interface ILndService
    {
        Task<string> GetHoldInvoice(long amount, string message);
        void AddHoldInvoiceListener(InvoiceActiveEventHandler e);

        void RemoveHoldInvoiceListener(InvoiceActiveEventHandler e);

        void ResetHoldInvoiceListener();
        Task SettleHodlInvoice(Invoice invoice);
        Task CancelHodlInvoice(Invoice invoice);

        Task<(bool, string)> VerifyMessage(string message, string signature);
        
    }

    public delegate void InvoiceActiveEventHandler(object sender, Invoice invoice);
    public delegate void InvoiceCreatedEventHandler(object sender, Invoice invoice);
}
