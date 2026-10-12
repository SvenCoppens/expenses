# Phase 2: Transactions & Categories Management UI - Architecture Plan

## 1. User Story

### Description

As an authenticated user of the Personal Expense Tracker, I need an intuitive, responsive Blazor Interactive Server user interface to manage my financial transactions and category taxonomy. I need to visualize and organize my spending categories in an extensible two-tier hierarchy (parent and subcategories) with custom color tags, filter and search my transactions across multiple dimensions, perform inline edits on transaction rows directly within the grid, quickly add manual transactions via a dedicated modal, and delete one or multiple records in bulk.

As a developer and test engineer, I need these UI components to be thoroughly testable in isolation using `bUnit`, strictly decoupled from database context instances by communicating only through `ICategoryService` and `ITransactionService`, and safely authenticated using ASP.NET Core cascading authentication state to maintain zero multi-tenant data leakage.

### Acceptance Criteria

- [x] **Navigation Menu Modernization (`NavMenu.razor`)**:
  - Remove placeholder links: Counter (`/counter`), Weather (`/weather`), and Auth Required (`/auth`).
  - Delete or decommission placeholder pages (`Counter.razor`, `Weather.razor`, `Auth.razor`).
  - Add navigation links for **Transactions** (`/transactions`) with icon `bi-receipt` / `bi-credit-card-2-front` and **Categories** (`/categories`) with icon `bi-tags`.
  - Gate financial navigation items (`/transactions`, `/categories`) behind `<AuthorizeView>` so they are visible only to authenticated users.
- [x] **Authentication State Integration**:
  - Components retrieve the current authenticated `userId` via `[CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; }`.
  - Both `/categories` and `/transactions` pages are decorated with `@attribute [Authorize]` and rendered via `@rendermode InteractiveServer`.
  - If a user is not authenticated, navigation is intercepted by `Routes.razor` and redirected to `/Account/Login`.
- [x] **Two-Tier Categories Management UI (`/categories`)**:
  - Display user categories in a two-tier nested tree visualizer (parent categories as cards/rows with nested subcategory badges or child lists).
  - Show color indicators/swatches for each category using `ColorCode` (e.g. `#10B981`) and display sort order.
  - "+ Add Category" modal/form supporting Name (required, max 100), optional Color Picker/HEX input, optional Icon, and Display Order.
  - "+ Add Subcategory" action directly from a parent category row, pre-populating and locking the parent category.
  - "Edit Category" modal supporting renaming, recoloring, and updating display order.
  - "Delete Category" action with confirmation. If the service returns `CATEGORY_IN_USE` error, display a clear inline alert banner: *"Cannot delete category because it is currently assigned to existing transactions. Please reassign those transactions before deleting."*
- [x] **Transactions Filter Toolbar (`/transactions`)**:
  - Date range selector supporting quick presets (*This Month*, *Last Month*, *Year to Date*, *All Time*, *Custom Range*) with `DateOnly` start and end dates.
  - Transaction Type filter dropdown (*All*, *Expense*, *Income*, *Transfer*).
  - Multi-Category filter allowing filtering by one or more categories/subcategories.
  - Real-time or on-submit search bar filtering by description/payee text.
  - Summary metrics display at the top of the grid: Filtered Transaction Count, Total Income (green), Total Expenses (red), and Net Savings.
- [x] **Interactive Transactions Table & Inline Editing (`/transactions`)**:
  - Grid columns: Checkbox selector, Date (`yyyy-MM-dd`), Description, Category (with color badge), Subcategory, Type badge, Amount (formatted in EUR: `€#,##0.00`, colored red for Expense, green for Income), Notes, Actions (Edit, Delete).
  - Inline row editing: Clicking "Edit" puts the active row into edit mode with form inputs (Date picker, Description, Category dropdown, cascading Subcategory dropdown, Type selector, Amount decimal input, Notes).
  - Inline row Save validates inputs, dispatches `ITransactionService.UpdateTransactionAsync`, updates the grid on success, or displays an inline row error on failure.
  - Inline row Cancel reverts local modifications without refetching the entire dataset.
- [x] **Manual Transaction Creation Modal (`/transactions`)**:
  - "+ Add Transaction" button in top toolbar opens a creation modal dialog.
  - Form fields: Date (default: today), Amount (required decimal > 0), Description (required, max 250), Type (default: Expense), Parent Category dropdown, dynamic Subcategory dropdown (cascading from Parent Category), Notes (optional, max 1000).
  - Form validation with error summary and submission via `ITransactionService.CreateTransactionAsync`.
  - On success, modal closes, transaction list refreshes, and success toast/alert appears.
- [x] **Bulk Deletion Action (`/transactions`)**:
  - Checkbox selection: Header "Select All" checkbox toggles all visible rows; individual row checkboxes update selection set.
  - "Delete Selected (N)" button enabled when 1 or more transactions are checked.
  - Displays confirmation dialog showing total records to delete.
  - Dispatches `ITransactionService.DeleteTransactionsBulkAsync(userId, selectedIds, ct)`.
  - Clears selection and refreshes data grid upon completion.
