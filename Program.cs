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
        private static IConfiguration _configuration = null!;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments. Expects 2-4 arguments: <account> <integration code> [from] [to].</param>
        static async Task Main(string[] args)
        {
            Init(); // Initialize configuration first

            // Check if user wants to see schools list
            if (args.Length == 1 && args[0].ToLower() == "konti")
            {
                ShowAccounts();
                return;
            }

            if (args.Length < 2 || args.Length > 4)
            {
                Write("Usage: <account> <integration code> [from] [to]");
                Write("Example: MyAccount MyCode 2025-01-01 2025-01-31");
                Write("Or use: konti - to show available accounts");
                Write(string.Empty);
                return;
            } 

            string requestId = Guid.NewGuid().ToString();
            DateTime time = DateTime.UtcNow;
            string erpId = _configuration["erpId"] ?? throw new ArgumentNullException("erpId");
            string erpNavn = _configuration["erpNavn"] ?? throw new ArgumentNullException("erpNavn");
            string kontonr = args[0];
            string integrationskode = args[1];

            // Parse optional from and to dates
            DateTime fromDate, toDate;
            if (args.Length >= 3 && DateTime.TryParse(args[2], out fromDate))
            {
                if (args.Length >= 4 && DateTime.TryParse(args[3], out toDate))
                {
                    // Both from and to specified
                }
                else
                {
                    // Only from specified, use end of month as to
                    toDate = new DateTime(fromDate.Year, fromDate.Month, DateTime.DaysInMonth(fromDate.Year, fromDate.Month));
                }
            }
            else
            {
                // No dates specified, use current month
                fromDate = new DateTime(time.Year, time.Month, 1);
                toDate = new DateTime(time.Year, time.Month, DateTime.DaysInMonth(time.Year, time.Month));
            }

            var entries = await GetEntries(erpId, erpNavn, kontonr, integrationskode, requestId, time, fromDate, toDate);

            Write("Result from bankintegration.dk:");
            Write(string.Empty);
            Write(entries);
            Write(string.Empty);

            // Parse JSON response to see structure
            try 
            {
                var jsonDocument = JsonDocument.Parse(entries);
                var rootElement = jsonDocument.RootElement;
                
                // Try to find array in the response
                if (rootElement.ValueKind == JsonValueKind.Array)
                {
                    ExportJsonArrayToExcel(rootElement, "BankEntries");
                }
                else if (rootElement.ValueKind == JsonValueKind.Object)
                {
                    // Look for array properties in the object
                    foreach (var property in rootElement.EnumerateObject())
                    {
                        Write($"Property: {property.Name}, Type: {property.Value.ValueKind}");
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            ExportJsonArrayToExcel(property.Value, property.Name);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Write($"Error parsing JSON: {ex.Message}", ConsoleColor.Red);
            }
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
        /// <param name="fromDate">The start date for the query.</param>
        /// <param name="toDate">The end date for the query.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the API as a string.</returns>
        private static async Task<string> GetEntries(string erpId, string erpNavn, string konto, string integrationskode, string requestId, DateTime now, DateTime fromDate, DateTime toDate)
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
                try {
                    Console.Clear();
                } catch {
                    // Ignore clear errors when output is piped
                }
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
        /// Exports a JSON array to an Excel file and saves it to c:\temp.
        /// </summary>
        /// <param name="jsonArray">The JSON array element to export.</param>
        /// <param name="sheetName">The name of the Excel sheet.</param>
        private static void ExportJsonArrayToExcel(JsonElement jsonArray, string sheetName)
        {
            if (jsonArray.ValueKind != JsonValueKind.Array)
            {
                Write("Error: JSON element is not an array", ConsoleColor.Red);
                return;
            }

            string filePath = $@"c:\temp\BankEntries_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add(sheetName);

                var arrayItems = jsonArray.EnumerateArray().ToArray();
                if (arrayItems.Length == 0)
                {
                    Write("No data to export", ConsoleColor.Yellow);
                    return;
                }

                // Get all unique property names from the first few items
                var allProperties = new HashSet<string>();
                foreach (var item in arrayItems.Take(10))
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in item.EnumerateObject())
                        {
                            allProperties.Add(property.Name);
                        }
                    }
                }

                var propertyNames = allProperties.ToArray();

                // Add headers
                for (int i = 0; i < propertyNames.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = propertyNames[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                }

                // Add data
                for (int row = 0; row < arrayItems.Length; row++)
                {
                    if (arrayItems[row].ValueKind == JsonValueKind.Object)
                    {
                        for (int col = 0; col < propertyNames.Length; col++)
                        {
                            if (arrayItems[row].TryGetProperty(propertyNames[col], out JsonElement property))
                            {
                                worksheet.Cells[row + 2, col + 1].Value = GetJsonValue(property);
                            }
                        }
                    }
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Save to file
                Directory.CreateDirectory(@"c:\temp");
                File.WriteAllBytes(filePath, package.GetAsByteArray());

                Write($"Excel file with {arrayItems.Length} entries saved to {filePath}");
            }
        }
        
                private static void ShowAccounts()
        {
            try
            {
                var accounts = _configuration.GetBankAccounts();
                
                if (!accounts.Any())
                {
                    Write("No accounts found in User Secrets.", ConsoleColor.Yellow);
                    Write("Use 'dotnet user-secrets set \"BankAccounts\" \"[{...}]\"' to add accounts.", ConsoleColor.Yellow);
                    return;
                }

                Write("Available Accounts:", ConsoleColor.Green);
                Write(new string('=', 50), ConsoleColor.Green);
                Write(string.Empty);
                Write($"{"NAVN",-40} {"INSTKODE",-10} {"BBAN",-18} INTEGRATIONSKEY");
                Write(new string('-', 80), ConsoleColor.Gray);
                
                foreach (var account in accounts)
                {
                    Write($"{account.Navn,-40} {account.InstKode,-10} {account.BBAN,-18} {account.IntegrationsKey}");
                }
                
                Write(string.Empty);
                Write($"Total accounts found: {accounts.Count}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Write($"Error reading accounts from User Secrets: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Converts a JsonElement to a string value for Excel.
        /// </summary>
        /// <param name="element">The JSON element to convert.</param>
        /// <returns>The string representation of the JSON value.</returns>
        private static object GetJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetDecimal(out var dec) ? (object)dec : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
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
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "c:\\temp");
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

    public class Account
    {
        public string Navn { get; set; } = "";
        public string InstKode { get; set; } = "";
        public string BBAN { get; set; } = "";
        public string IntegrationsKey { get; set; } = "";
    }

    // Hjælpeklasse til at håndtere konto-konfiguration
    public static class ConfigurationExtensions
    {
        public static List<Account> GetBankAccounts(this IConfiguration configuration)
        {
            // Læs direkte som array fra configuration section
            return configuration.GetSection("BankAccounts").Get<List<Account>>() ?? new List<Account>();
        }
    }
}
