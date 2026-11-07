using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Validation of the statistical transforms: <c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c> and <c>extent</c> (a partial file of the specification parser unit).
/// </content>
/// <remarks>
///     <para>
///         The shape rules follow the Vega-Lite schema (<c>RegressionTransform</c>, <c>LoessTransform</c>, <c>DensityTransform</c>, <c>QuantileTransform</c>,
///         <c>ExtentTransform</c>): a field is a valid field path, <c>groupby</c> an array of them, <c>as</c> names the output fields, the options have
///         their types. The rules that the schema leaves open but that the renderer must bound are stricter: the order of a polynomial is an integer from 0 to
///         <see cref="RegressionFit.MaxPolynomialOrder"/> (the normal equations cost its cube), the bandwidth of a loess is in [0, 1] (the share of the points in a window, as the schema says
///         in its description), the bandwidth, the step counts and the step of a density and a quantile are not negative. A value of the right type
///         that Vega would accept but that has no meaning (an extent whose end is before its start) is not an error, because Vega computes with it.
///     </para>
///     <para>
///         The <c>param</c> of an <c>extent</c> transform is a new name: a parameter of the same name that the view or an ancestor declares would be a second signal of that name in
///         Vega, which is an error there and is here.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The regression methods of Vega-Lite (<c>RegressionMethod</c>).</summary>
    private static readonly string[] RegressionMethods = ["linear", "log", "exp", "pow", "quad", "poly"];

    /// <summary>
    ///     Validates a <c>regression</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is predicted.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateRegressionTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The regression must be the name of the field to predict.");
        ValidateOnField(entry, "regression");
        if (entry["method"] is { } method && (!method.IsString || Array.IndexOf(RegressionMethods, method.StringValue) < 0))
        {
            throw Error(method, "The 'method' of a regression must be one of linear, log, exp, pow, quad or poly.");
        }

        if (entry["order"] is { } order && (!order.IsNumber || !double.IsInteger(order.NumberValue) || order.NumberValue < 0 || order.NumberValue > RegressionFit.MaxPolynomialOrder))
        {
            throw Error(order, $"The 'order' of a regression must be an integer from 0 to {RegressionFit.MaxPolynomialOrder}.");
        }

        ValidateNumberPair(entry["extent"], "The 'extent' of a regression must be an array of two numbers.");
        if (entry["params"] is { IsBoolean: false } parameters)
        {
            throw Error(parameters, "The 'params' of a regression must be true or false.");
        }

        ValidateStatisticsCommon(entry, "regression");
    }

    /// <summary>
    ///     Validates a <c>loess</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is smoothed.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateLoessTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The loess must be the name of the field to smooth.");
        ValidateOnField(entry, "loess");
        if (entry["bandwidth"] is { } bandwidth && (!bandwidth.IsNumber || bandwidth.NumberValue < 0 || bandwidth.NumberValue > 1))
        {
            throw Error(bandwidth, "The 'bandwidth' of a loess must be a number from 0 to 1.");
        }

        ValidateStatisticsCommon(entry, "loess");
    }

    /// <summary>
    ///     Validates a <c>density</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose density is estimated.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateDensityTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The density must be the name of the field to estimate.");
        foreach (var flag in new[] { "cumulative", "counts" })
        {
            if (entry[flag] is { IsBoolean: false } value)
            {
                throw Error(value, $"The '{flag}' of a density must be true or false.");
            }
        }

        foreach (var name in new[] { "bandwidth", "steps", "minsteps", "maxsteps" })
        {
            if (entry[name] is { } number && (!number.IsNumber || number.NumberValue < 0))
            {
                throw Error(number, $"The '{name}' of a density must be a number that is not negative.");
            }
        }

        ValidateNumberPair(entry["extent"], "The 'extent' of a density must be an array of two numbers.");
        if (entry["resolve"] is { } resolve && (!resolve.IsString || resolve.StringValue is not ("shared" or "independent")))
        {
            throw Error(resolve, "The 'resolve' of a density must be 'shared' or 'independent'.");
        }

        ValidateStatisticsCommon(entry, "density");
    }

    /// <summary>
    ///     Validates a <c>quantile</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose quantiles are computed.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateQuantileTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The quantile must be the name of the field to summarize.");
        if (entry["probs"] is { } probabilities && (!probabilities.IsArray || probabilities.Items.Any(p => !p.IsNumber)))
        {
            throw Error(probabilities, "The 'probs' of a quantile must be an array of numbers.");
        }

        if (entry["step"] is { } step && (!step.IsNumber || step.NumberValue < 0))
        {
            throw Error(step, "The 'step' of a quantile must be a number that is not negative.");
        }

        ValidateStatisticsCommon(entry, "quantile");
    }

    /// <summary>
    ///     Validates an <c>extent</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose extent is computed.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape, or its parameter has the name of a declared parameter.</exception>
    private void ValidateExtentTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The extent must be the name of the field whose extent is computed.");
        if (entry["param"] is not { IsString: true } param || param.StringValue!.Length == 0)
        {
            throw Error(entry["param"] ?? entry, "The extent transform needs a text 'param' that names the parameter to set.");
        }

        if (IsParameterDeclared(param, param.StringValue))
        {
            throw Error(param, $"The extent transform sets the parameter '{param.StringValue}', which the specification also declares in 'params'.");
        }
    }

    /// <summary>
    ///     Validates the <c>on</c> field of a regression or loess.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="transform">The transform name, for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>on</c> is missing or not a field path.</exception>
    private void ValidateOnField(SpecNode entry, string transform)
    {
        if (entry["on"] is not { } on)
        {
            throw Error(entry, $"The {transform} transform needs an 'on' field.");
        }

        RequireFieldPath(on, $"The 'on' of a {transform} must be a valid field name.");
    }

    /// <summary>
    ///     Validates the <c>groupby</c> and <c>as</c> that the statistical transforms share.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="transform">The transform name, for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>groupby</c> or <c>as</c> has the wrong shape.</exception>
    private void ValidateStatisticsCommon(SpecNode entry, string transform)
    {
        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, $"The 'groupby' of a {transform} must be an array of field names.");
        }

        ValidateOutputNames(entry["as"], 1, 2, $"The 'as' of a {transform} must be an array of one or two field names.");
    }

    /// <summary>
    ///     Checks an optional array of exactly two numbers.
    /// </summary>
    /// <param name="node">The value, or null.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is present and is not an array of two numbers.</exception>
    private void ValidateNumberPair(SpecNode? node, string message)
    {
        if (node is not null && (!node.IsArray || node.Items.Count != 2 || node.Items.Any(i => !i.IsNumber)))
        {
            throw Error(node, message);
        }
    }
}
