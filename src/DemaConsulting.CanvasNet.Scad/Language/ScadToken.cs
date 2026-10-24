namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     A lexical token: its kind, the text it carries, its numeric value and where it starts.
/// </summary>
/// <remarks>
///     <see cref="Text"/> holds the identifier name, the decoded string value, the directive path or the
///     exact lexeme (operators, punctuation, keywords and numbers); it is <see langword="null"/> only for
///     <see cref="ScadTokenKind.EndOfFile"/>. <see cref="Number"/> is meaningful only for
///     <see cref="ScadTokenKind.Number"/> tokens.
/// </remarks>
/// <param name="Kind">The token kind.</param>
/// <param name="Text">The token text (see remarks).</param>
/// <param name="Number">The numeric value of a number token; zero otherwise.</param>
/// <param name="Location">The location of the first character of the token (the keyword for directives).</param>
internal readonly record struct ScadToken(ScadTokenKind Kind, string? Text, double Number, ScadSourceLocation Location);
