using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Trill.APIGateway.Framework
{
    internal class LogContextMiddleware : IMiddleware
    {
        private readonly CorrelationIdFactory _correlationIdFactory;

        public LogContextMiddleware(CorrelationIdFactory correlationIdFactory)
        {
            _correlationIdFactory = correlationIdFactory;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var correlationId = _correlationIdFactory.Create();
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next(context);
            }
        }
    }
}