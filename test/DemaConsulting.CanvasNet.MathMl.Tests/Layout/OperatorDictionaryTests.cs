using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="OperatorDictionary"/>: the categories of MathML Core B.1 and their values, the special
///     tables, the form inference and the stretch axis, and the well-formedness of the generated tables.
/// </summary>
public class OperatorDictionaryTests
{
    /// <summary>
    ///     Validates that an infix plus is in category B: 4/18 em on each side, nothing else.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_PlusInfix_IsCategoryB()
    {
        // Act
        var plus = OperatorDictionary.Lookup("+", OperatorForm.Infix, formIsExplicit: false);

        // Assert
        Assert.Equal((OperatorCategory.B, false, false, false, false), (plus.Category, plus.Stretchy, plus.Symmetric, plus.LargeOp, plus.MovableLimits));
        Assert.Equal(4 / 18.0, plus.LeadingSpace, 12);
        Assert.Equal(4 / 18.0, plus.TrailingSpace, 12);
    }

    /// <summary>
    ///     Validates that an infix arrow is in category A (5/18 em, stretchy) and that an equals sign, which has no entry,
    ///     gets the same spacing from the Default category without stretching.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_EqualsInfix_IsCategoryA()
    {
        // Act
        var arrow = OperatorDictionary.Lookup("\u2192", OperatorForm.Infix, formIsExplicit: false);
        var equals = OperatorDictionary.Lookup("=", OperatorForm.Infix, formIsExplicit: false);

        // Assert
        Assert.Equal((OperatorCategory.A, true), (arrow.Category, arrow.Stretchy));
        Assert.Equal(5 / 18.0, arrow.LeadingSpace, 12);
        Assert.Equal((OperatorCategory.Default, false), (equals.Category, equals.Stretchy));
        Assert.Equal(5 / 18.0, equals.TrailingSpace, 12);
    }

    /// <summary>
    ///     Validates that a prefix left parenthesis is a stretchy, symmetric fence without spacing (category F), and the
    ///     postfix right parenthesis likewise (category G).
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_LeftParenPrefix_IsFenceStretchySymmetric()
    {
        // Act
        var open = OperatorDictionary.Lookup("(", OperatorForm.Prefix, formIsExplicit: false);
        var close = OperatorDictionary.Lookup(")", OperatorForm.Postfix, formIsExplicit: false);

        // Assert
        Assert.Equal(new OperatorProperties(OperatorCategory.F, 0, 0, true, true, false, false), open);
        Assert.Equal(new OperatorProperties(OperatorCategory.G, 0, 0, true, true, false, false), close);
        Assert.True(OperatorDictionary.IsFence("("));
        Assert.False(OperatorDictionary.IsFence("+"));
        Assert.True(OperatorDictionary.IsSeparator(","));
        Assert.False(OperatorDictionary.IsSeparator("("));
    }

    /// <summary>
    ///     Validates that a prefix summation is a symmetric large operator with movable limits (category J), also when its
    ///     form was inferred as infix (the fallback tries the other forms).
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_SumPrefix_IsLargeOpMovableLimits()
    {
        // Act
        var prefix = OperatorDictionary.Lookup("\u2211", OperatorForm.Prefix, formIsExplicit: false);
        var inferredInfix = OperatorDictionary.Lookup("\u2211", OperatorForm.Infix, formIsExplicit: false);
        var explicitInfix = OperatorDictionary.Lookup("\u2211", OperatorForm.Infix, formIsExplicit: true);

        // Assert
        Assert.Equal((OperatorCategory.J, true, true, true), (prefix.Category, prefix.Symmetric, prefix.LargeOp, prefix.MovableLimits));
        Assert.Equal(3 / 18.0, prefix.LeadingSpace, 12);
        Assert.Equal(OperatorCategory.J, inferredInfix.Category);
        Assert.Equal((OperatorCategory.Default, false), (explicitInfix.Category, explicitInfix.LargeOp));
    }

    /// <summary>
    ///     Validates that a prefix integral is a symmetric large operator without movable limits (category H).
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_IntegralPrefix_IsLargeOpNotMovable()
    {
        // Act
        var integral = OperatorDictionary.Lookup("\u222B", OperatorForm.Prefix, formIsExplicit: false);

        // Assert
        Assert.Equal((OperatorCategory.H, true, true, false, false), (integral.Category, integral.Symmetric, integral.LargeOp, integral.MovableLimits, integral.Stretchy));
    }

