namespace Flippo.Core.Domain;

/// <summary>Port von ReviewResult.kt.</summary>
public enum ReviewResult
{
    Wrong,
    Hard,
    Good,
    Easy
}

public static class ReviewResultExtensions
{
    /// <summary>Kotlin: <c>fun isCorrect() = this != WRONG</c>.</summary>
    public static bool IsCorrect(this ReviewResult result) => result != ReviewResult.Wrong;
}