- [x] **bUnit Component Testing Suite (`Expenses.Tests`)**:
  - Configure `bunit` in `Expenses.Tests.csproj`.
  - Implement reusable test harness (`BunitTestContext`) providing mocked authentication (`TestAuthorizationContext`), mock services, and navigation manager.
  - Unit/Component tests verifying:
    - `NavMenu`: Hides protected links when unauthenticated; renders Transactions/Categories when authenticated; placeholder links removed.
    - `Categories`: Renders tree nodes; opens add modal; handles `CATEGORY_IN_USE` failure gracefully.
    - `Transactions`: Renders grid with EUR formatting; filters data; toggles inline edit and updates row; executes bulk deletion.

### Non-Functional Requirements

- **Performance & Circuit Responsiveness**:
  - Minimal SignalR payload size: Avoid transmitting unchanged full entity graphs. Inline edit cancel must restore from local state copy without server roundtrips.
  - Efficient queries: Transaction filtering executes via server-side service queries (`TransactionFilter`) leveraging indexed `(UserId, Date)` columns.
  - Non-blocking async: All UI event handlers yield control using asynchronous service invocations with `CancellationToken`.
- **Security & Multi-Tenancy**:
  - Component code must NEVER accept `userId` from query strings, route parameters, or client inputs. `userId` must strictly come from the verified `AuthenticationState` claims (`ClaimTypes.NameIdentifier`).
  - Unauthenticated access attempts are immediately routed to `/Account/Login`.
- **Concurrency & Reliability**:
  - Pure presentation separation: Razor components inject only `ITransactionService` and `ICategoryService`. Direct injection of `ApplicationDbContext` into Razor components is strictly forbidden.
  - Concurrency safety: Backing services utilize `IDbContextFactory<ApplicationDbContext>` ensuring simultaneous circuit actions do not trigger EF Core threading exceptions.
- **Usability & Design Consistency**:
  - Monetary values formatted with European currency standard (`€#,##0.00`).
  - Visual hierarchy adhering to Bootstrap 5 responsive layout and Bootstrap Icons (`bi bi-*`).
  - Inline feedback: Clear banners and alerts for validation failures; no generic unhandled Blazor exception yellow bars.

---

## 2. Architecture Overview

### Affected Components / Layers

- [x] **Presentation Layer - Navigation & Shell (`Expenses/Components/Layout/`)**:
  - `NavMenu.razor`: Remove Counter/Weather/Auth placeholders; add Transactions and Categories with icons; wrap in `<AuthorizeView>`.
- [x] **Presentation Layer - Pages (`Expenses/Components/Pages/`)**:
  - `Counter.razor`: Delete obsolete placeholder file.
  - `Weather.razor`: Delete obsolete placeholder file.
  - `Auth.razor`: Delete obsolete placeholder file.
  - `Categories.razor` (create): Two-tier category management page with tree visualizer and CRUD modals.
  - `Transactions.razor` (create): Transaction data grid, filter toolbar, inline editing, and bulk deletion.
- [x] **Presentation Layer - Shared Dialogs & Widgets (`Expenses/Components/Shared/`)**:
  - `CategoryDialog.razor` (create): Reusable modal for adding and editing categories and subcategories.
  - `AddTransactionModal.razor` (create): Modal for manual transaction entry with cascading category dropdowns.
  - `ConfirmDialog.razor` (create): Reusable modal for destructive confirmation (single/bulk deletion).
  - `ToastNotification.razor` (create): Transient feedback alert widget for operation success and error banners.
- [x] **Presentation Models / ViewModels (`Expenses/Models/ViewModels/`)**:
  - `CategoryFormModel.cs` (create): Input model with DataAnnotations for Category create/edit forms.
  - `TransactionFormModel.cs` (create): Input model with DataAnnotations for Transaction create modal.
  - `TransactionRowEditModel.cs` (create): Working draft model for inline row editing.
  - `TransactionFilterViewModel.cs` (create): View state model binding date presets, search terms, and selected filters.
- [x] **Test Project (`Expenses.Tests/`)**:
  - `Expenses.Tests.csproj`: Add `bunit` package reference.
  - `Fixtures/BunitTestContext.cs`: Base test harness configuring Blazor test authorization and service mocks.
  - `Components/NavMenuTests.cs`: Tests for navigation links and authorization visibility.
  - `Components/CategoriesPageTests.cs`: Tests for category hierarchy rendering, modal submission, and error handling.
  - `Components/TransactionsPageTests.cs`: Tests for grid rendering, filter changes, inline editing, and bulk deletions.

### Data Flow Diagram

```text
[User Browser Interaction]
         │
         │ (SignalR Circuit Event: Click, Filter, Inline Edit, Modal Submit)
         ▼
[Blazor InteractiveServer Component] (Categories.razor / Transactions.razor)
         │
         ├─► 1. Extract Authenticated UserId from [CascadingParameter] Task<AuthenticationState>
         │        (If unauthenticated -> RedirectToLogin)
         │
         ├─► 2. Validate Local Form / Row Edit Model (DataAnnotations)
         │
         ├─► 3. Invoke Service Method (userId, requestDto, CancellationToken)
         │        │
         │        ▼
         │   [ICategoryService / ITransactionService]
         │        │
         │        ├─► Creates short-lived context via dbContextFactory.CreateDbContextAsync()
         │        ├─► Enforces tenant scoping: .Where(x => x.UserId == userId)
         │        └─► Returns Result<T> / Result (Success or Failure with ErrorCode)
         │
         ├─► 4. Evaluate Service Result:
         │        ├─► If Success: Update local state collection, close modal, exit edit mode
         │        └─► If Failure: Display inline error banner (e.g. CATEGORY_IN_USE)
         │
         ▼
[Component Re-render] -> Virtual DOM diff pushed over SignalR circuit to Browser
```

