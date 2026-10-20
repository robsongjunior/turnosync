using TurnoSync.Domain.Employees;

namespace TurnoSync.Tests.Domain;

public sealed class EmployeeTests
{
    [Fact]
    public void Create_ShouldNormalizeNameEmailAndCountry()
    {
        var employee = Employee.Create("  Ana Torres ", "  Ana.Torres@TurnoSync.DEV ", " es ");

        Assert.Equal("Ana Torres", employee.Name);
        Assert.Equal("ana.torres@turnosync.dev", employee.Email);
        Assert.Equal("ES", employee.CountryCode);
    }

    [Fact]
    public void Update_ShouldNormalizeAgain()
    {
        var employee = Employee.Create("Ana", "ana@turnosync.dev", "ES");

        employee.Update("Ana B.", "ANA.B@turnosync.dev", "br");

        Assert.Equal("Ana B.", employee.Name);
        Assert.Equal("ana.b@turnosync.dev", employee.Email);
        Assert.Equal("BR", employee.CountryCode);
    }

    [Theory]
    [InlineData("", "a@b.com", "ES")]
    [InlineData("Ana", " ", "ES")]
    [InlineData("Ana", "a@b.com", "")]
    public void Create_ShouldThrow_WhenAnyFieldIsBlank(string name, string email, string country) =>
        Assert.ThrowsAny<ArgumentException>(() => Employee.Create(name, email, country));

    [Fact]
    public void Delete_ShouldMarkAsDeleted()
    {
        var employee = Employee.Create("Ana", "ana@turnosync.dev", "ES");

        employee.Delete();

        Assert.True(employee.IsDeleted);
    }
}
