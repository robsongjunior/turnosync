using Microsoft.EntityFrameworkCore;
using TurnoSync.Domain.Employees;
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
    public async Task Migrate_ShouldSeedEmployeesFromFiveCountries()
    {
        await using var context = _database.CreateContext();

        var countries = await context.Employees.OrderBy(c => c.Id).Select(c => c.CountryCode).ToListAsync();

        Assert.Equal(["ES", "BR", "AR", "MX", "PT"], countries);
    }

    [Fact]
    public async Task SaveChanges_ShouldFail_WhenEmailAlreadyUsedByActiveEmployee()
    {
        await using var context = _database.CreateContext();
        var repository = new EmployeeRepository(context);

        repository.Add(Employee.Create("Other Ana", "ana@turnosync.dev", "ES"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_ShouldHideEmployeeAndFreeItsEmail()
    {
        await using (var context = _database.CreateContext())
        {
            var repository = new EmployeeRepository(context);
            var ana = await repository.GetByIdAsync(1);
            ana!.Delete();
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new EmployeeRepository(context);

            Assert.Null(await repository.GetByIdAsync(1));
            Assert.DoesNotContain(await repository.ListAsync(), c => c.Id == 1);
            Assert.False(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: null));

            repository.Add(Employee.Create("New Ana", "ana@turnosync.dev", "ES"));
            await repository.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task EmailExists_ShouldIgnoreTheEmployeeBeingUpdated()
    {
        await using var context = _database.CreateContext();
        var repository = new EmployeeRepository(context);

        Assert.True(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: null));
        Assert.True(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: 2));
        Assert.False(await repository.EmailExistsAsync("ana@turnosync.dev", excludeId: 1));
    }

    [Fact]
    public async Task GetByEmails_ShouldReturnOnlyMatchingEmployees()
    {
        await using var context = _database.CreateContext();
        var repository = new EmployeeRepository(context);

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
            var shifts = await new ShiftRepository(context).ListWithEmployeeAsync();

            var shift = Assert.Single(shifts);
            Assert.Equal(Day, shift.Date);
            Assert.Equal(new TimeOnly(22, 0), shift.StartTime);
            Assert.Equal(new TimeOnly(6, 0), shift.EndTime);
            Assert.Equal(new DateTime(2026, 10, 13, 6, 0, 0), shift.EndsAt);
            Assert.Equal("Ana Torres", shift.Employee!.Name);
        }
    }

    [Fact]
    public async Task ListWithEmployee_ShouldFilterByPeriodInclusiveOnBothEnds_AndOrderByStartThenName()
    {
        await using var context = _database.CreateContext();
        var repository = new ShiftRepository(context);
        repository.AddRange(
        [
            Shift.Create(1, Day.AddDays(-1), new TimeOnly(8, 0), new TimeOnly(16, 0)), // fuera (antes)
            Shift.Create(1, Day, new TimeOnly(8, 0), new TimeOnly(16, 0)),               // límite inferior
            Shift.Create(3, Day, new TimeOnly(8, 0), new TimeOnly(16, 0)),               // mismo inicio, el nombre va después
            Shift.Create(2, Day.AddDays(2), new TimeOnly(8, 0), new TimeOnly(16, 0)),    // límite superior
            Shift.Create(1, Day.AddDays(3), new TimeOnly(8, 0), new TimeOnly(16, 0)),    // fuera (después)
        ]);
        await repository.SaveChangesAsync();

        var result = await repository.ListWithEmployeeAsync(from: Day, to: Day.AddDays(2));

        Assert.Equal(
            [("Ana Torres", Day), ("Lucía Fernández", Day), ("João Lima", Day.AddDays(2))],
            result.Select(s => (s.Employee!.Name, s.Date)));
    }

    [Fact]
    public async Task ListForOverlap_ShouldExcludeOtherEmployeesDeletedShiftsAndTheExcludedId()
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
    public async Task ListByEmployee_ShouldReturnTrackedActiveShiftsOfThatEmployee()
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

        var result = await repository.ListByEmployeeAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(EntityState.Unchanged, context.Entry(s).State));
    }
}