### Key Design Decisions

1. **Cascading Authentication State with Centralized User Context**:
   - *Rationale*: Blazor Server applications using ASP.NET Core Identity expose `AuthenticationState` through `CascadingAuthenticationState`.
   - *Implementation*: Pages declare `[CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;`. In `OnInitializedAsync`, the component retrieves the `ClaimsPrincipal` and extracts the user ID (`user.FindFirst(ClaimTypes.NameIdentifier)?.Value`). If null or unauthenticated, the component exits gracefully while `<AuthorizeRouteView>` handles redirection.
   - *Alternative Considered*: Injecting `UserManager<ApplicationUser>` directly in components. Rejected because `UserManager` interacts with the database using scoped identity stores, which risks concurrency conflicts and breaks presentation-to-service decoupling.

2. **Isolated Row Edit State for Optimistic Inline Grid Editing**:
   - *Rationale*: In an interactive transaction table, modifying a row should not block or reload the rest of the table. When a user cancels an inline edit, the previous values must be immediately restored without an expensive database query.
   - *Implementation*: The component maintains `private Guid? _editingTransactionId` and a separate clone `TransactionRowEditModel _activeRowEdit`. Clicking "Edit" creates a shallow clone of the target DTO into the edit model. "Save" executes `ITransactionService.UpdateTransactionAsync`; upon success, the row in the local list is updated and `_editingTransactionId = null`. "Cancel" simply sets `_editingTransactionId = null`, discarding edits instantly with zero network traffic.

3. **Cascading Two-Tier Category Dropdowns in Transaction Forms**:
   - *Rationale*: A transaction can belong to a Parent Category (e.g., Food) and an optional Subcategory (e.g., Groceries). Subcategories must strictly belong to the selected Parent Category.
   - *Implementation*: When the user selects a Parent Category in either the inline grid editor or the Add Transaction modal, the Subcategory dropdown dynamically filters its options to only show `parentCategory.Subcategories`. If the parent category is cleared or changed, any previously selected subcategory that does not belong to the new parent is automatically reset to null.

4. **Component Decoupling via Dedicated Dialog Widgets**:
   - *Rationale*: Embedding complex modal HTML, validation logic, and state into a single 1000-line Razor page makes unit testing and maintenance difficult.
   - *Implementation*: Modals are isolated into focused child components (`CategoryDialog.razor`, `AddTransactionModal.razor`, `ConfirmDialog.razor`). Parents control modal visibility via standard `EventCallback` parameters (`OnSave`, `OnCancel`).

5. **Component Testing with bUnit and Mocked Service Contracts**:
   - *Rationale*: End-to-end browser tests (like Playwright/Selenium) are slow and fragile for verifying micro-interactions (e.g. button disabling, filter presets, error banners).
   - *Implementation*: Use `bUnit` to mount Razor components in memory. Mock `ICategoryService` and `ITransactionService` using `NSubstitute`. Use bUnit's `TestAuthorizationContext` to simulate authenticated users with specific user IDs.

---

## 3. Implementation Steps

_Note: The Test_Engineer will write failing tests against these steps and touchpoints before the Software_Engineer begins implementation._

### Step 1: bUnit Infrastructure & Test Project Configuration (`Expenses.Tests`)

**Status**: [✅ Done]

#### Objectives

- Add `bunit` package reference to `Expenses.Tests/Expenses.Tests.csproj`.
- Create a reusable `BunitTestContext` test fixture that initializes `TestContext`, configures authentication test services (`AddTestAuthorization()`), and registers mock service instances.
- Ensure all existing unit tests in `Expenses.Tests` continue to compile and pass.

#### Touchpoints (Files to Create/Modify)

- `Expenses.Tests/Expenses.Tests.csproj` (modify)
- `Expenses.Tests/Fixtures/BunitTestContext.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// File: Expenses.Tests/Expenses.Tests.csproj
// Add PackageReference:
// <PackageReference Include="bunit" Version="1.38.5" />

namespace Expenses.Tests.Fixtures;

public abstract class BunitTestContext : TestContext
{
    protected TestAuthorizationContext AuthContext { get; }
    protected ICategoryService MockCategoryService { get; }
    protected ITransactionService MockTransactionService { get; }

    public const string DefaultUserId = "test-user-guid-123";

    protected BunitTestContext()
    {
        AuthContext = this.AddTestAuthorization();
        AuthContext.SetAuthorized("testuser@example.com");
        AuthContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, DefaultUserId));

        MockCategoryService = Substitute.For<ICategoryService>();
        MockTransactionService = Substitute.For<ITransactionService>();

        Services.AddSingleton(MockCategoryService);
        Services.AddSingleton(MockTransactionService);
        
        // Suppress JSInterop calls by default
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
```

#### Definition of Done

- [x] `bunit` package is added and `Expenses.Tests` compiles cleanly.
- [x] `BunitTestContext` provides authenticated claims and mocked services.
- [x] Running `dotnet test Expenses.sln` completes with 100% passing tests.

