using Flippo.Core.Domain;
using Flippo.Core.Session;

namespace Flippo.Tests.Session;

/// <summary>
/// Port der reinen Session-Zusammenstellungs-Logik aus StartLearningSessionUseCase.kt
/// (sortDueFirstNewShuffled + applyLimits) plus der filterMode-Selektion, die am Desktop
/// in-memory statt per DB-Query läuft. Random + nowMs injiziert = deterministisch.
/// </summary>
public class SessionComposerTests
{
    private const long Now = 1_700_000_000_000L;

    private static VocabularyEntry Card(
        long id, long nextReviewAt = 0L, int correct = 1, int wrong = 0,
        bool leech = false, bool archived = false, int box = 1)
        => new()
        {
            Id = id,
            NextReviewAt = nextReviewAt,
            CorrectCount = correct,
            WrongCount = wrong,
            IsLeech = leech,
            IsArchived = archived,
            BoxLevel = box
        };

    private static readonly SrsSettings Unlimited =
        new() { MaxCardsPerSession = 0, MaxNewCardsPerDay = 0 };

    private static SessionPlan Compose(
        IReadOnlyList<VocabularyEntry> cards, SessionComposeOptions options,
        SrsSettings? settings = null, int seed = 0, long nowMs = Now)
        => SessionComposer.Compose(cards, options, settings ?? Unlimited, new Random(seed), nowMs);

    [Fact] // reviewed cards ordered by nextReviewAt ascending
    public void Compose_OrdersReviewedCardsByNextReviewAtAscending()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: 300),
            Card(2, nextReviewAt: 100),
            Card(3, nextReviewAt: 200)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All });

        Assert.Equal(new long[] { 2, 3, 1 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // maxCardsPerSession takes the first N after ordering
    public void Compose_LimitsToMaxCardsPerSession()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: 300),
            Card(2, nextReviewAt: 100),
            Card(3, nextReviewAt: 200)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All },
            settings: new SrsSettings { MaxCardsPerSession = 2, MaxNewCardsPerDay = 0 });

        Assert.Equal(new long[] { 2, 3 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // maxNewCardsPerDay caps only new cards, keeps every reviewed card
    public void Compose_LimitsNewCardsPerDay()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: 100),        // reviewed
            Card(2, correct: 0, wrong: 0),     // new
            Card(3, correct: 0, wrong: 0),     // new
            Card(4, correct: 0, wrong: 0)      // new
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All },
            settings: new SrsSettings { MaxCardsPerSession = 0, MaxNewCardsPerDay = 1 });

        Assert.Equal(2, plan.Cards.Count);
        Assert.Contains(plan.Cards, c => c.Id == 1);          // reviewed bleibt
        Assert.Equal(1, plan.Cards.Count(c => c.IsNew));      // genau 1 neue
    }

    [Fact] // boxLevel >= 1 keeps only cards of that box
    public void Compose_FiltersByBoxLevel()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: 100, box: 1),
            Card(2, nextReviewAt: 200, box: 2),
            Card(3, nextReviewAt: 300, box: 2),
            Card(4, nextReviewAt: 400, box: 3)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All, BoxLevel = 2 });

        Assert.Equal(new long[] { 2, 3 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // new cards are shuffled via the injected Random (not left in input order)
    public void Compose_ShufflesNewCards()
    {
        var cards = Enumerable.Range(1, 8).Select(i => Card(i, correct: 0, wrong: 0)).ToList();
        var opts = new SessionComposeOptions { Filter = SessionFilter.All };

        var distinctOrderings = Enumerable.Range(0, 12)
            .Select(seed => string.Join(",", Compose(cards, opts, seed: seed).Cards.Select(c => c.Id)))
            .Distinct()
            .Count();

        Assert.True(distinctOrderings > 1,
            "Neue Karten sollten je nach Random-Seed unterschiedlich angeordnet werden.");
    }

    [Fact] // Due filter keeps only cards due at nowMs
    public void Compose_DueFilter_KeepsOnlyDueCards()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: Now - 1000),   // fällig
            Card(2, nextReviewAt: Now + 1000),   // nicht fällig
            Card(3, nextReviewAt: Now)           // fällig (Grenze <=)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.Due });

        Assert.Equal(new long[] { 1, 3 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // New filter keeps only never-answered cards
    public void Compose_NewFilter_KeepsOnlyNewCards()
    {
        var cards = new[]
        {
            Card(1, correct: 1, wrong: 0),   // reviewed
            Card(2, correct: 0, wrong: 0),   // new
            Card(3, correct: 0, wrong: 2)    // reviewed (wrong > 0)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.New });

        Assert.Equal(new long[] { 2 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // Leech filter keeps only leech cards
    public void Compose_LeechFilter_KeepsOnlyLeeches()
    {
        var cards = new[]
        {
            Card(1, leech: true, nextReviewAt: 100),
            Card(2, leech: false, nextReviewAt: 200),
            Card(3, leech: true, nextReviewAt: 300)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.Leech });

        Assert.Equal(new long[] { 1, 3 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // archived cards are excluded regardless of filter
    public void Compose_AlwaysExcludesArchivedCards()
    {
        var cards = new[]
        {
            Card(1, nextReviewAt: 100),
            Card(2, nextReviewAt: 200, archived: true)
        };

        var plan = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All });

        Assert.Equal(new long[] { 1 }, plan.Cards.Select(c => c.Id));
    }

    [Fact] // reviewed cards precede new cards (sortDueFirstNewShuffled)
    public void Compose_PlacesNewCardsAfterReviewed()
    {
        var cards = new[]
        {
            Card(1, correct: 0, wrong: 0),   // new
            Card(2, nextReviewAt: 100),      // reviewed
            Card(3, correct: 0, wrong: 0),   // new
            Card(4, nextReviewAt: 50)        // reviewed
        };

        var ids = Compose(cards, new SessionComposeOptions { Filter = SessionFilter.All })
            .Cards.Select(c => c.Id).ToList();

        Assert.Equal(new long[] { 4, 2 }, ids.Take(2));                  // reviewed zuerst, nach NextReviewAt
        Assert.Equal(new long[] { 1, 3 }, ids.Skip(2).Order());          // neue dahinter (Reihenfolge egal)
    }
}
