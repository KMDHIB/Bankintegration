namespace BankIntegration
{
    public static class StorageHelper
    {
        public static void ShowAccountsFromLocalStorage(List<Account> accounts)
        {
            try
            {
                if (!accounts.Any())
                {
                    ConsoleHelper.Write("No accounts found in User Secrets.", ConsoleColor.Yellow);
                    ConsoleHelper.Write("Use 'dotnet user-secrets set \"BankAccounts\" \"[{...}]\"' to add accounts.", ConsoleColor.Yellow);
                    return;
                }

                ConsoleHelper.Write("Available Accounts:", ConsoleColor.Green);
                ConsoleHelper.Write(new string('=', 50), ConsoleColor.Green);
                ConsoleHelper.Write(string.Empty);
                ConsoleHelper.Write($"{"NAVN",-40} {"INSTKODE",-10} {"BBAN",-18} INTEGRATIONSKEY");
                ConsoleHelper.Write(new string('-', 80), ConsoleColor.Gray);

                foreach (var account in accounts)
                {
                    ConsoleHelper.Write($"{account.Navn,-40} {account.InstKode,-10} {account.BBAN,-18} {account.IntegrationsKey}");
                }

                ConsoleHelper.Write(string.Empty);
                ConsoleHelper.Write($"Total accounts found: {accounts.Count}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write($"Error reading accounts from User Secrets: {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}