---

### Step 2: Clean Navigation Layout & Authentication Gating (`Expenses/Components/Layout/NavMenu.razor`)

**Status**: [✅ Done]

#### Objectives

- Clean up placeholder links in `NavMenu.razor`: remove Counter (`counter`), Weather (`weather`), and Auth Required (`auth`).
- Safely remove or decommission placeholder pages (`Counter.razor`, `Weather.razor`, `Auth.razor`).
- Add navigation links for Transactions (`transactions`) and Categories (`categories`) with proper Bootstrap icons.
- Enclose the Transactions and Categories links within `<AuthorizeView><Authorized>...</Authorized></AuthorizeView>` to ensure only logged-in users see them.
- Add bUnit tests in `Expenses.Tests/Components/NavMenuTests.cs` verifying link visibility for authenticated vs anonymous users.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Components/Layout/NavMenu.razor` (modify)
- `Expenses/Components/Pages/Counter.razor` (delete)
- `Expenses/Components/Pages/Weather.razor` (delete)
- `Expenses/Components/Pages/Auth.razor` (delete)
- `Expenses.Tests/Components/NavMenuTests.cs` (create)

#### Pseudo-code / Technical Notes

```razor
<!-- In Expenses/Components/Layout/NavMenu.razor -->
<div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">
    <nav class="nav flex-column">
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
                <span class="bi bi-house-door-fill-nav-menu" aria-hidden="true"></span> Home
            </NavLink>
        </div>

        <AuthorizeView>
            <Authorized>
                <div class="nav-item px-3">
                    <NavLink class="nav-link" href="transactions">
                        <span class="bi bi-credit-card-2-front-nav-menu" aria-hidden="true"></span> Transactions
                    </NavLink>
                </div>
                <div class="nav-item px-3">
                    <NavLink class="nav-link" href="categories">
                        <span class="bi bi-tags-fill-nav-menu" aria-hidden="true"></span> Categories
                    </NavLink>
                </div>
                <div class="nav-item px-3">
                    <NavLink class="nav-link" href="Account/Manage">
                        <span class="bi bi-person-fill-nav-menu" aria-hidden="true"></span> @context.User.Identity?.Name
                    </NavLink>
                </div>
                <!-- Logout Form -->
            </Authorized>
            <NotAuthorized>
                <!-- Register / Login Links -->
            </NotAuthorized>
        </AuthorizeView>
    </nav>
</div>
```

```csharp
// In Expenses.Tests/Components/NavMenuTests.cs
public class NavMenuTests : BunitTestContext
{
    [Fact]
    public void NavMenu_WhenAnonymous_HidesTransactionsAndCategories()
    {
        AuthContext.SetNotAuthorized();
        var cut = RenderComponent<NavMenu>();
        cut.FindAll("a[href='transactions']").Should().BeEmpty();
        cut.FindAll("a[href='categories']").Should().BeEmpty();
    }

    [Fact]
    public void NavMenu_WhenAuthorized_ShowsTransactionsAndCategories()
    {
        AuthContext.SetAuthorized("user@test.com");
        var cut = RenderComponent<NavMenu>();
        cut.FindAll("a[href='transactions']").Should().NotBeEmpty();
        cut.FindAll("a[href='categories']").Should().NotBeEmpty();
    }
}
```

#### Definition of Done

- [x] Placeholder Counter, Weather, and Auth links are removed from `NavMenu.razor`.
- [x] Transactions and Categories links are rendered under `<Authorized>` block.
- [x] Obsolete placeholder razor files are safely removed without breaking build.
- [x] `NavMenuTests` pass cleanly.

---

### Step 3: Two-Tier Categories Management UI (`Expenses/Components/Pages/Categories.razor`)

**Status**: [✅ Done]

#### Objectives

- Create the Categories page at route `/categories`, protected with `@attribute [Authorize]` and rendered as `@rendermode InteractiveServer`.
- Retrieve authenticated `userId` from cascading authentication state.
- Implement two-tier tree visualizer:
  - Parent categories rendered with name, display order, and color preview badge (`ColorCode`).
  - Subcategories nested under their respective parent categories with badges/indentation.
- Implement Add / Edit category modal dialog (`CategoryDialog.razor`):
  - Form validation: Name (required, max 100), ColorCode (valid HEX format or color picker), DisplayOrder (integer).
  - Parent category selector: None for parent category; or locked/selectable parent category for subcategories.
- Implement Delete category workflow:
  - Deletion confirmation dialog.
  - Call `ICategoryService.DeleteCategoryAsync(userId, categoryId, ct)`.
  - If service fails with `CATEGORY_IN_USE` code, display an alert banner: *"Cannot delete category because it is currently assigned to existing transactions. Please reassign those transactions before deleting."*
- Add component tests in `Expenses.Tests/Components/CategoriesPageTests.cs`.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Models/ViewModels/CategoryFormModel.cs` (create)
- `Expenses/Components/Shared/CategoryDialog.razor` (create)
- `Expenses/Components/Pages/Categories.razor` (create)
- `Expenses.Tests/Components/CategoriesPageTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// File: Expenses/Models/ViewModels/CategoryFormModel.cs
public class CategoryFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    [RegularExpression("^#(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "Color must be a valid hex code (e.g. #FF5733).")]
    public string? ColorCode { get; set; } = "#3B82F6";

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }
}
```

