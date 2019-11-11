# Lightning Auction
Simple auction using hodl invoices:

## Usage

- requires dotnet core 3.0
-  place tls.cert and admin macaroon in LightningAuction/LightningAuction folder
- run `dotnet ef migrations add InitialCreate`
- run `dotnet ef database update`
- `dotnet run rpc="host:port" admin_pub="your_pubkey"` in LightningAuction/LightningAuction
-  sign a message with `lncli signmessage `
-  `/auction/start/{message}/{signature}` to start the auction
-  `/auction/invoice/{amount}/{text}` requests a hodl invoice
-  `/auction/entries` lists all entries
-  `/auction/end/{message}/{signature}` to end the auction
-  `/auction/winner` shows the last winners text

