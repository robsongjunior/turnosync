using Microsoft.EntityFrameworkCore;
using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Shifts;
using TurnoSync.Infra.Persistence.Repositories;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Infra;

public sealed class PersistenceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Migrate_ShouldSeedCollaboratorsFromFiveCountries()
    {
        await using var context = _database.CreateContext();

        var countries = await context.Collaborators.OrderBy(c => c.Id).Select(c => c.CountryCode).ToListAsync();

        Assert.Equal(["ES", "BR", "AR", "MX", "PT"], countries);
    }

    [Fact]
    public async Task SaveChanges_ShouldFail_WhenEmailAlreadyUsedByActiveCollaborator()
    {
        await using var context = _database.CreateContext();
        var repository = new CollaboratorRepository(context);

        repository.Add(Collaborator.Create("Other Ana", "ana@turnosync.dev", "ES"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_ShouldHideCollaboratorAndFreeItsEmail()
    {
        await using (var context = _database.CreateContext())
        {
            var repository = new CollaboratorRepository(context);
            var ana = await repository.GetByIdAsync(1);
            ana!.Delete();
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new CollaboratorRepository(context);

            Assert.Null(await repository.GetByIdAsync(1));
            Assert.DoesNotContain(await repository.ListAsync(), c => c.Id == 1);
            Assert.False(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: null));

            repository.Add(Collaborator.Create("New Ana", "ana@turnosync.dev", "ES"));
            await repository.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task EmailExists_ShouldIgnoreTheCollaboratorBeingUpdated()
    {
        await using var context = _database.CreateContext();
        var repository = new CollaboratorRepository(context);

        Assert.True(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: null));
        Assert.True(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: 2));
        Assert.False(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: 1));
    }

    [Fact]
    public async Task GetByEmails_ShouldReturnOnlyMatchingCollaborators()
    {
        await using var context = _database.CreateContext();
        var repository = new CollaboratorRepository(context);

        var found = await repository.GetByEmailsAsync(["ana@turnosync.dev", "ines@turnosync.dev", "nobody@x.com"]);

        Assert.Equal(["ana@turnosync.dev", "ines@turnosync.dev"], found.Select(c => c.Email).Order());
    }

    [Fact]
    public async Task Shift_ShouldRoundTripDateAndTimes_WhenNightShift()
    {
        await using (var context = _database.CreateContext())
        {
            var repository = new ShiftRepository(context);
            repository.Add(Shift.Create(1, Day, new TimeOnly(22, 0), new TimeOnly(6, 0)));
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var shifts = await new ShiftRepository(context).ListWithCollaboratorAsync();

            var shift = Assert.Single(shifts);
            Assert.Equal(Day, shift.Date);
            Assert.Equal(new TimeOnly(22, 0), shift.StartTime);
            Assert.Equal(new TimeOnly(6, 0), shift.EndTime);
            Assert.Equal(new DateTime(2026, 10, 13, 6, 0, 0), shift.EndsAt);
            Assert.Equal("Ana Torres", shift.Collaborator!.Name);
        }
    }

    [Fact]
    public async Task ListByPeriod_ShouldBeInclusiveOnBothEnds_AndOrderedByStartThenName()
    {
        await using var context = _database.CreateContext();
        var repository = new ShiftRepository(context);
        repository.AddRange(
        [
            Shift.Create(1, Day.AddDays(-1), new TimeOnly(8, 0), new TimeOnly(16, 0)), // outside (before)
            Shift.Create(1, Day, new TimeOnly(8, 0), new TimeOnly(16, 0)),               // lower bound
            Shift.Create(3, Day, new TimeOnly(8, 0), new TimeOnly(16, 0)),               // same start, name comes later
            Shift.Create(2, Day.AddDays(2), new TimeOnly(8, 0), new TimeOnly(16, 0)),    // upper bound
            Shift.Create(1, Day.AddDays(3), new TimeOnly(8, 0), new TimeOnly(16, 0)),    // outside (after)
        ]);
        await repository.SaveChangesAsync();

        var result = await repository.ListByPeriodAsync(Day, Day.AddDays(2));

        Assert.Equal(
            [("Ana Torres", Day), ("Lucía Fernández", Day), ("João Lima", Day.AddDays(2))],
            result.Select(s => (s.Collaborator!.Name, s.Date)));
    }

    [Fact]
    public async Task ListForOverlap_ShouldExcludeOtherCollaboratorsDeletedShiftsAndTheExcludedId()
    {
        await using var context = _database.CreateContext();
        var repository = new ShiftRepository(context);
        var kept = Shift.Create(1, Day, new TimeOnly(8, 0), new TimeOnly(16, 0));
        var deleted = Shift.Create(1, Day, new TimeOnly(18, 0), new TimeOnly(20, 0));
        var excluded = Shift.Create(1, Day, new TimeOnly(21, 0), new TimeOnly(23, 0));
        deleted.Delete();
        repository.AddRange([kept, deleted, excluded, Shift.Create(2, Day, new TimeOnly(8, 0), new TimeOnly(16, 0))]);
        await repository.SaveChangesAsync();

        var result = await repository.ListForOverlapAsync(1, Day.AddDays(-1), Day.AddDays(1), excluded.Id);

        Assert.Equal(kept.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task ListByCollaborator_ShouldReturnTrackedActiveShiftsOfThatCollaborator()
    {
        await using var context = _database.CreateContext();
        var repository = new ShiftRepository(context);
        repository.AddRange(
        [
            Shift.Create(1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            Shift.Create(1, Day, new TimeOnly(13, 0), new TimeOnly(17, 0)),
            Shift.Create(2, Day, new TimeOnly(8, 0), new TimeOnly(12, 0)),
        ]);
        await repository.SaveChangesAsync();

        var result = await repository.ListByCollaboratorAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(EntityState.Unchanged, context.Entry(s).State));
    }
}
