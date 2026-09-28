using System.Diagnostics.CodeAnalysis;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>The outcome of a submission's check: what to save, or why nothing is.</summary>
internal sealed record SubmissionCheck
{
    private SubmissionCheck(BuildSubmission? submission, SubmissionRefusal? refusal)
    {
        Submission = submission;
        Refusal = refusal;
    }

    public BuildSubmission? Submission { get; }

    public SubmissionRefusal? Refusal { get; }

    [MemberNotNullWhen(true, nameof(Submission))]
    [MemberNotNullWhen(false, nameof(Refusal))]
    public bool IsAccepted => Submission is not null;

    public static SubmissionCheck Accept(BuildSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return new SubmissionCheck(submission, null);
    }

    public static SubmissionCheck Refuse(IResult problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new SubmissionCheck(null, new SubmissionRefusal(problem));
    }
}
