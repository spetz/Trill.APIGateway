using System;
using System.Threading;

namespace Trill.APIGateway.Framework
{
    internal class CorrelationIdFactory
    {
        private static readonly AsyncLocal<CorrelationIdHolder> Holder = new AsyncLocal<CorrelationIdHolder>();

        private static string CorrelationId
        {
            get => Holder.Value?.Id;
            set
            {
                var holder = Holder.Value;
                if (holder is {})
                {
                    holder.Id = null;
                }

                if (value is {})
                {
                    Holder.Value = new CorrelationIdHolder {Id = value};
                }
            }
        }

        private class CorrelationIdHolder
        {
            public string Id;
        }

        public string Create()
        {
            if (!string.IsNullOrWhiteSpace(CorrelationId))
            {
                return CorrelationId;
            }

            CorrelationId = Guid.NewGuid().ToString("N");
            return CorrelationId;
        }
    }
}