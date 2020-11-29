using Convey;
using Convey.Auth;
using Convey.MessageBrokers.RabbitMQ;
using Convey.Metrics.Prometheus;
using Convey.Tracing.Jaeger;
using Convey.Types;
using Convey.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Trill.APIGateway.Framework;

namespace Trill.APIGateway
{
    internal class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<LogContextMiddleware>();
            services.AddScoped<UserMiddleware>();
            services.AddScoped<MessagingMiddleware>();
            services.AddSingleton<CorrelationIdFactory>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ICorrelationContextBuilder, CorrelationContextBuilder>();
            services.AddSingleton<RouteMatcher>();
            services.Configure<MessagingOptions>(Configuration.GetSection("messaging"));
            services.AddReverseProxy()
                .LoadFromConfig(Configuration.GetSection("ReverseProxy"));
            services.AddSingleton<IProxyHttpClientFactory, CustomProxyHttpClientFactory>();
            services
                .AddConvey()
                .AddJwt()
                .AddPrometheus()
                .AddJaeger()
                .AddRabbitMq()
                .AddWebApi()
                .Build();
            
            services.AddAuthorization(options =>
            {
                options.AddPolicy("authenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());
            });

            services.AddCors(cors =>
            {
                cors.AddPolicy("cors", x =>
                {
                    x.WithOrigins("*")
                        .WithMethods("POST", "PUT", "DELETE")
                        .WithHeaders("Content-Type", "Authorization");
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMiddleware<LogContextMiddleware>();
            app.UseCors("cors");
            app.UseConvey();
            app.UseJaeger();
            app.UsePrometheus();
            app.UseAccessTokenValidator();
            app.UseAuthentication();
            app.UseRabbitMq();
            app.UseMiddleware<UserMiddleware>();
            app.UseMiddleware<MessagingMiddleware>();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync(context.RequestServices.GetService<AppOptions>().Name);
                });
                endpoints.MapReverseProxy();
            });
        }
    }
}
