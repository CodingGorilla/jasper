using Jasper.Configuration;
using Jasper.Transports.Tcp;
using Jasper.Util;
using Shouldly;
using Xunit;

namespace Jasper.Testing.Transports.Tcp
{
    public class TcpEndpointTests
    {
        [Fact]
        public void default_host()
        {
            new TcpEndpoint()
                .HostName.ShouldBe("localhost");

            new TcpEndpoint(3333)
                .HostName.ShouldBe("localhost");
        }

        [Theory]
        [InlineData("tcp://localhost:4444", "localhost", 4444, EndpointMode.BufferedInMemory)]
        [InlineData("tcp://localhost:4445", "localhost", 4445, EndpointMode.BufferedInMemory)]
        [InlineData("tcp://server1:4445", "server1", 4445, EndpointMode.BufferedInMemory)]
        [InlineData("tcp://server1:4445/durable", "server1", 4445, EndpointMode.Durable)]
        public void parsing_uri(string uri, string host, int port, EndpointMode mode)
        {
            var endpoint = new TcpEndpoint();
            endpoint.Parse(uri.ToUri());

            endpoint.HostName.ShouldBe(host);
            endpoint.Port.ShouldBe(port);
            endpoint.Mode.ShouldBe(mode);
        }

        [Fact]
        public void reply_uri_when_durable()
        {
            var endpoint = new TcpEndpoint(4444);
            endpoint.Mode = EndpointMode.Durable;

            endpoint.ReplyUri().ShouldBe($"tcp://localhost:4444/durable".ToUri());
        }

        [Fact]
        public void reply_uri_when_not_durable()
        {
            var endpoint = new TcpEndpoint(4444);
            endpoint.Mode = EndpointMode.BufferedInMemory;

            endpoint.ReplyUri().ShouldBe($"tcp://localhost:4444".ToUri());
        }
    }
}
