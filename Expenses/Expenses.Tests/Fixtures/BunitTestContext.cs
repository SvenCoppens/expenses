using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Expenses.Services.Categories;
using Expenses.Services.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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
