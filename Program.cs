using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace BankIntegration
{
    class Program
    {   
        private static IConfiguration _configuration;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments. Expects two arguments: <account> and <integration code>.</param>
        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Write("Usage: <account> <integration code>");
                Write(string.Empty);
                return;
            }

            Init(); 

            string requestId = Guid.NewGuid().ToString();
            DateTime time = DateTime.UtcNow;
            string erpId = _configuration["erpId"] ?? throw new ArgumentNullException("erpId");
            string erpNavn = _configuration["erpNavn"] ?? throw new ArgumentNullException("erpNavn");
            string kontonr = args[0];
            string integrationskode = args[1];

            var entries = await GetEntries(erpId, erpNavn, kontonr, integrationskode, requestId, time);

            ExportToExcel(JsonSerializer.Deserialize<dynamic[]>(entries));

            Write("Result from bankintegration.dk:");
            Write(string.Empty);
            Write(entries);
            Write(string.Empty);
        }

        /// <summary>
        /// Retrieves entries from the bank integration API.
        /// </summary>
        /// <param name="erpId">The ERP system identifier.</param>
        /// <param name="erpNavn">The ERP system name.</param>
        /// <param name="konto">The account number.</param>
        /// <param name="integrationskode">The integration code.</param>
        /// <param name="requestId">The unique request identifier.</param>
        /// <param name="now">The current date and time.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a string.</returns>
        private static async Task<string> GetEntries(string erpId, string erpNavn, string konto, string integrationskode, string requestId, DateTime now)
        {
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.bankintegration.dk/report/account"))
            {
                requestMessage.Content = JsonContent.Create(new
                {
                    requestId = requestId,
                    from = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd"),
                    to = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)).ToString("yyyy-MM-dd"),
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
                time = now.ToString("yyyy-MM-ddTHH:mm:ss"),
                requestId = requestId,
                hash = new List<AuthorizationHash>() {
                new AuthorizationHash() {
                    id = requestId,
                    hash = CalculateHash(erpId, kode, custacc, erp, now, requestId)
                }
            }
            };

            string jsonString = JsonSerializer.Serialize(auth);

            Write("JSON object for authentication: " + jsonString);
            Write(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(jsonString);

            string requestHeader = Convert.ToBase64String(bytes);

            Write("Authentication object encoded in UTF-8 bytes and presented as Base64: " + requestHeader);
            Write(string.Empty);

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

            Write("Payload for hash calculation: " + payLoad);
            Write(string.Empty);

            string hashedString = HashHmacSha256(payLoad.ToString(), new Guid(apikey));

            Write("Hash encoded in UTF-8 bytes presented as Base64: " + hashedString);
            Write(string.Empty);

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

        /// <summary>
        /// Initializes the console window.
        /// </summary>
        private static void Init() {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            if (!Debugger.IsAttached) {
                Console.Clear();
            }

            _configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();                 
        }

        /// <summary>
        /// Writes a string to the console window.
        /// </summary>
        /// <param name="writestuff"></param>
        /// <param name="color"></param>
        private static void Write(string writestuff, ConsoleColor color = ConsoleColor.Cyan)
        {
            if (color != ConsoleColor.Cyan) {
                Console.ForegroundColor = color;
            }                

            foreach (char c in writestuff)
            {
                Console.Write(c);
                // Thread.Sleep(1);
            }

            Console.WriteLine();

            if (Console.ForegroundColor != ConsoleColor.Cyan) {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }                
        }

        /// <summary>
        /// Exports an array of objects to an Excel file and saves it to c:\temp.
        /// </summary>
        /// <typeparam name="T">The type of objects in the array.</typeparam>
        /// <param name="data">The array of objects to export.</param>
        /// <param name="fileName">The name of the Excel file to save.</param>
        private static void ExportToExcel<T>(T[] data)
        {
            string filePath = $@"c:\temp\{DateTime.Now.Ticks.ToString()}.xlsx";

            using (var package = new OfficeOpenXml.ExcelPackage())
            {
            var worksheet = package.Workbook.Worksheets.Add("Sheet1");

            // Add headers
            var properties = typeof(T).GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = properties[i].Name;
            }

            // Add data
            for (int row = 0; row < data.Length; row++)
            {
                for (int col = 0; col < properties.Length; col++)
                {
                worksheet.Cells[row + 2, col + 1].Value = properties[col].GetValue(data[row]);
                }
            }

            // Save to file
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, package.GetAsByteArray());
            }

            Write($"Excel file saved to {filePath}");
        }

        /// <summary>
        /// Represents an authorization hash.
        /// </summary>
        private record AuthorizationHash
        {
            public string id { get; set; } = string.Empty;
            public string hash { get; set; } = string.Empty;
        }
    }
}
