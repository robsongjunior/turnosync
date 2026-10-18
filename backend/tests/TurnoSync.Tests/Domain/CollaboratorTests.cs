using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Tests.Domain;

public sealed class CollaboratorTests
{
    [Fact]
    public void Create_ShouldNormalizeNameEmailAndCountry()
    {
        var collaborator = Collaborator.Create("  Ana Torres ", "  Ana.Torres@TurnoSync.DEV ", " es ");

        Assert.Equal("Ana Torres", collaborator.Name);
        Assert.Equal("ana.torres@turnosync.dev", collaborator.Email);
        Assert.Equal("ES", collaborator.CountryCode);
    }

    [Fact]
    public void Update_ShouldNormalizeAgain()
    {
        var collaborator = Collaborator.Create("Ana", "ana@turnosync.dev", "ES");

        collaborator.Update("Ana B.", "ANA.B@turnosync.dev", "br");

        Assert.Equal("Ana B.", collaborator.Name);
        Assert.Equal("ana.b@turnosync.dev", collaborator.Email);
        Assert.Equal("BR", collaborator.CountryCode);
    }

    [Theory]
    [InlineData("", "a@b.com", "ES")]
    [InlineData("Ana", " ", "ES")]
    [InlineData("Ana", "a@b.com", "")]
    public void Create_ShouldThrow_WhenAnyFieldIsBlank(string name, string email, string country) =>
        Assert.ThrowsAny<ArgumentException>(() => Collaborator.Create(name, email, country));

    [Fact]
    public void Delete_ShouldMarkAsDeleted()
    {
        var collaborator = Collaborator.Create("Ana", "ana@turnosync.dev", "ES");

        collaborator.Delete();

        Assert.True(collaborator.IsDeleted);
    }
}
