namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>seed 的結果。有新增回 <see cref="Seeded"/>，全部略過才回 <see cref="AlreadySeeded"/>。</summary>
public enum SeedOutcome
{
    Seeded,
    AlreadySeeded
}

public sealed record SeedResult(SeedOutcome Outcome, int Events, int Performances, int Sections, int Seats, int Users)
{
    public override string ToString()
        => $"{Outcome}: events={Events}, performances={Performances}, sections={Sections}, seats={Seats}, users={Users}";
}
