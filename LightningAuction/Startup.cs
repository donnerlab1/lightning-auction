using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LightningAuction.Services;
using LightningAuction.Models;


namespace LightningAuction
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<TimerService>();
            services.AddDbContext<AuctionContext>();
            services.AddSingleton<ILndService, LndService>();

            services.AddSingleton<IRaffleService, RaffleService>();
            services.AddSingleton<IAuctionService, AuctionService>();

            //services.AddControllers();
            services.AddGrpc();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();
            
            app.UseRouting();
            
            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapControllers();
                endpoints.MapGrpcService<LightningAuction.Delivery.LightningAuctionService>();
                endpoints.MapGrpcService<LightningAuction.Delivery.LightningAuctionAdminService>();
            });
        }
    }
}
