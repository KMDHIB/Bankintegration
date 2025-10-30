using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BankIntegration
{
    public static class BankintegrationHelper
    {
        /// <summary>
        /// Retrieves entries from the bank integration API.
        /// </summary>
        /// <param name="erpId">The ERP system identifier.</param>
        /// <param name="erpNavn">The ERP system name.</param>
        /// <param name="konto">The account number.</param>
        /// <param name="integrationskode">The integration code.</param>
        /// <param name="requestId">The unique request identifier.</param>
        /// <param name="now">The current date and time.</param>
        /// <param name="fromDate">The start date for the query.</param>
        /// <param name="toDate">The end date for the query.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a string.</returns>
        public static async Task<string> GetEntries(string erpId, string erpNavn, string konto, string integrationskode, string requestId, DateTime now, DateTime fromDate, DateTime toDate)
        {
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.bankintegration.dk/report/account"))
            {
                requestMessage.Content = JsonContent.Create(new
                {
                    requestId = requestId,
                    from = fromDate.ToString("yyyy-MM-dd"),
                    to = toDate.ToString("yyyy-MM-dd"),
                    newOnly = false
                });

                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("BASIC", CalculateRequestHeader(requestId, erpId, erpNavn, konto, integrationskode, now));

                var result = await new HttpClient().SendAsync(requestMessage);
                result.EnsureSuccessStatusCode();
                return await result.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// Calculates the request header for authentication.
        /// </summary>
        /// <param name="requestId">The unique request identifier.</param>
        /// <param name="erpId">The ERP system identifier.</param>
        /// <param name="erpNavn">The ERP system name.</param>
        /// <param name="konto">The account number.</param>
        /// <param name="integrationskode">The integration code.</param>
        /// <param name="now">The current date and time.</param>
        /// <returns>The calculated request header as a Base64 encoded string.</returns>
        private static string CalculateRequestHeader(string requestId, string erpId, string erpNavn, string konto, string integrationskode, DateTime now)
        {
            string kode = integrationskode;
            string custacc = konto;
            string erp = erpNavn;

            var auth = new
            {
                serviceProvider = erp,
                account = custacc,
                time = now.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                requestId = requestId,
                hash = new List<AuthorizationHash>() {
                new AuthorizationHash() {
                    id = requestId,
                    hash = CalculateHash(erpId, kode, custacc, erp, now, requestId)
                }
            }
            };

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonString = JsonSerializer.Serialize(auth, options);

            ConsoleHelper.Write("JSON object for authentication: " + jsonString);
            ConsoleHelper.Write(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(jsonString);

            string requestHeader = Convert.ToBase64String(bytes);

            ConsoleHelper.Write("Authentication object encoded in UTF-8 bytes and presented as Base64: " + requestHeader);
            ConsoleHelper.Write(string.Empty);

            return requestHeader;
        }

        /// <summary>
        /// Calculates a hash using the provided parameters.
        /// </summary>
        /// <param name="apikey">The API key.</param>
        /// <param name="kode">The integration code.</param>
        /// <param name="custacc">The customer account number.</param>
        /// <param name="erpName">The ERP system name.</param>
        /// <param name="now">The current date and time.</param>
        /// <param name="requestId">The unique request identifier.</param>
        /// <returns>The calculated hash as a string.</returns>
        private static string CalculateHash(string apikey, string kode, string custacc, string erpName, DateTime now, string requestId)
        {
            var token = HashSha256(kode);
            string currency = "";
            string paydate = "";
            string amount = "";
            var credacc = "";
            string payid = requestId;

            string payLoad = $"{token}#{custacc}#{currency}#{requestId}#{paydate}#{amount}#{credacc}#{erpName}#{payid}#{now:yyyyMMddHHmmss}";

            ConsoleHelper.Write("Payload for hash calculation: " + payLoad);
            ConsoleHelper.Write(string.Empty);

            string hashedString = HashHmacSha256(payLoad.ToString(), new Guid(apikey));

            ConsoleHelper.Write("Hash encoded in UTF-8 bytes presented as Base64: " + hashedString);
            ConsoleHelper.Write(string.Empty);

            return hashedString;
        }

        /// <summary>
        /// Computes an HMAC-SHA-256 hash of the given text using the provided key.
        /// </summary>
        static string HashSha256(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.Default.GetBytes(text);

                byte[] hashBytes = sha256.ComputeHash(passwordBytes);

                StringBuilder stringBuilder = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    stringBuilder.Append(b.ToString("x2"));
                }

                return stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Computes an HMAC-SHA-256 hash of the given text using the provided key.
        /// </summary>
        /// <param name="text">The text to hash.</param>
        /// <param name="key">The key to use for the HMAC.</param>
        /// <returns>The HMAC-SHA-256 hash as a Base64 encoded string.</returns>
        private static string HashHmacSha256(string text, Guid key)
        {
            byte[] keyBytes = key.ToByteArray();
            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(textBytes);

                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}