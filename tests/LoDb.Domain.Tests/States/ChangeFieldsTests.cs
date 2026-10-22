using LoDb.Domain.States.Fields;

namespace LoDb.Domain.Tests.States;

/// <summary>
/// The closed set of field templates: one template per member, in the member's order, with
/// a pattern of its own whose subject segment matches its subject.
/// </summary>
public sealed class ChangeFieldsTests
{
    [Fact]
    public void EveryFieldHasOneTemplateInItsOrder()
    {
        Assert.Equal(
            Enum.GetValues<ChangeField>(),
            ChangeFields.All.Select(static template => template.Field));
    }

    [Fact]
    public void PatternsAreDistinctAndTakeASubjectWhenTheTemplateDoes()
    {
        Assert.Equal(
            ChangeFields.All.Count,
            ChangeFields.All.Select(static template => template.Pattern).Distinct().Count());
        Assert.All(ChangeFields.All, static template => Assert.Equal(
            template.Subject is not null,
            template.Pattern.Split('.').Contains(FieldTemplate.SubjectSegment)));
    }
}