```razor
<!-- Outline: Expenses/Components/Pages/Categories.razor -->
@page "/categories"
@attribute [Authorize]
@rendermode InteractiveServer
@inject ICategoryService CategoryService

<PageTitle>Categories - Expenses</PageTitle>

<div class="d-flex justify-content-between align-items-center mb-4">
    <h2>Categories</h2>
    <button class="btn btn-primary" @onclick="OpenAddParentModal">
        <span class="bi bi-plus-lg"></span> Add Parent Category
    </button>
</div>

@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="alert alert-danger alert-dismissible fade show" role="alert">
        @errorMessage
        <button type="button" class="btn-close" @onclick="() => errorMessage = null"></button>
    </div>
}

@if (isLoading)
{
    <div class="spinner-border text-primary" role="status"></div>
}
else
{
    <div class="category-tree">
        @foreach (var parent in categories)
        {
            <div class="card mb-3 shadow-sm">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <div class="d-flex align-items-center gap-2">
                        <span class="color-badge" style="background-color: @parent.ColorCode"></span>
                        <h5 class="mb-0">@parent.Name</h5>
                        <span class="badge bg-secondary">Order: @parent.DisplayOrder</span>
                    </div>
                    <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-success" @onclick="() => OpenAddSubcategoryModal(parent)">
                            + Add Subcategory
                        </button>
                        <button class="btn btn-outline-primary" @onclick="() => OpenEditModal(parent)">Edit</button>
                        <button class="btn btn-outline-danger" @onclick="() => ConfirmDelete(parent.Id, parent.Name)">Delete</button>
                    </div>
                </div>
                <div class="card-body">
                    @if (parent.Subcategories.Count == 0)
                    {
                        <p class="text-muted mb-0 small">No subcategories defined.</p>
                    }
                    else
                    {
                        <div class="d-flex flex-wrap gap-2">
                            @foreach (var sub in parent.Subcategories)
                            {
                                <div class="badge-item d-flex align-items-center gap-1 border rounded px-2 py-1">
                                    <span class="color-badge small" style="background-color: @(sub.ColorCode ?? parent.ColorCode)"></span>
                                    <span>@sub.Name</span>
                                    <button class="btn btn-link btn-xs text-primary p-0" @onclick="() => OpenEditModal(sub, parent.Id)">✎</button>
                                    <button class="btn btn-link btn-xs text-danger p-0" @onclick="() => ConfirmDelete(sub.Id, sub.Name)">✕</button>
                                </div>
                            }
                        </div>
                    }
                </div>
            </div>
        }
    </div>
}

<CategoryDialog @ref="dialog" OnSave="HandleCategorySaved" />
<ConfirmDialog @ref="confirmDialog" OnConfirmed="HandleDeleteConfirmed" />
```

#### Definition of Done

- [x] `/categories` route loads two-tier hierarchy from `ICategoryService.GetCategoriesAsync`.
- [x] Add/Edit modal creates and updates parent categories and subcategories.
- [x] Deletion of category in use surfaces `CATEGORY_IN_USE` error banner without throwing exceptions.
- [x] Component tests in `CategoriesPageTests` cover tree rendering, modal submissions, and error banner display.

---

### Step 4: Transactions Filter Toolbar & Summary Metrics (`Expenses/Components/Pages/Transactions.razor`)

**Status**: [✅ Done]

#### Objectives

- Create the base structure of `/transactions` page with `@attribute [Authorize]` and `@rendermode InteractiveServer`.
- Build the filter toolbar:
  - Date Range selector: Preset dropdown (*This Month*, *Last Month*, *Year to Date*, *All Time*, *Custom*) and explicit Start/End Date inputs.
  - Transaction Type filter (*All*, *Expense*, *Income*, *Transfer*).
  - Category selector: Grouped dropdown of parent categories and subcategories.
  - Search input: Text filter matching description.
- Implement summary metrics bar:
  - Count of matching transactions.
  - Total Income (EUR, formatted green).
  - Total Expenses (EUR, formatted red).
  - Net Balance / Net Savings (Income minus Expenses).
- Implement reactive filtering: Changes to filters construct `TransactionFilter` and query `ITransactionService.GetTransactionsAsync(userId, filter, ct)`.
- Add component tests in `Expenses.Tests/Components/TransactionsFilterTests.cs`.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Models/ViewModels/TransactionFilterViewModel.cs` (create)
- `Expenses/Components/Pages/Transactions.razor` (create base structure)
- `Expenses.Tests/Components/TransactionsFilterTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// File: Expenses/Models/ViewModels/TransactionFilterViewModel.cs
public class TransactionFilterViewModel
{
    public string DatePreset { get; set; } = "ThisMonth";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TransactionType? Type { get; set; }
    public Guid? SelectedCategoryId { get; set; }
    public string? SearchTerm { get; set; }

