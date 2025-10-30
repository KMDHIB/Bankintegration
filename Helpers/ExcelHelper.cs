using System.Text.Json;

namespace BankIntegration
{
    public static class ExcelHelper
    {
        /// <summary>
        /// Exports a JSON array to an Excel file and saves it to c:\temp.
        /// </summary>
        /// <param name="jsonArray">The JSON array element to export.</param>
        /// <param name="sheetName">The name of the Excel sheet.</param>
        public static void ExportJsonArrayToExcel(JsonElement jsonArray, string sheetName)
        {
            if (jsonArray.ValueKind != JsonValueKind.Array)
            {
                ConsoleHelper.Write("Error: JSON element is not an array", ConsoleColor.Red);
                return;
            }

            string filePath = $@"c:\temp\BankEntries_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add(sheetName);

                var arrayItems = jsonArray.EnumerateArray().ToArray();
                if (arrayItems.Length == 0)
                {
                    ConsoleHelper.Write("No data to export", ConsoleColor.Yellow);
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

                ConsoleHelper.Write($"Excel file with {arrayItems.Length} entries saved to {filePath}");
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
    }
}