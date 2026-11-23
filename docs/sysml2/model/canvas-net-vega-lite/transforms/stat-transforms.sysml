package CanvasNetVegaLite_StatTransforms {
    private import SoftwareItemMetadata::*;

    part def StatTransforms {
        doc /* The internal StatTransforms unit (StatTransforms.cs with its Regression, Loess, Density, Quantile, and Extent parts, RegressionFit.cs, CurveSampler.cs, LoessFit.cs, KernelDensity.cs, and StatisticsPrimitives.cs) of the Transforms subsystem: the statistical transforms of Vega-Lite, ported from the Vega operators they compile to (vega-regression Regression and Loess, vega-transforms KDE, Quantile, and Extent, and the vega-statistics functions they call). StatTransforms.Apply runs the transform that the entry names: regression (linear, log, exp, pow, quad, and poly, with order, extent, params, groupby, and as; the end points of a line or the adaptively sampled curve, or one row of keys, coef, and rSquared per group; a group with no more rows than the parameters of the model is skipped with a warning), loess (the tricube-weighted local linear fit of vega-statistics with its robustness iterations, the default bandwidth 0.3), density (the Gaussian kernel density or, with cumulative, the cumulative distribution, with Scott's bandwidth by default, the shared or independent resolve of Vega-Lite, the steps, minsteps, and maxsteps of the sampling, and counts), quantile (the R-7 quantiles of probs or of the range of half steps), and extent (the minimum and maximum of a field, stored with TransformContext.SetParameter as the parameter that the following transforms, bin.extent, and scale.domain read, leaving the table as it is). The groups are those of Vega's partition (the key is the JavaScript text of the array of the group values, in order of first appearance). The generated rows replace the table; they are counted in the row budget of the call before they exist, the input rows are released when the output is complete, the work is charged to the evaluation step budget, the fields and cells are checked against the limits, cancellation is polled in the long loops, and a failure is a located VegaLiteRenderException. The unit is deterministic and reads no clock, culture, or random source. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.Regression.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.Loess.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.Density.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.Quantile.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatTransforms.Extent.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/RegressionFit.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/CurveSampler.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/LoessFit.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/KernelDensity.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Transforms/StatisticsPrimitives.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/StatTransformsRegressionTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/StatTransformsDensityTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/StatTransformsQuantileTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/RegressionFitTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/CurveSamplerTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/LoessFitTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/KernelDensityTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/StatisticsPrimitivesTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/StatTestSupport.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/VegaLiteStatTransformsIntegrationTests.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/transforms/stat-transforms.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/transforms/stat-transforms.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/transforms/stat-transforms.yaml");
        }
    }
}
