using Expenses.Common;
using FluentAssertions;

namespace Expenses.Tests.Common;

public sealed class ResultTests
{
    private sealed record TestPayload(int Id, string Name);

    #region Non-generic Result Tests

    [Fact]
    public void Success_WhenCalled_ReturnsSuccessResultWithNoErrors()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_WhenCalledWithErrorMessageOnly_SetsFailureStateAndErrorMessage()
    {
        // Arrange
        const string errorMessage = "An unexpected error occurred.";

        // Act
        var result = Result.Failure(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_WhenCalledWithErrorMessageAndErrorCode_SetsAllErrorDetails()
    {
        // Arrange
        const string errorMessage = "Resource was not found.";
        const string errorCode = "NOT_FOUND";

        // Act
        var result = Result.Failure(errorMessage, errorCode);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void Failure_WhenErrorCodeIsNull_PreservesNullErrorCode()
    {
        // Arrange
        const string errorMessage = "Validation failed.";

        // Act
        var result = Result.Failure(errorMessage, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Result_Equality_WhenIdenticalState_EvaluatesAsEqual()
    {
        // Arrange
        var result1 = Result.Failure("Failed", "ERR_01");
        var result2 = Result.Failure("Failed", "ERR_01");

        // Act & Assert
        result1.Should().Be(result2);
        (result1 == result2).Should().BeTrue();
    }

    [Fact]
    public void Result_Equality_WhenDifferentState_EvaluatesAsNotEqual()
    {
        // Arrange
        var result1 = Result.Failure("Failed", "ERR_01");
        var result2 = Result.Failure("Failed", "ERR_02");

        // Act & Assert
        result1.Should().NotBe(result2);
        (result1 == result2).Should().BeFalse();
    }

    #endregion

    #region Generic Result<T> Tests

    [Fact]
    public void GenericSuccess_WithValueType_ReturnsSuccessAndValue()
    {
        // Arrange
        const decimal value = 129.95m;

        // Act
        var result = Result<decimal>.Success(value);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericSuccess_WithReferenceType_ReturnsSuccessAndValue()
    {
        // Arrange
        const string message = "Transaction imported successfully";

        // Act
        var result = Result<string>.Success(message);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(message);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericSuccess_WithNullableReferenceTypeAndNullValue_AllowsNullValueWhenSuccessful()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericSuccess_WithComplexType_PreservesTypeSafetyAndIntegrity()
    {
        // Arrange
        var payload = new TestPayload(42, "Grocery Budget");

        // Act
        var result = Result<TestPayload>.Success(payload);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(payload);
        result.Value!.Id.Should().Be(42);
        result.Value.Name.Should().Be("Grocery Budget");
    }

    [Fact]
    public void GenericFailure_WhenCalledWithErrorMessageOnly_SetsFailureStateAndDefaultValue()
    {
        // Arrange
        const string errorMessage = "Category not found.";

        // Act
        var result = Result<int>.Failure(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default(int));
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericFailure_WhenCalledWithErrorMessageAndErrorCode_SetsAllFailureProperties()
    {
        // Arrange
        const string errorMessage = "Invalid hierarchy nesting.";
        const string errorCode = "MAX_DEPTH_EXCEEDED";

        // Act
        var result = Result<TestPayload>.Failure(errorMessage, errorCode);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void GenericFailure_WhenErrorCodeIsNull_PreservesNullErrorCode()
    {
        // Arrange
        const string errorMessage = "Unauthorized operation.";

        // Act
        var result = Result<string>.Failure(errorMessage, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericResult_Equality_WhenIdenticalState_EvaluatesAsEqual()
    {
        // Arrange
        var payload = new TestPayload(1, "Utilities");
        var result1 = Result<TestPayload>.Success(payload);
        var result2 = Result<TestPayload>.Success(payload);

        // Act & Assert
        result1.Should().Be(result2);
        (result1 == result2).Should().BeTrue();
    }

    [Fact]
    public void GenericResult_Equality_WhenDifferentValues_EvaluatesAsNotEqual()
    {
        // Arrange
        var result1 = Result<int>.Success(100);
        var result2 = Result<int>.Success(200);

        // Act & Assert
        result1.Should().NotBe(result2);
        (result1 == result2).Should().BeFalse();
    }

    #endregion
}
