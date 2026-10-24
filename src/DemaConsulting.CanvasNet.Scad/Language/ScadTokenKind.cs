namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     The kinds of token produced by <see cref="ScadLexer"/>.
/// </summary>
/// <remarks>
///     Keywords, operators and punctuation each have a dedicated kind so that the parser can dispatch on
///     <see cref="ScadToken.Kind"/> alone; the characters <c>*</c>, <c>!</c>, <c>#</c> and <c>%</c> double as
///     statement modifiers, which the parser (not the lexer) decides from context.
/// </remarks>
internal enum ScadTokenKind
{
    /// <summary>The end of the input; always the last token.</summary>
    EndOfFile,

    /// <summary>A numeric literal.</summary>
    Number,

    /// <summary>A string literal, with escapes decoded.</summary>
    String,

    /// <summary>An identifier, including special variables that start with <c>$</c>.</summary>
    Identifier,

    /// <summary>An <c>include &lt;path&gt;</c> directive.</summary>
    Include,

    /// <summary>A <c>use &lt;path&gt;</c> directive.</summary>
    Use,

    /// <summary>The <c>module</c> keyword.</summary>
    Module,

    /// <summary>The <c>function</c> keyword.</summary>
    Function,

    /// <summary>The <c>if</c> keyword.</summary>
    If,

    /// <summary>The <c>else</c> keyword.</summary>
    Else,

    /// <summary>The <c>for</c> keyword.</summary>
    For,

    /// <summary>The <c>intersection_for</c> keyword.</summary>
    IntersectionFor,

    /// <summary>The <c>let</c> keyword.</summary>
    Let,

    /// <summary>The <c>each</c> keyword.</summary>
    Each,

    /// <summary>The <c>assert</c> keyword.</summary>
    Assert,

    /// <summary>The <c>echo</c> keyword.</summary>
    Echo,

    /// <summary>The <c>true</c> keyword.</summary>
    True,

    /// <summary>The <c>false</c> keyword.</summary>
    False,

    /// <summary>The <c>undef</c> keyword.</summary>
    Undef,

    /// <summary>The <c>(</c> punctuation.</summary>
    LeftParen,

    /// <summary>The <c>)</c> punctuation.</summary>
    RightParen,

    /// <summary>The <c>{</c> punctuation.</summary>
    LeftBrace,

    /// <summary>The <c>}</c> punctuation.</summary>
    RightBrace,

    /// <summary>The <c>[</c> punctuation.</summary>
    LeftBracket,

    /// <summary>The <c>]</c> punctuation.</summary>
    RightBracket,

    /// <summary>The <c>;</c> punctuation.</summary>
    Semicolon,

    /// <summary>The <c>,</c> punctuation.</summary>
    Comma,

    /// <summary>The <c>.</c> punctuation.</summary>
    Dot,

    /// <summary>The <c>:</c> punctuation.</summary>
    Colon,

    /// <summary>The <c>?</c> punctuation.</summary>
    Question,

    /// <summary>The <c>=</c> operator.</summary>
    Assign,

    /// <summary>The <c>==</c> operator.</summary>
    EqualEqual,

    /// <summary>The <c>!=</c> operator.</summary>
    NotEqual,

    /// <summary>The <c>&lt;</c> operator.</summary>
    Less,

    /// <summary>The <c>&lt;=</c> operator.</summary>
    LessEqual,

    /// <summary>The <c>&gt;</c> operator.</summary>
    Greater,

    /// <summary>The <c>&gt;=</c> operator.</summary>
    GreaterEqual,

    /// <summary>The <c>&amp;&amp;</c> operator.</summary>
    AndAnd,

    /// <summary>The <c>||</c> operator.</summary>
    OrOr,

    /// <summary>The <c>+</c> operator.</summary>
    Plus,

    /// <summary>The <c>-</c> operator.</summary>
    Minus,

    /// <summary>The <c>*</c> operator (also the disable modifier).</summary>
    Star,

    /// <summary>The <c>/</c> operator.</summary>
    Slash,

    /// <summary>The <c>%</c> operator (also the background modifier).</summary>
    Percent,

    /// <summary>The <c>^</c> operator.</summary>
    Caret,

    /// <summary>The <c>!</c> operator (also the show-only modifier).</summary>
    Bang,

    /// <summary>The <c>#</c> character (the highlight modifier).</summary>
    Hash,
}
