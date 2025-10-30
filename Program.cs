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
            Init();

            // Check if user wants to see schools list
            if (args.Length == 1 && args[0].ToLower() == "konti")
            {
                StorageHelper.ShowAccountsFromLocalStorage(_configuration.GetSection("BankAccounts").Get<List<Account>>() ?? new List<Account>());
                return;
            }

            if (args.Length < 2 || args.Length > 4)
            {
                ConsoleHelper.Write("Usage: <account> <integration code> [from] [to]");
                ConsoleHelper.Write("Example: MyAccount MyCode 2025-01-01 2025-01-31");
                ConsoleHelper.Write("Or use: konti - to show available accounts");
                ConsoleHelper.Write(string.Empty);
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

            var entries = await BankintegrationHelper.GetEntries(erpId, erpNavn, kontonr, integrationskode, requestId, time, fromDate, toDate);

            ConsoleHelper.Write("Result from bankintegration.dk:");
            ConsoleHelper.Write(string.Empty);
            ConsoleHelper.Write(entries);
            ConsoleHelper.Write(string.Empty);

            // Parse JSON response to see structure
            try
            {
                var jsonDocument = JsonDocument.Parse(entries);
                var rootElement = jsonDocument.RootElement;

                // Try to find array in the response
                if (rootElement.ValueKind == JsonValueKind.Array)
                {
                    ExcelHelper.ExportJsonArrayToExcel(rootElement, "BankEntries");
                }
                else if (rootElement.ValueKind == JsonValueKind.Object)
                {
                    // Look for array properties in the object
                    foreach (var property in rootElement.EnumerateObject())
                    {
                        ConsoleHelper.Write($"Property: {property.Name}, Type: {property.Value.ValueKind}");
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            ExcelHelper.ExportJsonArrayToExcel(property.Value, property.Name);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write($"Error parsing JSON: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Initializes the console window.
        /// </summary>
        private static void Init()
        {
            ConsoleHelper.Init();

            _configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();
        }
    }
}