    public void ApplyPreset(string preset)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DatePreset = preset;
        switch (preset)
        {
            case "ThisMonth":
                StartDate = new DateOnly(today.Year, today.Month, 1);
                EndDate = StartDate.Value.AddMonths(1).AddDays(-1);
                break;
            case "LastMonth":
                var firstLastMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                StartDate = firstLastMonth;
                EndDate = firstLastMonth.AddMonths(1).AddDays(-1);
                break;
            case "YearToDate":
                StartDate = new DateOnly(today.Year, 1, 1);
                EndDate = today;
                break;
            case "AllTime":
                StartDate = null;
                EndDate = null;
                break;
        }
    }

    public TransactionFilter ToFilter() => new(
        StartDate: StartDate,
        EndDate: EndDate,
        CategoryIds: SelectedCategoryId.HasValue ? [SelectedCategoryId.Value] : null,
        Type: Type,
        SearchTerm: SearchTerm
    );
}
```

```razor
<!-- Toolbar snippet in Transactions.razor -->
<div class="card mb-3 p-3 bg-light">
    <div class="row g-2 align-items-end">
        <div class="col-md-2">
            <label class="form-label small">Date Preset</label>
            <select class="form-select form-select-sm" @bind="filterVm.DatePreset" @bind:after="OnPresetChanged">
                <option value="ThisMonth">This Month</option>
                <option value="LastMonth">Last Month</option>
                <option value="YearToDate">Year to Date</option>
                <option value="AllTime">All Time</option>
                <option value="Custom">Custom</option>
            </select>
        </div>
        <div class="col-md-2">
            <label class="form-label small">From</label>
            <input type="date" class="form-control form-control-sm" @bind="filterVm.StartDate" @bind:after="LoadTransactionsAsync" />
        </div>
        <div class="col-md-2">
            <label class="form-label small">To</label>
            <input type="date" class="form-control form-control-sm" @bind="filterVm.EndDate" @bind:after="LoadTransactionsAsync" />
        </div>
        <div class="col-md-2">
            <label class="form-label small">Type</label>
            <select class="form-select form-select-sm" @bind="filterVm.Type" @bind:after="LoadTransactionsAsync">
                <option value="">All Types</option>
                <option value="@TransactionType.Expense">Expense</option>
                <option value="@TransactionType.Income">Income</option>
                <option value="@TransactionType.Transfer">Transfer</option>
            </select>
        </div>
        <div class="col-md-3">
            <label class="form-label small">Search</label>
            <input type="text" class="form-control form-control-sm" placeholder="Search payee/description..." @bind="filterVm.SearchTerm" @bind:after="LoadTransactionsAsync" />
        </div>
        <div class="col-md-1">
            <button class="btn btn-outline-secondary btn-sm w-100" @onclick="ResetFilters">Reset</button>
        </div>
    </div>
</div>

<!-- Summary Metrics Bar -->
<div class="row g-3 mb-3 text-center">
    <div class="col-md-4">
        <div class="card p-2 border-success text-success">
            <div class="small">Total Income</div>
            <div class="h5 mb-0">+€@totalIncome.ToString("N2")</div>
        </div>
    </div>
    <div class="col-md-4">
        <div class="card p-2 border-danger text-danger">
            <div class="small">Total Expenses</div>
            <div class="h5 mb-0">-€@totalExpenses.ToString("N2")</div>
        </div>
    </div>
    <div class="col-md-4">
        <div class="card p-2 border-primary text-primary">
            <div class="small">Net Balance</div>
            <div class="h5 mb-0">€@netBalance.ToString("N2")</div>
        </div>
    </div>
</div>
```

#### Definition of Done

- [x] Filter toolbar updates trigger service calls with populated `TransactionFilter`.
- [x] Summary cards accurately compute totals and net balances for filtered records.
- [x] Tests in `TransactionsFilterTests` verify filter mappings, date preset calculations, and summary aggregates.

---

### Step 5: Interactive Transactions Grid with Inline Row Editing (`Expenses/Components/Pages/Transactions.razor`)

**Status**: [✅ Done]

#### Objectives

- Build the interactive transaction data grid in `Transactions.razor`:
  - Columns: Checkbox, Date, Description, Category, Subcategory, Type, Amount (EUR formatting `€#,##0.00`, color coding), Notes, Actions.
- Implement inline row editing:
  - "Edit" button sets `_editingId = transaction.Id` and initializes `_editRow` model.
  - Active row transforms into input controls: Date picker, Description input, Type dropdown, Parent Category dropdown, dynamic Subcategory dropdown, Amount decimal input, Notes input.
  - Save button: Validates inputs, calls `ITransactionService.UpdateTransactionAsync(userId, id, request, ct)`. On success, updates local record and exits edit mode. On failure, renders inline row error.
  - Cancel button: Reverts local changes and exits edit mode without refetching from server.
