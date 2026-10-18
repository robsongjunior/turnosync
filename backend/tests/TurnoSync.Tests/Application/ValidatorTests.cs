using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Tests.Application;

public sealed class ValidatorTests
{
    private readonly CollaboratorRequestValidator _collaboratorValidator = new();
    private readonly ShiftRequestValidator _shiftValidator = new();

    private static ShiftRequest ValidShift() => new(1, new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0));

    private static string[] FailedProperties(FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(e => e.PropertyName).Distinct().Order().ToArray();

    [Fact]
    public void CollaboratorValidator_ShouldPass_WhenRequestIsValid()
    {
        var result = _collaboratorValidator.Validate(new CollaboratorRequest("Ana Torres", "ana@turnosync.dev", "ES"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("Pt")]
    public void CollaboratorValidator_ShouldPass_WhenCountryCodeIsLowercaseOrMixed(string country)
    {
        var result = _collaboratorValidator.Validate(new CollaboratorRequest("Ana", "ana@turnosync.dev", country));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CollaboratorValidator_ShouldFailEveryField_WhenRequestIsEmpty()
    {
        var result = _collaboratorValidator.Validate(new CollaboratorRequest(null, null, null));

        Assert.Equal(["CountryCode", "Email", "Name"], FailedProperties(result));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    [InlineData("@turnosync.dev")]
    public void CollaboratorValidator_ShouldFail_WhenEmailIsInvalid(string email)
    {
        var result = _collaboratorValidator.Validate(new CollaboratorRequest("Ana", email, "ES"));

        Assert.Equal(["Email"], FailedProperties(result));
    }

    [Theory]
    [InlineData("E")]      // curto demais
    [InlineData("ESP")]    // alpha-3, não alpha-2
    [InlineData("12")]     // não é letra
    [InlineData("ZZ")]     // 2 letras, mas não é país
    public void CollaboratorValidator_ShouldFail_WhenCountryCodeIsNotAnIsoAlpha2Country(string country)
    {
        var result = _collaboratorValidator.Validate(new CollaboratorRequest("Ana", "ana@turnosync.dev", country));

        Assert.Equal(["CountryCode"], FailedProperties(result));
    }

    [Fact]
    public void CollaboratorValidator_ShouldFail_WhenNameIsTooLong()
    {
        var result = _collaboratorValidator.Validate(
            new CollaboratorRequest(new string('a', 201), "ana@turnosync.dev", "ES"));

        Assert.Equal(["Name"], FailedProperties(result));
    }

    [Fact]
    public void ShiftValidator_ShouldPass_WhenRequestIsValid() =>
        Assert.True(_shiftValidator.Validate(ValidShift()).IsValid);

    [Fact]
    public void ShiftValidator_ShouldPass_WhenShiftStartsAtMidnight() =>
        Assert.True(_shiftValidator.Validate(ValidShift() with { StartTime = TimeOnly.MinValue }).IsValid);

    [Fact]
    public void ShiftValidator_ShouldPass_WhenShiftCrossesMidnight() =>
        Assert.True(_shiftValidator.Validate(
            ValidShift() with { StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0) }).IsValid);

    [Fact]
    public void ShiftValidator_ShouldFailEveryField_WhenRequestIsEmpty()
    {
        var result = _shiftValidator.Validate(new ShiftRequest(null, null, null, null));

        Assert.Equal(["CollaboratorId", "Date", "EndTime", "StartTime"], FailedProperties(result));
    }

    [Fact]
    public void ShiftValidator_ShouldFailOnEndTime_WhenStartEqualsEnd()
    {
        var result = _shiftValidator.Validate(ValidShift() with { EndTime = new TimeOnly(8, 0) });

        Assert.Equal(["EndTime"], FailedProperties(result));
    }

    [Fact]
    public void ShiftValidator_ShouldReportOnlyMissingEnd_WhenStartIsPresentAndEndIsNot()
    {
        var result = _shiftValidator.Validate(ValidShift() with { EndTime = null });

        Assert.Equal(["EndTime"], FailedProperties(result));
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ShiftValidator_ShouldFail_WhenCollaboratorIdIsNotPositive(int id)
    {
        var result = _shiftValidator.Validate(ValidShift() with { CollaboratorId = id });

        Assert.Equal(["CollaboratorId"], FailedProperties(result));
    }
}
