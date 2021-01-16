using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;

namespace Trill.APIGateway.Framework
{
    internal class CustomProxyHttpClientFactory : IProxyHttpClientFactory
    {
        private readonly CorrelationIdFactory _correlationIdFactory;

        public CustomProxyHttpClientFactory(CorrelationIdFactory correlationIdFactory)
        {
            _correlationIdFactory = correlationIdFactory;
        }
        
        public HttpMessageInvoker CreateClient(ProxyHttpClientContext context)
        {
            if (context.OldClient != null && context.NewOptions == context.OldOptions)
            {
                return context.OldClient;
            }

            var newClientOptions = context.NewOptions;

            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            };

            if (newClientOptions.SslProtocols.HasValue)
            {
                handler.SslOptions.EnabledSslProtocols = newClientOptions.SslProtocols.Value;
            }

            if (newClientOptions.ClientCertificate != null)
            {
                handler.SslOptions.ClientCertificates = new X509CertificateCollection
                {
                    newClientOptions.ClientCertificate
                };
            }

            if (newClientOptions.MaxConnectionsPerServer != null)
            {
                handler.MaxConnectionsPerServer = newClientOptions.MaxConnectionsPerServer.Value;
            }

            if (newClientOptions.DangerousAcceptAnyServerCertificate)
            {
                handler.SslOptions.RemoteCertificateValidationCallback =
                    (sender, cert, chain, errors) => cert.Subject == "trill.io";
            }
            
            var httpMessageInvoker =  new CustomHttpMessageInvoker(_correlationIdFactory, handler, true);

            return httpMessageInvoker;
        }

        private class CustomHttpMessageInvoker : HttpMessageInvoker
        {
            private readonly CorrelationIdFactory _correlationIdFactory;

            public CustomHttpMessageInvoker(CorrelationIdFactory correlationIdFactory, HttpMessageHandler handler,
                bool disposeHandler) : base(handler, disposeHandler)
            {
                _correlationIdFactory = correlationIdFactory;
            }

            public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var correlationId = _correlationIdFactory.Create();
                request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}