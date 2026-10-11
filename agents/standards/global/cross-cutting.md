# Cross-Cutting Concerns

## 1. Structured Logging & PII Protection

### 1.1 Logging Principles
- Inject `ILogger<T>` via primary constructor.
- Use structured message templates (semantic properties) rather than string interpolation:
  ```csharp
  // Correct
  logger.LogInformation("Parsed {Count} transactions from file {FileName} for user {UserId}", count, fileName, userId);

  // Forbidden
  logger.LogInformation($"Parsed {count} transactions from file {fileName}");
  ```

### 1.2 Data Privacy & Masking
- **Never Log Sensitive PII**: Do not log raw bank account numbers, IBANs, complete credit card numbers, or passkey credentials.
- Mask account numbers: Display only last 4 digits (e.g. `****1234`).
- Sanitize merchant raw strings if they might contain personal identifiers.

---

## 2. Configuration & Secrets Management

- **Development Secrets**: Store local connection strings, third-party OCR API keys, or test credentials using `dotnet user-secrets` (`aspnet-Expenses-0156175d-e6f9-4ec0-b410-24025594cc71`).
- **Production Settings**: Read environment variables via `builder.Configuration`.
- Never commit hardcoded API keys or credentials to Git.

---

## 3. User Feedback & Notifications in Blazor

- In Blazor Server components, provide immediate visual feedback for async actions:
  - Loading indicators during OCR parsing or CSV file parsing.
  - Success toasts/alerts upon importing transactions or updating categories.
  - Inline error banners explaining duplicate transaction warnings or invalid file rows.
- Never let unhandled exceptions show the generic Blazor crash yellow banner in production. Catch domain errors and present clear actionable messages.

---

## 4. File Upload & Ingestion Validation

- Limit uploaded file sizes for screenshots (e.g. max 10MB) and CSVs (e.g. max 5MB).
- Validate MIME types and file extensions (`.png`, `.jpg`, `.jpeg`, `.webp`, `.csv`, `.json`).
- Prevent directory traversal by sanitizing any original file names before saving or caching temporary files.
