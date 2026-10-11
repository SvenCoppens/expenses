# Expense Domain Standards: Ingestion, Deduplication & Taxonomy

## 1. Multi-Modal Ingestion Pipeline

The application processes transaction records from three distinct ingestion channels into a unified `NormalizedTransactionDto`.

```text
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Screenshot / OCR│    │  Bank API JSON  │    │  CSV Statement  │
└────────┬────────┘    └────────┬────────┘    └────────┬────────┘
         │                      │                      │
         ▼                      ▼                      ▼
┌───────────────────────────────────────────────────────────────┐
│               Unified NormalizedTransactionDto                │
│ (Date, Amount, Currency, Merchant, RawText, Source, ExternalId)│
└───────────────────────────────┬───────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────┐
│                     Deduplication Engine                      │
│            • SHA256 Exact Fingerprint Matching                │
│            • Time Window (+/- 3 days) + Fuzzy Merchant Match  │
└───────────────────────────────┬───────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────┐
│                   Category Assignment Engine                  │
│            • Rule Evaluation (Regex & Keyword Match)          │
│            • Fallback to Parent or "Uncategorized"            │
└───────────────────────────────────────────────────────────────┘
```

### 1.1 Ingestion Sources

1. **Screenshots / Receipt Images**:
   - Accepts image streams (`.png`, `.jpg`, `.jpeg`, `.webp`).
   - Extracts structured transaction fields: Date, Total Amount, Currency code, Merchant Name, Line items (if present), and OCR confidence score.
   - Retains original image reference or thumbnail for user verification during review.

2. **Bank API JSON**:
   - Ingests structured JSON payloads from banking APIs (e.g., Open Banking / Plaid / PSD2 formats).
   - Normalizes external identifiers (`TransactionId`, `AccountId`), timestamps, pending vs settled statuses, and native bank category hints.

3. **CSV Bank Statements**:
   - Supports variable delimiters (`,`, `;`, `\t`).
   - Flexible column mapping with auto-detection for common banking export headers (e.g., `Date`, `Amount`, `Description`, `Payee`, `Debit`, `Credit`, `Balance`).
   - Culture-aware parsing for numbers (`1.234,56` vs `1,234.56`) and date formats (`DD/MM/YYYY` vs `YYYY-MM-DD`).

---

## 2. Deduplication Engine & Matching Algorithms

### 2.1 Matching Strategies

1. **Exact Fingerprint Match**:
   - Generates a deterministic hash for each transaction:
     ```text
     Fingerprint = SHA256(UserId + ISO_Date + FormattedAmount + NormalizedMerchant)
     ```
   - If an existing transaction has the identical fingerprint, it is flagged as `ExactDuplicate`.

2. **Fuzzy Window Match**:
   - Evaluates whether another transaction exists where:
     - Absolute amount matches exactly (`Math.Abs(t1.Amount - t2.Amount) < 0.001m`).
     - Transaction date is within a +/- 3 day window (accounting for weekend clearing delays and bank posting lags).
     - Normalized merchant name has a string similarity score >= 0.8 (Levenshtein distance or token similarity, e.g., `"Amazon EU SARL"` vs `"Amazon.com"`).
   - Flagged as `PotentialDuplicate` with candidate reference for side-by-side user review.

### 2.2 Deduplication Statuses & Actions
- `Unique`: No duplicate found. Ready for immediate import.
- `PotentialDuplicate`: Requires user confirmation in the UI.
- `ExactDuplicate`: Default to skipped, with option for user override.
- User actions in Blazor UI: **Import as New**, **Merge/Link with Existing**, or **Dismiss/Skip**.

---

## 3. Extensible Category Hierarchy

### 3.1 Two-Tier Hierarchical Model
- **Categories** support parent-child relationships (`ParentCategoryId` nullable foreign key).
- Parent categories represent broad spending sectors (e.g. `Housing`, `Food & Dining`, `Transportation`, `Health`, `Entertainment`, `Income`).
- Subcategories represent specific granular allocations (e.g. `Food & Dining` -> `Groceries`, `Restaurants`, `Coffee`, `Delivery`).

### 3.2 Dual Extensibility (Developer & User)
- **System Default Categories**: Seeded in database, flagged with `IsSystemDefault = true`. Cannot be deleted by user, but can be customized or hidden.
- **User-Defined Categories**: Users can dynamically create, rename, re-parent, color-code, and archive custom categories through the Blazor interface.
- **Developer Extension**: New default categories and seeded rules can be added via seed scripts and EF Core migrations.

### 3.3 Rule-Based Auto-Categorization
- Each category can have associated `CategoryRule` entities.
- **Rule Criteria**:
  - `Pattern`: Substring or regular expression (e.g., `*uber*`, `*lyft*`, `*starbucks*`).
  - `MatchField`: Target property (`MerchantName`, `RawDescription`, `Notes`).
  - `Priority`: Integer priority ordering for conflict resolution.
- **Auto-Learning Mechanism**: When a user manually changes or assigns a category to an uncategorized transaction, the system prompts the user: *"Always assign future transactions from '[Merchant]' to '[Category]'?"* to automatically create a rule.
