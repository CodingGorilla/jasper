using System;
using System.Collections.Generic;
using Confluent.Kafka;
using Jasper.ConfluentKafka.Internal;
using Jasper.Transports;

namespace Jasper.ConfluentKafka
{
    public static class Protocols
    {
        public const string Kafka = "kafka";

    }
    public class KafkaTransport : TransportBase<KafkaEndpoint>
    {
        private readonly Dictionary<Uri, KafkaEndpoint> _endpoints;

        public KafkaTopicRouter Topics { get; } = new KafkaTopicRouter();
        public Config Config { get; set; }

        public KafkaTransport() : base(Protocols.Kafka)
        {
            _endpoints = new Dictionary<Uri, KafkaEndpoint>();
        }

        protected override IEnumerable<KafkaEndpoint> endpoints() => _endpoints.Values;

        protected override KafkaEndpoint findEndpointByUri(Uri uri) => _endpoints[uri];

        public KafkaEndpoint<TKey, TVal> EndpointForTopic<TKey, TVal>(string topicName, ProducerConfig producerConifg) =>
            AddOrUpdateEndpoint<TKey, TVal>(topicName, c => c.ProducerConfig = producerConifg);

        public KafkaEndpoint<TKey, TVal> EndpointForTopic<TKey, TVal>(string topicName, ConsumerConfig consumerConifg) =>
            AddOrUpdateEndpoint<TKey, TVal>(topicName, c => c.ConsumerConfig = consumerConifg);

        KafkaEndpoint<TKey, TVal> AddOrUpdateEndpoint<TKey, TVal>(string topicName, Action<KafkaEndpoint<TKey, TVal>> configure)
        {
            var endpoint = new KafkaEndpoint<TKey, TVal>
            {
                TopicName = topicName
            };

            if (_endpoints.ContainsKey(endpoint.Uri))
            {
                endpoint = (KafkaEndpoint<TKey, TVal>)_endpoints[endpoint.Uri];
                configure(endpoint);
            }
            else
            {
                configure(endpoint);
                _endpoints.Add(endpoint.Uri, endpoint);
            }

            return endpoint;
        }

    }
}