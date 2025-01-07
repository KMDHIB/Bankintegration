# BankIntegration API Documentation

As an ERP provider, you can purchase access to the BankIntegration API to perform bank transactions on behalf of your customers.

The API is REST-based and only works via HTTPS.

---

## API Functions

### 1. **Send Payment Request**

**Endpoint:**

```http
POST https://api.bankintegration.dk/payment
```

### 2. **Retrieve Payment Request Status**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/status
```

### 3. **Retrieve Account Statement**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/report/account
```

#### Example Account Statement Request

```json
{
  "serviceProvider": "MyERP",
  "account": "12345678901234",
  "time": "20250107T120000",
  "requestId": "REQ12345"
}
```

**Example Response:**

```json
{
  "status": "success",
  "transactions": [
    {
      "date": "2025-01-06",
      "amount": "-500.00",
      "currency": "DKK",
      "description": "Payment to supplier",
      "transactionId": "TX12345"
    },
    {
      "date": "2025-01-05",
      "amount": "+1500.00",
      "currency": "DKK",
      "description": "Payment from customer",
      "transactionId": "TX12346"
    }
  ]
}
```

### 4. **Retrieve Balance**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/report/balance
```

---

## Getting Started

### 1. **Obtain an API Key**

Before use, you must obtain an API key and a corresponding ERP name or code. These details are acquired through Dansk BankIntegration:

- **Email:** [support@bankintegration.dk](mailto:support@bankintegration.dk)

The API key must be kept secret and only used by the ERP provider.

### 2. **Access to Customer Profile**

- The ERP provider must have access to an end customer's profile.
- Create a test customer via our homepage using the ERP provider's own email address.
- The test customer must have one or more accounts linked to the ERP provider.

---

## API Endpoint

All API functions are accessed via:

```http
https://api.bankintegration.dk
```

---

## HTTP Status Codes

- **Success:**
  - 200, 201, 202, 204
- **Error:**
  - 500-599 for system or connection errors

See further details here: [HTTP/1.1 Status Code Definitions](https://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html).

---

## Authentication

### Request Authentication

All requests must include a JSON authentication object in the request header. This section explains the calculation procedure for the object and how to insert it into the request.

A JSON authentication object is created as below. The JSON object is sent with all HTTP 1.1 GET/POST requests as a UTF-8 encoded byte array in Base64 format. The value is inserted into the HTTP 1.1 Authorization header value.

For payment requests (POST), a hash object is created for each transaction.
For GET requests (status, account statements, etc.), only one hash object is created where "id" is set to the value in "requestId".

```json
{
  "serviceProvider": "<ERP name>",
  "account": "<Customer bank account>",
  "time": "<UTC Timestamp>",
  "requestId": "<Request ID>",
  "user": "<ERP's ID of user>" (optional),
  "hash": [
    {
      "id": "<Payment ID>",
      "hash": "<HMAC-Base64>"
    },
    {
      "id": "<Payment ID>",
      "hash": "<HMAC-Base64>"
    }
  ]
}
```

For each transaction in the payment request, a hash value is calculated and inserted into the authentication object's hash array.
All parts of the string (Payload) are separated by a # (pound). Blank/empty values are included.

First, the payload string is composed of the values found in the transaction and the top values.

1. A token string of the customer's password as a SHA256 hash over a UTF-8 encoded byte array formatted in HEX.
2. The customer's account number formatted to BBAN (14 digits).
3. Payment date formatted to YYYYMMDD (Y=year, M=month, D=day).
4. Amount formatted with two decimals and a period as the decimal separator (no thousand separators).
5. Currency formatted to ISO currency code (like DKK or USD).
6. Account or payment ID is retrieved from the request object.
7. ERP provider's name/code is inserted.
8. Payment ID from the transaction is inserted.
9. The current UTC time formatted to YYYYMMDDHHmmSS (Y=year, M=month, D=day, H=hour (24h), m=minute, S=second).

A HMAC/SHA256 hash is now generated over each payload string (as a UTF-8 encoded byte array) with the ERP provider's API key as the key.
The HMAC value is inserted along with `<PaymentId>` and HMAC as a Base64 string.

### C# Code for Hash Calculation

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

public class HashCalculator
{
    public static string CalculateHash(string customerCode, string account, string currency, string requestId, string paymentDate, decimal amount, string recipientAccount, string erpName, string paymentId, DateTime utcNow, string erpApiKey)
    {
        string token = ComputeSha256Hash(customerCode);
        string custacc = FormatToBBAN(account);
        string paydate = paymentDate.ToString("yyyyMMdd");
        string amountFormatted = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string now = utcNow.ToString("yyyyMMddHHmmss");

        string payload = $"{token}#{custacc}#{currency}#{requestId}#{paydate}#{amountFormatted}#{recipientAccount}#{erpName}#{paymentId}#{now}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        byte[] erpKeyBytes = Guid.Parse(erpApiKey).ToByteArray();
        using (var hmac = new HMACSHA256(erpKeyBytes))
        {
            byte[] hmacBytes = hmac.ComputeHash(payloadBytes);
            return Convert.ToBase64String(hmacBytes);
        }
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private static string FormatToBBAN(string account)
    {
        // Custom implementation for formatting BBAN
        return account.PadLeft(14, '0');
    }
}
```