- Add component tests in `Expenses.Tests/Components/TransactionsGridTests.cs`.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Models/ViewModels/TransactionRowEditModel.cs` (create)
- `Expenses/Components/Pages/Transactions.razor` (modify)
- `Expenses.Tests/Components/TransactionsGridTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// File: Expenses/Models/ViewModels/TransactionRowEditModel.cs
public class TransactionRowEditModel
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string? Notes { get; set; }

    public static TransactionRowEditModel FromDto(TransactionDto dto) => new()
    {
        Id = dto.Id,
        Date = dto.Date,
        Amount = dto.Amount,
        Description = dto.Description,
        Type = dto.Type,
        CategoryId = dto.CategoryId,
        SubcategoryId = dto.SubcategoryId,
        Notes = dto.Notes
    };

    public UpdateTransactionRequest ToRequest() => new(
        Date: Date,
        Amount: Amount,
        Description: Description,
        Type: Type,
        CategoryId: CategoryId,
        SubcategoryId: SubcategoryId,
        Notes: Notes
    );
}
```

```razor
<!-- Row rendering inside Transactions.razor table body -->
@foreach (var tx in transactions)
{
    @if (editingId == tx.Id)
    {
        <tr class="table-warning">
            <td><!-- Selection checkbox disabled while editing --></td>
            <td><input type="date" class="form-control form-control-sm" @bind="editRow.Date" /></td>
            <td><input type="text" class="form-control form-control-sm" @bind="editRow.Description" /></td>
            <td>
                <select class="form-select form-select-sm" @bind="editRow.CategoryId" @bind:after="OnEditCategoryChanged">
                    <option value="">-- None --</option>
                    @foreach (var c in categories) { <option value="@c.Id">@c.Name</option> }
                </select>
            </td>
            <td>
                <select class="form-select form-select-sm" @bind="editRow.SubcategoryId">
                    <option value="">-- None --</option>
                    @foreach (var sc in GetSubcategoriesFor(editRow.CategoryId)) { <option value="@sc.Id">@sc.Name</option> }
                </select>
            </td>
            <td>
                <select class="form-select form-select-sm" @bind="editRow.Type">
                    <option value="@TransactionType.Expense">Expense</option>
                    <option value="@TransactionType.Income">Income</option>
                    <option value="@TransactionType.Transfer">Transfer</option>
                </select>
            </td>
            <td><input type="number" step="0.01" class="form-control form-control-sm" @bind="editRow.Amount" /></td>
            <td><input type="text" class="form-control form-control-sm" @bind="editRow.Notes" /></td>
            <td>
                <button class="btn btn-sm btn-success" @onclick="SaveRowEditAsync">✓</button>
                <button class="btn btn-sm btn-secondary" @onclick="CancelRowEdit">✕</button>
            </td>
        </tr>
    }
    else
    {
        <tr>
            <td><input type="checkbox" checked="@selectedIds.Contains(tx.Id)" @onchange="e => ToggleSelection(tx.Id, e)" /></td>
            <td>@tx.Date.ToString("yyyy-MM-dd")</td>
            <td>@tx.Description</td>
            <td><span class="badge" style="background-color: @GetCategoryColor(tx.CategoryId)">@tx.CategoryName</span></td>
            <td><span class="badge bg-light text-dark">@tx.SubcategoryName</span></td>
            <td><span class="badge @GetTypeBadgeClass(tx.Type)">@tx.Type</span></td>
            <td class="@GetAmountClass(tx.Type) text-end font-monospace">@FormatCurrency(tx.Amount, tx.Type)</td>
            <td>@tx.Notes</td>
            <td>
                <button class="btn btn-sm btn-outline-primary" @onclick="() => StartRowEdit(tx)">Edit</button>
                <button class="btn btn-sm btn-outline-danger" @onclick="() => ConfirmDeleteSingle(tx.Id)">Delete</button>
            </td>
        </tr>
    }
}
```

#### Definition of Done

- [x] Table renders transactions with correct styling, currency formatting, and badges.
- [x] Inline edit mode toggles seamlessly and binds to working model.
- [x] Saving updates transaction via service and refreshes row in local state.
- [x] Canceling restores previous row state without database query.
- [x] Component tests in `TransactionsGridTests` verify edit activation, saving, and cancellation.

---

### Step 6: Manual Transaction Creation Modal & Bulk Deletion Action (`Expenses/Components/Pages/Transactions.razor` & Dialogs)

**Status**: [✅ Done]

#### Objectives

- Build manual "+ Add Transaction" modal (`AddTransactionModal.razor`):
  - Form validation: Date (required), Amount (decimal > 0), Description (required, max 250), Type (required), Category & cascading Subcategory, Notes (optional, max 1000).
  - Submit dispatches `ITransactionService.CreateTransactionAsync(userId, request, ct)`.
  - On success, modal closes, newly created transaction is added to view, and success banner displays.
- Implement Bulk Deletion workflow:
  - Header checkbox: Select All visible rows toggles state across all items.
  - Row checkboxes update `HashSet<Guid> _selectedTransactionIds`.
  - Action button: "Delete Selected (<count>)" enabled only when selection count > 0.
  - Confirmation modal warning user before deletion.
  - Dispatches `ITransactionService.DeleteTransactionsBulkAsync(userId, ids, ct)`.
  - On success, clears selection and reloads transactions list.
- Add component tests in `Expenses.Tests/Components/TransactionsBulkActionsTests.cs`.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Models/ViewModels/TransactionFormModel.cs` (create)
- `Expenses/Components/Shared/AddTransactionModal.razor` (create)
- `Expenses/Components/Shared/ConfirmDialog.razor` (create)
- `Expenses/Components/Pages/Transactions.razor` (modify)
- `Expenses.Tests/Components/TransactionsBulkActionsTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// File: Expenses/Models/ViewModels/TransactionFormModel.cs
public class TransactionFormModel
{
    [Required]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Range(0.01, 1_000_000_000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string Description { get; set; } = string.Empty;

    public TransactionType Type { get; set; } = TransactionType.Expense;
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    public CreateTransactionRequest ToRequest() => new(
        Date: Date,
        Amount: Amount,
        Description: Description.Trim(),
        Type: Type,
        CategoryId: CategoryId,
        SubcategoryId: SubcategoryId,
        Notes: Notes?.Trim()
    );
}
```

