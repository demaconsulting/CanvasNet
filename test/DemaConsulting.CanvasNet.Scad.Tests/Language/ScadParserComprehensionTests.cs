using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> list comprehensions.
/// </summary>
public class ScadParserComprehensionTests
{
    /// <summary>
    ///     Validates a simple <c>for</c> comprehension.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_For_ProducesListFor()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec (lfor ((i (range 0 3))) (* i i)))", ScadParserTestSupport.Expr("[for (i=[0:3]) i*i]"));
    }

    /// <summary>
    ///     Validates a comprehension with several iterators.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_MultipleIterators_ProducesAllIterators()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec (lfor ((i a) (j b)) (vec i j)))", ScadParserTestSupport.Expr("[for (i=a, j=b) [i,j]]"));
    }

    /// <summary>
    ///     Validates the C-style <c>for</c> comprehension, including empty initializer and increment lists.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_CStyleFor_ProducesListCFor()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(vec (lcfor ((i 0)) (< i 3) ((i (+ i 1))) i))",
            ScadParserTestSupport.Expr("[for (i=0; i<3; i=i+1) i]"));
        Assert.Equal("(vec (lcfor () a () 1))", ScadParserTestSupport.Expr("[for (; a; ) 1]"));
        Assert.Equal(
            "(vec (lcfor ((a 0) (b 1)) (< a 3) ((a (+ a 1)) (b (* b 2))) a))",
            ScadParserTestSupport.Expr("[for (a=0, b=1; a<3; a=a+1, b=b*2) a]"));
    }

    /// <summary>
    ///     Validates <c>if</c> with and without <c>else</c>, including the dangling-else rule.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_IfElse_ProducesListIf()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(vec (lfor ((x v)) (lif (> x 1) x 0)))",
            ScadParserTestSupport.Expr("[for (x=v) if (x>1) x else 0]"));
        Assert.Equal("(vec (lfor ((x v)) (lif (> x 1) x)))", ScadParserTestSupport.Expr("[for (x=v) if (x>1) x]"));
        Assert.Equal(
            "(vec (lfor ((x v)) (lif a (lif b 1 2))))",
            ScadParserTestSupport.Expr("[for (x=v) if (a) if (b) 1 else 2]"));
    }

    /// <summary>
    ///     Validates <c>let</c> in element position.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_Let_ProducesListLet()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec (llet ((a 1)) a) 2)", ScadParserTestSupport.Expr("[let(a=1) a, 2]"));
        Assert.Equal("(vec (llet ((a 1)) (each (vec a 2))))", ScadParserTestSupport.Expr("[let(a=1) each [a,2]]"));
    }

    /// <summary>
    ///     Validates <c>each</c>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_Each_ProducesListEach()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec (each (vec 1 2)) 3)", ScadParserTestSupport.Expr("[each [1,2], 3]"));
    }

    /// <summary>
    ///     Validates nested comprehensions, directly nested and through a nested vector.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_Nested_ProducesNestedElements()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(vec (lfor ((i a)) (lfor ((j b)) (vec i j))))",
            ScadParserTestSupport.Expr("[for (i=a) for (j=b) [i,j]]"));
        Assert.Equal(
            "(vec (lfor ((i a)) (vec (lfor ((j b)) j))))",
            ScadParserTestSupport.Expr("[for (i=a) [for (j=b) j]]"));
    }

    /// <summary>
    ///     Validates that comprehension elements mix freely with plain elements.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_MixedWithPlainElements_KeepsSourceOrder()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(vec 1 2 (lfor ((i (range 3 4))) i) 5)",
            ScadParserTestSupport.Expr("[1, 2, for (i=[3:4]) i, 5]"));
        Assert.Equal("(vec (lfor ((i v)) (?: (> i 0) 1 2)))", ScadParserTestSupport.Expr("[for (i=v) i>0 ? 1 : 2]"));
    }

    /// <summary>
    ///     Validates that a C-style comprehension header requires a condition.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_CStyleWithoutCondition_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [for (i=0;;i=i+1) i];", 1, 15, "Expected an expression but found ';'");
    }

    /// <summary>
    ///     Validates that a comprehension keyword outside a vector is not an expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_OutsideVector_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = for (i=a) i;", 1, 5, "Expected an expression but found 'for'");
        ScadParserTestSupport.AssertParseError("x = each a;", 1, 5, "Expected an expression but found 'each'");
    }

    /// <summary>
    ///     Validates that a comprehension header with no iterator is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Comprehension_EmptyHeader_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [for () 1];", 1, 11, "Expected an identifier but found ')'");
    }
}
