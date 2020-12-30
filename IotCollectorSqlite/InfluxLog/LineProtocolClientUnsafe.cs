using InfluxDB.LineProtocol;
using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IotCollectorSqlite.InfluxLog
{
    public class LineProtocolClientUnsafe : LineProtocolClientBase
    {
        private readonly HttpClient _httpClient;

        public LineProtocolClientUnsafe(Uri serverBaseAddress, string database, string username = null, string password = null)
            : this(new HttpClientHandler(), serverBaseAddress, database, username, password)
        {
        }

        protected LineProtocolClientUnsafe(
                HttpMessageHandler handler,
                Uri serverBaseAddress,
                string database,
                string username,
                string password)
            : base(serverBaseAddress, database, username, password)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (
    sender,
    cert,
    chain,
    sslPolicyErrors) =>
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;   //Is valid
                }

                if (cert.GetCertHashString() == "A443BA9A3CCEEC3E6C51CC74A835CA8481697FEF")
                {
                    return true;
                }

                return false;
            };
            if (serverBaseAddress == null)
                throw new ArgumentNullException(nameof(serverBaseAddress));
            if (string.IsNullOrEmpty(database))
                throw new ArgumentException("A database must be specified");

            // Overload that allows injecting handler is protected to avoid HttpMessageHandler being part of our public api which would force clients to reference System.Net.Http when using the lib.
            _httpClient = new HttpClient(httpClientHandler) { BaseAddress = serverBaseAddress };


        }

        protected override async Task<LineProtocolWriteResult> OnSendAsync(
            string payload,
            Precision precision,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var endpoint = $"write?db={Uri.EscapeDataString(_database)}";
            if (!string.IsNullOrEmpty(_username))
                endpoint += $"&u={Uri.EscapeDataString(_username)}&p={Uri.EscapeDataString(_password)}";

            switch (precision)
            {
                case Precision.Microseconds:
                    endpoint += "&precision=u";
                    break;
                case Precision.Milliseconds:
                    endpoint += "&precision=ms";
                    break;
                case Precision.Seconds:
                    endpoint += "&precision=s";
                    break;
                case Precision.Minutes:
                    endpoint += "&precision=m";
                    break;
                case Precision.Hours:
                    endpoint += "&precision=h";
                    break;
            }

            var content = new StringContent(payload, Encoding.UTF8);
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new LineProtocolWriteResult(true, null);
            }

            var body = string.Empty;

            if (response.Content != null)
            {
                body = await response.Content.ReadAsStringAsync();
            }

            return new LineProtocolWriteResult(false, $"{response.StatusCode} {response.ReasonPhrase} {body}");
        }
    }
}