```razor
<!-- Bulk action bar in Transactions.razor -->
<div class="d-flex justify-content-between align-items-center mb-3">
    <div class="d-flex align-items-center gap-2">
        <button class="btn btn-primary" @onclick="OpenAddTransactionModal">
            <span class="bi bi-plus-lg"></span> + Add Transaction
        </button>
        @if (selectedIds.Count > 0)
        {
            <button class="btn btn-danger" @onclick="ConfirmBulkDelete">
                <span class="bi bi-trash"></span> Delete Selected (@selectedIds.Count)
            </button>
        }
    </div>
    <div class="text-muted small">
        Showing @transactions.Count transactions
    </div>
</div>

<AddTransactionModal @ref="addModal" Categories="categories" OnTransactionCreated="HandleTransactionCreated" />
<ConfirmDialog @ref="confirmBulkDialog" OnConfirmed="HandleBulkDeleteConfirmed" />
```

#### Definition of Done

- [x] Add transaction modal validates input and creates new transaction.
- [x] Header select all and individual row checkboxes properly track selected IDs.
- [x] Bulk delete removes selected transactions in single service invocation.
- [x] Component tests in `TransactionsBulkActionsTests` verify creation flow and bulk deletion.

---

### Step 7: End-to-End Integration Verification & Solution Polish

**Status**: [✅ Done]

#### Objectives

- Verify full integration between `NavMenu`, `Categories.razor`, `Transactions.razor`, and ASP.NET Core Identity authentication.
- Verify unauthorized requests to `/categories` and `/transactions` automatically redirect to `/Account/Login`.
- Verify full test suite execution with zero warnings and 100% green tests:
  - Unit tests for services and DTOs.
  - bUnit component tests for `NavMenu`, `Categories`, and `Transactions`.
- Execute `dotnet build Expenses.sln` and `dotnet test Expenses.sln` to confirm complete solution integrity.

#### Touchpoints (Files to Create/Modify)

- `Expenses.Tests/Integration/ExpenseTrackerUiIntegrationTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// Verification workflow:
// 1. Build solution: dotnet build Expenses.sln
// 2. Test solution: dotnet test Expenses.sln
// 3. Verify zero failing tests and zero regression across Phase 1 & Phase 2 test suites.
```

#### Definition of Done

- [x] Complete solution builds cleanly with zero compilation errors (`dotnet build Expenses.sln`).
- [x] All unit and bUnit test cases pass (`dotnet test Expenses.sln`).
- [x] All Phase 2 acceptance criteria from PRS Pillar 1 & 3 are fully satisfied.

---

## 4. Progress Tracking (State Machine)

| Step | Status    | Task Description                                                  | Target File(s)                                                                                                    |
| ---- | --------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 1    | [✅ Done] | Configure `bunit` and create reusable component test context       | `Expenses.Tests/Expenses.Tests.csproj`, `Expenses.Tests/Fixtures/BunitTestContext.cs`                              |
| 2    | [✅ Done] | Clean `NavMenu.razor`, remove placeholders, and gate links        | `Expenses/Components/Layout/NavMenu.razor`, `Expenses/Components/Pages/Counter.razor`, `Weather.razor`, `Auth.razor`, `Expenses.Tests/Components/NavMenuTests.cs` |
| 3    | [✅ Done] | Implement two-tier Categories page, modals, and error banners     | `Expenses/Models/ViewModels/CategoryFormModel.cs`, `Expenses/Components/Shared/CategoryDialog.razor`, `Expenses/Components/Pages/Categories.razor`, `Expenses.Tests/Components/CategoriesPageTests.cs` |
| 4    | [✅ Done] | Implement Transactions filter toolbar and summary metrics         | `Expenses/Models/ViewModels/TransactionFilterViewModel.cs`, `Expenses/Components/Pages/Transactions.razor`, `Expenses.Tests/Components/TransactionsFilterTests.cs` |
| 5    | [✅ Done] | Build interactive Transactions grid with inline row editing       | `Expenses/Models/ViewModels/TransactionRowEditModel.cs`, `Expenses/Components/Pages/Transactions.razor`, `Expenses.Tests/Components/TransactionsGridTests.cs` |
| 6    | [✅ Done] | Implement Add Transaction modal and bulk deletion workflow        | `Expenses/Models/ViewModels/TransactionFormModel.cs`, `Expenses/Components/Shared/AddTransactionModal.razor`, `Expenses/Components/Shared/ConfirmDialog.razor`, `Expenses.Tests/Components/TransactionsBulkActionsTests.cs` |
| 7    | [✅ Done] | End-to-end integration verification, auth routing, and test suite | `Expenses.Tests/Integration/ExpenseTrackerUiIntegrationTests.cs`                                                  |

**Status Legend:**

- `[⏳ Pending]` - Not started.
- `[🔄 In Progress]` - Software_Engineer is currently executing.
- `[✅ Done]` - Compiled, tests passed, and code reviewed.
- `[⏸️ Blocked]` - Halted due to technical blockers.
