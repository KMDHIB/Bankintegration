# BankIntegration API Dokumentation

Som ERP-udbyder kan man tilkøbe sig adgang til BankIntegrations API  til at foretage banktransaktioner på sine kunders vegne.

API'en er REST-baseret og fungerer kun via HTTPS.

---

## API Funktioner

### 1. **Sende betalingsanmodning**

**Endpoint:**

```http
POST https://api.bankintegration.dk/payment
```

### 2. **Hente status på betalingsanmodninger**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/status
```

### 3. **Hente kontoudtog**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/report/account
```

#### Eksempel på kontoudtog forespørgsel

```json
{
  "serviceProvider": "MyERP",
  "account": "12345678901234",
  "time": "20250107T120000",
  "requestId": "REQ12345"
}
```

**Eksempel på svar:**

```json
{
  "status": "success",
  "transactions": [
    {
      "date": "2025-01-06",
      "amount": "-500.00",
      "currency": "DKK",
      "description": "Betaling til leverandør",
      "transactionId": "TX12345"
    },
    {
      "date": "2025-01-05",
      "amount": "+1500.00",
      "currency": "DKK",
      "description": "Indbetaling fra kunde",
      "transactionId": "TX12346"
    }
  ]
}
```

### 4. **Hente balance**

**Endpoint:**

```http
GET/POST https://api.bankintegration.dk/report/balance
```

---

## Kom godt i gang

### 1. **Få en API-nøgle**

Inden brug skal du have en API-nøgle og et tilhørende ERP-navn eller -kode. De oplysninger erhverves gennem Dansk BankIntegration:

- **Email:** [support@bankintegration.dk](mailto:support@bankintegration.dk)

API-nøglen skal holdes hemmelig og må kun anvendes af ERP-udbyderen.

### 2. **Adgang til kundens profil**

- ERP-udbyderen skal have adgang til en slutkundes profil.
- Opret en testkunde via vores forside ved brug af en ERP-udbyders egen e-mailadresse.
- Testkunden skal have en eller flere konti tilknyttet ERP-udbyderen.

---

## API Endpoint

Alle API-funktioner tilgås via:

```http
https://api.bankintegration.dk
```

---

## HTTP Statuskoder

- **Success:**
  - 200, 201, 202, 204
- **Fejl:**
  - 500-599 for system- eller forbindelsesfejl

Se yderligere detaljer her: [HTTP/1.1 Status Code Definitions](https://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html).

---

## Godkendelse

### Godkendelse af forespørgsler

For alle forespørgsler skal medsendes et JSON godkendelsesobjekt i forespørgslens header. Denne side forklarer beregningsproceduren for objektet og hvordan det indsættes i anmodningen.

Der oprettes et JSON godkendelsesobjekt som nedenfor. JSON objektet sendes med alle HTTP 1.1 GET/POST forespørgsler som et UTF-8 kodet byte array i Base64 format. Værdien indsættes i HTTP 1.1 Authorization header værdien.

For betalingsanmodninger (POST) oprettes et hash objekt for hver transaktion.
For GET forespørgsler (status, kontoudtog m.m.) oprettes kun et hash objekt hvor "id" sættes til værdien i "requestId".

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

For hver transaktion i betalingsanmodningen beregnes en hash værdi som indsættes i godkendelsesobjektets hash-array. 
Alle dele af strengen (Payload) adskilles af et # (pound). Blanke/tomme værdier medtages.

Først sammensættes payload strengen af dels de værdier som der findes i transaktionen og dels af de øverste værdier.

1. En token streng af kundens password som en SHA256 hash over et UTF-8 kodet byte array formateret i HEX.
2. Kundens konto nr. formateres til BBAN (14 cifre).
3. Betalingsdato formateres til YYYYMMDD (Y=år, M=måned, D=dato).
4. Beløb formatet med to decimaler og punktum som decimalseparator (ingen tusindtals adskiller).
5. Valuta formateres til ISO valuta kode (som DKK eller USD).
6. Konto eller betalings ID hentes fra request objekt.
7. ERP udbyders navn/kode indsættes.
8. Betalings ID fra transaktion indsættes.
9. Det aktuelle UTC tidspunkt formateres til YYYYMMDDHHmmSS (Y=år, M=måned, D=dato, H=time (24h), m=minut, S=sekund).

Over hver payload streng (som UTF-8 kodet byte array) genereres nu en HMAC/SHA256 hash med ERP udbyders API-nøgle som nøgle. 
HMAC værdien indsættes sammen med `<PaymentId>` og HMAC som Base64 streng.

### C# kode for beregning af hash

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
