using Microsoft.AspNetCore.Http;

namespace Trill.APIGateway.Framework
{
    internal interface ICorrelationContextBuilder
    {
        CorrelationContext Build(HttpContext context, string correlationId, string spanContext, string name = null,
            string resourceId = null);
    }
}