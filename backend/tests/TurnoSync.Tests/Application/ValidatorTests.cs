using TurnoSync.Application.Employees;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Application;

public sealed class ValidatorTests
{
    private readonly EmployeeRequestValidator _employeeValidator = new();
    private readonly ShiftRequestValidator _shiftValidator = new(new FixedTimeProvider());

    private static ShiftRequest ValidShift() => new(1, new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0));

    private static string[] FailedProperties(FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(e => e.PropertyName).Distinct().Order().ToArray();

    [Fact]
    public void EmployeeValidator_ShouldPass_WhenRequestIsValid()
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana Torres", "ana@turnosync.dev", "ES"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("Pt")]
    public void EmployeeValidator_ShouldPass_WhenCountryCodeIsLowercaseOrMixed(string country)
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana", "ana@turnosync.dev", country));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmployeeValidator_ShouldFailEveryField_WhenRequestIsEmpty()
    {
        var result = _employeeValidator.Validate(new EmployeeRequest(null, null, null));

        Assert.Equal(["CountryCode", "Email", "Name"], FailedProperties(result));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    [InlineData("@turnosync.dev")]
    public void EmployeeValidator_ShouldFail_WhenEmailIsInvalid(string email)
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana", email, "ES"));

        Assert.Equal(["Email"], FailedProperties(result));
    }

    [Theory]
    [InlineData("E")]      // demasiado corto
    [InlineData("ESP")]    // alfa-3, no alfa-2
    [InlineData("12")]     // no son letras
    [InlineData("ZZ")]     // 2 letras, pero no es un país
    public void EmployeeValidator_ShouldFail_WhenCountryCodeIsNotAnIsoAlpha2Country(string country)
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana", "ana@turnosync.dev", country));

        Assert.Equal(["CountryCode"], FailedProperties(result));
    }

    [Fact]
    public void EmployeeValidator_ShouldFail_WhenNameIsTooLong()
    {
        var result = _employeeValidator.Validate(
            new EmployeeRequest(new string('a', 201), "ana@turnosync.dev", "ES"));

        Assert.Equal(["Name"], FailedProperties(result));
    }

    [Fact]
    public void EmployeeValidator_ShouldReportSpanishMessages_WhenRequiredFieldsAreMissing()
    {
        var result = _employeeValidator.Validate(new EmployeeRequest(null, null, null));

        Assert.Equal(
            ["El nombre es obligatorio.", "El email es obligatorio.", "El país es obligatorio."],
            result.Errors.Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData("not-an-email", "El email no es válido.")]
    [InlineData("@turnosync.dev", "El email no es válido.")]
    public void EmployeeValidator_ShouldReportSpanishMessage_WhenEmailIsInvalid(string email, string message)
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana", email, "ES"));

        Assert.Equal([message], result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void EmployeeValidator_ShouldReportSpanishMessagesWithTheLimit_WhenTextsAreTooLong()
    {
        var result = _employeeValidator.Validate(
            new EmployeeRequest(new string('a', 201), new string('a', 250) + "@x.com", "ES"));

        Assert.Equal(
            ["El nombre no puede superar los 200 caracteres.", "El email no puede superar los 254 caracteres."],
            result.Errors.Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData("ESP", "El código de país debe tener 2 letras.")]
    [InlineData("ZZ", "El código de país debe ser un código ISO 3166-1 alfa-2 válido (por ejemplo, ES).")]
    public void EmployeeValidator_ShouldReportSpanishMessage_WhenCountryCodeIsInvalid(string country, string message)
    {
        var result = _employeeValidator.Validate(new EmployeeRequest("Ana", "ana@turnosync.dev", country));

        Assert.Equal([message], result.Errors.Select(e => e.ErrorMessage));
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

    // El reloj de las pruebas marca el 1/10/2026 a las 08:00 en Madrid.
    [Theory]
    [InlineData(2026, 9, 30, 22, 0)] // día anterior
    [InlineData(2026, 10, 1, 7, 59)] // hoy, hora ya pasada
    public void ShiftValidator_ShouldFail_WhenShiftStartsInThePast(int year, int month, int day, int hour, int minute)
    {
        var result = _shiftValidator.Validate(new ShiftRequest(
            1, new DateOnly(year, month, day), new TimeOnly(hour, minute), new TimeOnly(hour, minute).AddHours(8)));

        Assert.Equal(["No se puede registrar un turno en el pasado."], result.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["Date"], FailedProperties(result));
    }

    [Theory]
    [InlineData(8, 0)] // empieza justo ahora
    [InlineData(9, 0)] // hoy, más tarde
    public void ShiftValidator_ShouldPass_WhenShiftStartsTodayFromNowOn(int hour, int minute) =>
        Assert.True(_shiftValidator.Validate(new ShiftRequest(
            1, new DateOnly(2026, 10, 1), new TimeOnly(hour, minute), new TimeOnly(hour, minute).AddHours(8))).IsValid);

    [Fact]
    public void ShiftValidator_ShouldCompareWithMadridTime_NotUtc()
    {
        // 06:30 UTC son las 08:30 en Madrid (horario de verano): un turno a las 08:00 ya es pasado.
        var validator = new ShiftRequestValidator(new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 6, 30, 0, TimeSpan.Zero)));

        var result = validator.Validate(ValidShift() with { Date = new DateOnly(2026, 10, 1), StartTime = new TimeOnly(8, 0) });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ShiftValidator_ShouldFailEveryField_WhenRequestIsEmpty()
    {
        var result = _shiftValidator.Validate(new ShiftRequest(null, null, null, null));

        Assert.Equal(["Date", "EmployeeId", "EndTime", "StartTime"], FailedProperties(result));
    }

    [Fact]
    public void ShiftValidator_ShouldReportSpanishMessages_WhenRequestIsEmpty()
    {
        var result = _shiftValidator.Validate(new ShiftRequest(null, null, null, null));

        Assert.Equal(
            [
                "El colaborador es obligatorio.",
                "La fecha es obligatoria.",
                "La hora de inicio es obligatoria.",
                "La hora de fin es obligatoria.",
            ],
            result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void ShiftValidator_ShouldFailOnEndTime_WhenStartEqualsEnd()
    {
        var result = _shiftValidator.Validate(ValidShift() with { EndTime = new TimeOnly(8, 0) });

        Assert.Equal(["EndTime"], FailedProperties(result));
        Assert.Equal(
            ["La hora de fin debe ser distinta de la hora de inicio."],
            result.Errors.Select(e => e.ErrorMessage));
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
    public void ShiftValidator_ShouldFail_WhenEmployeeIdIsNotPositive(int id)
    {
        var result = _shiftValidator.Validate(ValidShift() with { EmployeeId = id });

        Assert.Equal(["EmployeeId"], FailedProperties(result));
        Assert.Equal(
            ["El identificador del colaborador debe ser mayor que 0."],
            result.Errors.Select(e => e.ErrorMessage));
    }
}
