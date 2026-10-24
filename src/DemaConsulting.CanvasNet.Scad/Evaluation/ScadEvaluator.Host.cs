namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The evaluator's implementation of the services built-in functions use.
/// </summary>
internal sealed partial class ScadEvaluator : IScadBuiltinHost
{
    /// <summary>The random stream shared by all <c>rands</c> calls without a seed in this pass.</summary>
    private readonly ScadRandom _random;

    /// <summary>The location of the built-in call being evaluated.</summary>
    private ScadSourceLocation _hostLocation;

    /// <inheritdoc />
    ScadSourceLocation IScadBuiltinHost.Location => _hostLocation;

    /// <inheritdoc />
    int IScadBuiltinHost.MaxStringLength => _limits.MaxStringLength;

    /// <inheritdoc />
    ScadRandom IScadBuiltinHost.UnseededRandom => _random;

    /// <inheritdoc />
    void IScadBuiltinHost.Warn(string message) => Warn(message, _hostLocation);

    /// <inheritdoc />
    void IScadBuiltinHost.Unsupported(string feature, string message)
    {
        // The first report of a feature at a location may abort evaluation; repeats are silent
        var isNew = _sink.Unsupported(feature, message, _hostLocation);
        if (isNew && _settings.ThrowOnUnsupportedFeature)
        {
            throw new ScadUnsupportedFeatureException(feature, message, _hostLocation);
        }
    }

    /// <inheritdoc />
    void IScadBuiltinHost.CheckListLength(long length)
    {
        if (length > _limits.MaxListLength)
        {
            throw Fail($"List too long (limit {_limits.MaxListLength})", _hostLocation);
        }
    }

    /// <inheritdoc />
    void IScadBuiltinHost.CheckStringLength(long length)
    {
        if (length > _limits.MaxStringLength)
        {
            throw Fail($"String too long (limit {_limits.MaxStringLength})", _hostLocation);
        }
    }

    /// <inheritdoc />
    void IScadBuiltinHost.Charge(long steps) => Charge(steps);
}
