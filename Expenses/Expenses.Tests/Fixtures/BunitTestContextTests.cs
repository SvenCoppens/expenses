using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Expenses.Services.Categories;
using Expenses.Services.Transactions;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Expenses.Tests.Fixtures;

public sealed class BunitTestContextTests : BunitTestContext
{
    [Fact]
    public void Constructor_WhenInitialized_ConfiguresDefaultAuthorizedUser()
    {
        // Arrange & Act
        var authContext = AuthContext;

        // Assert
        authContext.Should().NotBeNull();
        authContext.IsAuthenticated.Should().BeTrue();
        authContext.UserName.Should().Be("testuser@example.com");
        authContext.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == DefaultUserId);
    }

    [Fact]
    public void Constructor_WhenInitialized_RegistersMockServicesInDependencyInjection()
    {
        // Arrange & Act
        var categoryService = Services.GetRequiredService<ICategoryService>();
        var transactionService = Services.GetRequiredService<ITransactionService>();

        // Assert
        MockCategoryService.Should().NotBeNull();
        MockTransactionService.Should().NotBeNull();

        categoryService.Should().BeSameAs(MockCategoryService);
        transactionService.Should().BeSameAs(MockTransactionService);
    }

    [Fact]
    public void Constructor_WhenInitialized_ConfiguresLooseJsInteropMode()
    {
        // Arrange & Act & Assert
        JSInterop.Mode.Should().Be(JSRuntimeMode.Loose);
    }

    [Fact]
    public void AuthContext_WhenSetNotAuthorized_UpdatesAuthenticationStateToAnonymous()
    {
        // Arrange
        AuthContext.SetNotAuthorized();

        // Act
        var isAuthenticated = AuthContext.IsAuthenticated;
        var state = AuthContext.State;

        // Assert
        isAuthenticated.Should().BeFalse();
        state.Should().Be(AuthorizationState.Unauthorized);
    }

    [Fact]
    public async Task AuthenticationStateProvider_WhenResolved_ReturnsDefaultUserClaims()
    {
        // Arrange
        var authProvider = Services.GetRequiredService<AuthenticationStateProvider>();

        // Act
        var authState = await authProvider.GetAuthenticationStateAsync();

        // Assert
        authState.Should().NotBeNull();
        authState.User.Identity?.IsAuthenticated.Should().BeTrue();
        authState.User.Identity?.Name.Should().Be("testuser@example.com");
        authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(DefaultUserId);
    }
}