    /// <summary>
    ///     Validates that the infix vertical line and tilde operator are ForceDefault (no fallback to the stretchy prefix
    ///     and postfix fences), while the prefix and postfix vertical lines are fences.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_VerticalBarInfix_IsForceDefault()
    {
        // Act
        var infix = OperatorDictionary.Lookup("|", OperatorForm.Infix, formIsExplicit: false);

        // Assert
        Assert.Equal((OperatorCategory.ForceDefault, false), (infix.Category, infix.Stretchy));
        Assert.Equal(OperatorCategory.ForceDefault, OperatorDictionary.Category("\u223C", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.F, OperatorDictionary.Category("|", OperatorForm.Prefix));
        Assert.Equal(OperatorCategory.G, OperatorDictionary.Category("|", OperatorForm.Postfix));
    }

    /// <summary>
    ///     Validates that the two-character ASCII operators are looked up through Operators_2_ascii_chars (U+0320 plus
    ///     their index), and that one without an entry is Default.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_TwoAsciiChars_MapsThroughSpecialTable()
    {
        // Act / Assert
        Assert.Equal(OperatorCategory.B, OperatorDictionary.Category("&&", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.C, OperatorDictionary.Category("**", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.D, OperatorDictionary.Category("||", OperatorForm.Prefix));
        Assert.Equal(OperatorCategory.E, OperatorDictionary.Category("++", OperatorForm.Postfix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category("->", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category(":=", OperatorForm.Infix));
        Assert.True(OperatorDictionary.IsFence("||"));
    }

    /// <summary>
    ///     Validates that a combining long solidus or vertical line overlay is dropped before the lookup.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_NegatedOperator_StripsCombiningSolidus()
    {
        // Act / Assert
        Assert.Equal(OperatorCategory.B, OperatorDictionary.Category("+\u0338", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.A, OperatorDictionary.Category("\u2192\u20D2", OperatorForm.Infix));
    }

    /// <summary>
    ///     Validates that a character without an entry, a text of more than two characters, an empty text and the reserved
    ///     range are Default, and the Arabic operators of the supplementary planes are I in the postfix form only.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Lookup_UnlistedCharacter_IsDefault()
    {
        // Act / Assert
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category("x", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category("sin", OperatorForm.Prefix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category(string.Empty, OperatorForm.Infix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category("\u0322", OperatorForm.Infix));
        Assert.Equal(OperatorCategory.I, OperatorDictionary.Category("\U0001EEF0", OperatorForm.Postfix));
        Assert.Equal(OperatorCategory.Default, OperatorDictionary.Category("\U0001EEF1", OperatorForm.Infix));
        var unlisted = OperatorDictionary.Lookup("x", OperatorForm.Infix, formIsExplicit: false);
        Assert.Equal(5 / 18.0, unlisted.LeadingSpace, 12);
        Assert.False(unlisted.Stretchy || unlisted.Symmetric || unlisted.LargeOp || unlisted.MovableLimits);
    }

    /// <summary>
    ///     Validates the form inference: the first of several children is prefix, the last postfix, the others and a lone
    ///     child infix.
    /// </summary>
    [Fact]
    public void OperatorDictionary_InferForm_FirstMiddleLast_PrefixInfixPostfix()
    {
        // Act / Assert
        Assert.Equal(
            [OperatorForm.Prefix, OperatorForm.Infix, OperatorForm.Postfix],
            Enumerable.Range(0, 3).Select(position => OperatorDictionary.InferForm(position, 3)));
        Assert.Equal(OperatorForm.Infix, OperatorDictionary.InferForm(0, 1));
        Assert.Equal(OperatorForm.Infix, OperatorDictionary.InferForm(0, 0));
    }

    /// <summary>
    ///     Validates that an explicit form wins and is parsed ASCII case-insensitively, and that it disables the fallback to
    ///     the other forms.
    /// </summary>
    [Fact]
    public void OperatorDictionary_InferForm_ExplicitAttribute_Wins()
    {
        // Act
        var parsed = OperatorDictionary.TryParseForm(" POSTFIX ", out var form);
        var invalid = OperatorDictionary.TryParseForm("sideways", out _);
        var inferred = OperatorDictionary.Lookup("(", OperatorForm.Infix, formIsExplicit: false);
        var explicitForm = OperatorDictionary.Lookup("(", OperatorForm.Infix, formIsExplicit: true);

        // Assert
        Assert.True(parsed);
        Assert.Equal(OperatorForm.Postfix, form);
        Assert.False(invalid);
        Assert.Equal(OperatorCategory.F, inferred.Category);
        Assert.Equal(OperatorCategory.Default, explicitForm.Category);
    }

    /// <summary>
    ///     Validates the stretch axis: inline for a single character of the inline list, block otherwise.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Axis_InlineList_IsInline()
    {
        // Act / Assert
        Assert.Equal(StretchAxis.Inline, OperatorDictionary.Axis("\u2192"));
        Assert.Equal(StretchAxis.Inline, OperatorDictionary.Axis("="));
        Assert.Equal(StretchAxis.Inline, OperatorDictionary.Axis("\U0001EEF0"));
        Assert.Equal(StretchAxis.Block, OperatorDictionary.Axis("("));
        Assert.Equal(StretchAxis.Block, OperatorDictionary.Axis("\u2191"));
        Assert.Equal(StretchAxis.Block, OperatorDictionary.Axis("\u2192\u2192"));
        Assert.Equal(StretchAxis.Block, OperatorDictionary.Axis(string.Empty));
    }

    /// <summary>
    ///     Validates that the generated tables are well-formed: sorted, disjoint ranges per form, an ascending inline list,
    ///     the 18 two-character operators, and a value for every category.
    /// </summary>
    [Fact]
    public void OperatorDictionary_Data_RangesAreSortedAndDisjoint()
    {
        // Act / Assert
        Assert.True(OperatorDictionary.TablesAreWellFormed());
        Assert.All(Enum.GetValues<OperatorCategory>(), category => Assert.Equal(category, OperatorDictionary.Properties(category).Category));
        Assert.Throws<ArgumentOutOfRangeException>(() => OperatorDictionary.Properties((OperatorCategory)99));
    }
}
