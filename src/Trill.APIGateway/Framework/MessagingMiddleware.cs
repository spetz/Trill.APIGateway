using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Convey.MessageBrokers.RabbitMQ;
using Convey.MessageBrokers.RabbitMQ.Conventions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Trill.APIGateway.Framework
{
    internal class MessagingMiddleware : IMiddleware
    {
        private static readonly ConcurrentDictionary<string, IConventions> Conventions =
            new ConcurrentDictionary<string, IConventions>();

        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly RouteMatcher _routeMatcher;
        private readonly ICorrelationContextBuilder _correlationContextBuilder;
        private readonly CorrelationIdFactory _correlationIdFactory;
        private readonly IDictionary<string, List<MessagingOptions.EndpointOptions>> _endpoints;

        public MessagingMiddleware(IRabbitMqClient rabbitMqClient, RouteMatcher routeMatcher,
            ICorrelationContextBuilder correlationContextBuilder, CorrelationIdFactory correlationIdFactory,
            IOptions<MessagingOptions> messagingOptions)
        {
            _rabbitMqClient = rabbitMqClient;
            _routeMatcher = routeMatcher;
            _correlationContextBuilder = correlationContextBuilder;
            _correlationIdFactory = correlationIdFactory;
            _endpoints = messagingOptions.Value.Endpoints?.Any() is true
                ? messagingOptions.Value.Endpoints.GroupBy(e => e.Method.ToUpperInvariant())
                    .ToDictionary(e => e.Key, e => e.ToList())
                : new Dictionary<string, List<MessagingOptions.EndpointOptions>>();
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (!_endpoints.TryGetValue(context.Request.Method, out var endpoints))
            {
                await next(context);
                return;
            }

            foreach (var endpoint in endpoints)
            {
                var match = _routeMatcher.Match(endpoint.Path, context.Request.Path);
                if (match is null)
                {
                    continue;
                }

                var key = $"{endpoint.Exchange}:{endpoint.RoutingKey}";
                if (!Conventions.TryGetValue(key, out var conventions))
                {
                    conventions = new MessageConventions(typeof(object), endpoint.RoutingKey, endpoint.Exchange, null);
                    Conventions.TryAdd(key, conventions);
                }

                var messageId = Guid.NewGuid().ToString("N");
                var correlationId = _correlationIdFactory.Create();
                var resourceId = Guid.NewGuid().ToString("N");
                var correlationContext = _correlationContextBuilder.Build(context, correlationId, default,
                    endpoint.RoutingKey, resourceId);

                var content = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var message = JsonConvert.DeserializeObject(content);
                _rabbitMqClient.Send(message, conventions, messageId, correlationId, default, correlationContext);
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return;
            }

            await next(context);
        }
    }
}