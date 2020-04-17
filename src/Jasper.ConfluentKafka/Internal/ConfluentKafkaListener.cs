using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Jasper.ConfluentKafka;
using Jasper.ConfluentKafka.Serialization;
using Jasper.Logging;
using Jasper.Transports;

namespace Jasper.Kafka.Internal
{
    public class ConfluentKafkaListener<TKey, TVal> : IListener
    {
        private readonly CancellationToken _cancellation;

        private readonly KafkaEndpoint _endpoint;
        private readonly ITransportLogger _logger;
        private readonly KafkaTransportProtocol<TKey, TVal> _protocol = new KafkaTransportProtocol<TKey, TVal>();
        private IReceiverCallback _callback;
        private IConsumer<TKey, TVal> _consumer;
        private Task _consumerTask;

        public ConfluentKafkaListener(KafkaEndpoint endpoint, ITransportLogger logger, CancellationToken cancellation)
        {
            _endpoint = endpoint;
            _logger = logger;
            _cancellation = cancellation;
            Address = endpoint.Uri;
        }


        public void Dispose()
        {
            _consumerTask?.Dispose();
            _consumer?.Dispose();
        }

        public Uri Address { get; }
        public ListeningStatus Status { get; set; }

        public void Start(IReceiverCallback callback)
        {
            _callback = callback;

            _consumer = new ConsumerBuilder<TKey, TVal>(_endpoint.ConsumerConfig)
                .SetKeyDeserializer(new DefaultJsonDeserializer<TKey>().AsSyncOverAsync())
                .SetValueDeserializer(new DefaultJsonDeserializer<TVal>().AsSyncOverAsync())
                .Build();

            _consumer.Subscribe(_endpoint.TopicName);

            _consumerTask = ConsumeAsync();
        }

        private async Task ConsumeAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                ConsumeResult<TKey, TVal> message;
                try
                {
                    message = _consumer.Consume();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, message: $"Error consuming message from Kafka topic {_endpoint.TopicName}");
                    return;
                }

                Envelope envelope;

                try
                {
                    envelope = _protocol.ReadEnvelope(message.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, message: $"Error trying to map an incoming Kafka {_endpoint.TopicName} Topic message to an Envelope. See the Dead Letter Queue");
                    return;
                }

                try
                {
                    await _callback.Received(Address, new[] { envelope });

                    _consumer.Commit();
                }
                catch (Exception e)
                {
                    _logger.LogException(e, envelope.Id, "Error trying to receive a message from " + Address);
                }
            }
        }

    }
}