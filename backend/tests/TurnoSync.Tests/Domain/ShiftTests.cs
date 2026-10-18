using TurnoSync.Domain.Shifts;

namespace TurnoSync.Tests.Domain;

public sealed class ShiftTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private static Shift ShiftOf(string start, string end, int collaboratorId = 1, DateOnly? date = null) =>
        Shift.Create(collaboratorId, date ?? Day, TimeOnly.Parse(start), TimeOnly.Parse(end));

    [Fact]
    public void Create_ShouldEndOnSameDay_WhenEndIsAfterStart()
    {
        var shift = ShiftOf("08:00", "16:00");

        Assert.Equal(new DateTime(2026, 10, 12, 8, 0, 0), shift.StartsAt);
        Assert.Equal(new DateTime(2026, 10, 12, 16, 0, 0), shift.EndsAt);
    }

    [Fact]
    public void Create_ShouldEndOnNextDay_WhenShiftCrossesMidnight()
    {
        var shift = ShiftOf("22:00", "06:00");

        Assert.Equal(new DateTime(2026, 10, 12, 22, 0, 0), shift.StartsAt);
        Assert.Equal(new DateTime(2026, 10, 13, 6, 0, 0), shift.EndsAt);
    }

    [Fact]
    public void Create_ShouldCrossMonthBoundary_WhenNightShiftStartsOnLastDayOfMonth()
    {
        var shift = ShiftOf("23:00", "07:00", date: new DateOnly(2026, 10, 31));

        Assert.Equal(new DateTime(2026, 11, 1, 7, 0, 0), shift.EndsAt);
    }

    [Fact]
    public void Create_ShouldThrow_WhenStartEqualsEnd() =>
        Assert.Throws<ArgumentException>(() => ShiftOf("08:00", "08:00"));

    [Fact]
    public void Update_ShouldRecalculateStartsAtAndEndsAt()
    {
        var shift = ShiftOf("08:00", "16:00");

        shift.Update(2, new DateOnly(2026, 10, 20), new TimeOnly(22, 0), new TimeOnly(6, 0));

        Assert.Equal(2, shift.CollaboratorId);
        Assert.Equal(new DateTime(2026, 10, 20, 22, 0, 0), shift.StartsAt);
        Assert.Equal(new DateTime(2026, 10, 21, 6, 0, 0), shift.EndsAt);
    }

    [Theory]
    [InlineData("08:00", "16:00", "12:00", "20:00")] // solapamiento parcial al final del existente
    [InlineData("08:00", "16:00", "06:00", "10:00")] // solapamiento parcial al inicio del existente
    [InlineData("08:00", "16:00", "10:00", "12:00")] // el nuevo está contenido en el existente
    [InlineData("08:00", "16:00", "06:00", "18:00")] // el nuevo contiene al existente
    [InlineData("08:00", "16:00", "08:00", "16:00")] // idéntico
    public void Overlaps_ShouldBeTrue_WhenRangesIntersect(string start, string end, string otherStart, string otherEnd)
    {
        var existing = ShiftOf(start, end);
        var candidate = ShiftOf(otherStart, otherEnd);

        Assert.True(candidate.Overlaps(existing));
        Assert.True(existing.Overlaps(candidate));
    }

    [Theory]
    [InlineData("08:00", "12:00", "12:00", "16:00")] // se tocan: el fin del primero = el inicio del segundo
    [InlineData("12:00", "16:00", "08:00", "12:00")] // se tocan, en orden inverso
    [InlineData("08:00", "10:00", "14:00", "18:00")] // separados el mismo día
    public void Overlaps_ShouldBeFalse_WhenRangesOnlyTouchOrAreApart(
        string start, string end, string otherStart, string otherEnd)
    {
        var existing = ShiftOf(start, end);
        var candidate = ShiftOf(otherStart, otherEnd);

        Assert.False(candidate.Overlaps(existing));
        Assert.False(existing.Overlaps(candidate));
    }

    [Fact]
    public void Overlaps_ShouldBeFalse_WhenSameTimeButDifferentCollaborators()
    {
        var first = ShiftOf("08:00", "16:00", collaboratorId: 1);
        var second = ShiftOf("08:00", "16:00", collaboratorId: 2);

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_ShouldBeTrue_WhenNightShiftReachesIntoNextDayShift()
    {
        var night = ShiftOf("22:00", "06:00");
        var nextMorning = ShiftOf("05:00", "09:00", date: Day.AddDays(1));

        Assert.True(night.Overlaps(nextMorning));
        Assert.True(nextMorning.Overlaps(night));
    }

    [Fact]
    public void Overlaps_ShouldBeFalse_WhenNightShiftEndsExactlyWhenNextDayShiftStarts()
    {
        var night = ShiftOf("22:00", "06:00");
        var nextMorning = ShiftOf("06:00", "10:00", date: Day.AddDays(1));

        Assert.False(night.Overlaps(nextMorning));
    }

    [Fact]
    public void Overlaps_ShouldBeFalse_WhenDayShiftEndsBeforeNightShiftOfSameDay()
    {
        var day = ShiftOf("08:00", "16:00");
        var night = ShiftOf("22:00", "06:00");

        Assert.False(day.Overlaps(night));
    }

    [Fact]
    public void Overlaps_ShouldBeTrue_WhenTwoNightShiftsOverlapAcrossDays()
    {
        var first = ShiftOf("22:00", "06:00");
        var second = ShiftOf("02:00", "10:00", date: Day.AddDays(1));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_ShouldThrow_WhenOtherIsNull() =>
        Assert.Throws<ArgumentNullException>(() => ShiftOf("08:00", "16:00").Overlaps(null!));
}
