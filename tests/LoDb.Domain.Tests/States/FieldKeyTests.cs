using LoDb.Domain.States.Fields;

namespace LoDb.Domain.Tests.States;

/// <summary>
/// The dotted keys the index stores: each template writes and reads back its own, a subject
/// of one word, and nothing else parses.
/// </summary>
public sealed class FieldKeyTests
{
    public static TheoryData<ChangeField> Fields => [.. Enum.GetValues<ChangeField>()];

    [Theory]
    [InlineData("name", ChangeField.Name, null)]
    [InlineData("passive.description", ChangeField.PassiveDescription, null)]
    [InlineData("gold.purchasable", ChangeField.Purchasable, null)]
    [InlineData("stat.attack_damage", ChangeField.Stat, "attack_damage")]
    [InlineData("statPercent.move_speed", ChangeField.StatPercent, "move_speed")]
    [InlineData("spell.q.cooldown", ChangeField.SpellCooldown, "q")]
    [InlineData("rune.8005.longDesc", ChangeField.RuneLongDesc, "8005")]
    [InlineData("slot.keystone", ChangeField.RuneSlot, "keystone")]
    [InlineData("skin.103001.name", ChangeField.SkinName, "103001")]
    public void DottedKeyNamesItsTemplateAndSubject(
        string dotted,
        ChangeField field,
        string? subject)
    {
        var key = FieldKey.Parse(dotted);

        Assert.Equal((field, subject), (key.Field, key.Subject));
        Assert.Equal(dotted, key.ToDotted());
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public void EveryTemplateReadsBackItsOwnKey(ChangeField field)
    {
        var key = ChangeFields.Of(field).Subject is null
            ? FieldKey.Of(field)
            : FieldKey.Of(field, "x_1");

        Assert.Equal(key, FieldKey.Parse(key.ToDotted()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("stat")]
    [InlineData("stat.*")]
    [InlineData("stat.hp.base")]
    [InlineData("spell.q")]
    [InlineData("spell.q-1.name")]
    [InlineData("name.q")]
    [InlineData("Name")]
    public void AnythingElseDoesNotParse(string dotted)
    {
        Assert.False(FieldKey.TryParse(dotted, out _));
    }

    [Fact]
    public void SubjectMatchesItsTemplate()
    {
        Assert.Throws<ArgumentException>(() => FieldKey.Of(ChangeField.Stat));
        Assert.Throws<ArgumentException>(() => FieldKey.Of(ChangeField.Name, "q"));
        Assert.Throws<ArgumentException>(() => FieldKey.Of(ChangeField.Stat, "a.b"));
    }
}
