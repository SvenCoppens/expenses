using Bunit;
using Expenses.Components.Layout;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Expenses.Tests.Components;

public sealed class NavMenuTests : BunitTestContext
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NavMenu_Always_RendersHomeLink(bool isAuthenticated)
    {
        // Arrange
        if (isAuthenticated)
        {
            AuthContext.SetAuthorized("testuser@example.com");
        }
        else
        {
            AuthContext.SetNotAuthorized();
        }

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        var homeLinks = cut.FindAll("nav a")
            .Where(a => a.GetAttribute("href") is "" or "/")
            .ToList();

        homeLinks.Should().ContainSingle("home navigation link should be rendered exactly once in the navigation menu");
        homeLinks.Single().TextContent.Should().Contain("Home");
    }

    [Fact]
    public void NavMenu_WhenUnauthenticated_HidesTransactionsAndCategories()
    {
        // Arrange
        AuthContext.SetNotAuthorized();

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        var transactionLinks = cut.FindAll("nav a")
            .Where(a => a.GetAttribute("href") is "transactions" or "/transactions" || a.TextContent.Contains("Transactions"))
            .ToList();
        var categoryLinks = cut.FindAll("nav a")
            .Where(a => a.GetAttribute("href") is "categories" or "/categories" || a.TextContent.Contains("Categories"))
            .ToList();

        transactionLinks.Should().BeEmpty("transactions link should not be rendered for anonymous users");
        categoryLinks.Should().BeEmpty("categories link should not be rendered for anonymous users");
    }

    [Fact]
    public void NavMenu_WhenAuthenticated_ShowsTransactionsAndCategories()
    {
        // Arrange
        AuthContext.SetAuthorized("testuser@example.com");

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        var transactionLinks = cut.FindAll("nav a")
            .Where(a => a.GetAttribute("href") is "transactions" or "/transactions")
            .ToList();
        var categoryLinks = cut.FindAll("nav a")
            .Where(a => a.GetAttribute("href") is "categories" or "/categories")
            .ToList();

        transactionLinks.Should().ContainSingle("transactions link should be rendered exactly once for authenticated users");
        transactionLinks.Single().GetAttribute("href").Should().Match(href => href == "transactions" || href == "/transactions");
        transactionLinks.Single().TextContent.Should().Contain("Transactions");

        categoryLinks.Should().ContainSingle("categories link should be rendered exactly once for authenticated users");
        categoryLinks.Single().GetAttribute("href").Should().Match(href => href == "categories" || href == "/categories");
        categoryLinks.Single().TextContent.Should().Contain("Categories");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NavMenu_DoesNotRenderObsoleteRoutes(bool isAuthenticated)
    {
        // Arrange
        if (isAuthenticated)
        {
            AuthContext.SetAuthorized("testuser@example.com");
        }
        else
        {
            AuthContext.SetNotAuthorized();
        }

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        var links = cut.FindAll("nav a").ToList();

        links.Should().NotContain(a => a.GetAttribute("href") == "counter" || a.GetAttribute("href") == "/counter",
            "obsolete /counter route should be removed from navigation");
        links.Should().NotContain(a => a.GetAttribute("href") == "weather" || a.GetAttribute("href") == "/weather",
            "obsolete /weather route should be removed from navigation");
        links.Should().NotContain(a => a.GetAttribute("href") == "auth" || a.GetAttribute("href") == "/auth",
            "obsolete /auth route should be removed from navigation");
    }
}
