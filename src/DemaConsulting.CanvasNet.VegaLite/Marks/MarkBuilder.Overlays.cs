// Portions derived from Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The overlays of the path marks of <see cref="MarkBuilder"/> (Vega-Lite's path overlay normalizer).
/// </content>
internal sealed partial class MarkBuilder
{
    /// <summary>
    ///     What distinguishes the run of an overlay from the run of the mark it overlays.
    /// </summary>
    /// <param name="Definition">The mark definition of the overlay (an object), which replaces the mark definition of the mark.</param>
    /// <param name="Orient">The orientation of the overlay mark, or null.</param>
    private sealed record OverlaySettings(ConfigNode Definition, string? Orient);

    /// <content>
    ///     The overlays of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>
        ///     Tells whether a configuration value is truthy as JavaScript sees it.
        /// </summary>
        /// <param name="node">The value.</param>
        /// <returns><see langword="true"/> for true, a number other than zero, a text that is not empty, an object and an array.</returns>
        private static bool IsTruthy(ConfigNode node) => node.Kind switch
        {
            ConfigNodeKind.Boolean => node.BoolValue,
            ConfigNodeKind.Number => node.NumberValue != 0 && !double.IsNaN(node.NumberValue),
            ConfigNodeKind.String => node.StringValue!.Length > 0,
            ConfigNodeKind.Array or ConfigNodeKind.Object => true,
            _ => false,
        };

        /// <summary>
        ///     Works out the point overlay of the mark: the <c>point</c> of the mark definition (<c>true</c>, an object, or <c>"transparent"</c>), else that of <c>config.&lt;mark&gt;</c>.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite's <c>getPointOverlay</c>. The overlay is a <c>point</c> mark with <c>opacity: 1</c>, <c>filled: true</c> and the <c>clip</c> of the mark, whatever the overlay
        ///     object adds to or replaces; <c>"transparent"</c> is the overlay <c>{"opacity": 0}</c>. <c>false</c> and null switch the overlay off, also when the configuration enables it.
        ///     (Vega-Lite enables it also for an encoding with a <c>shape</c> channel; this renderer does not.)
        /// </remarks>
        /// <returns>The mark definition of the overlay; null when the mark has no point overlay.</returns>
        private ConfigNode? PointOverlayDefinition()
        {
            if (_type is not ("line" or "area" or "trail" or "rule"))
            {
                return null;
            }

            ConfigNode overlay;
            if (Def("point") is { } point)
            {
                if (point is { Kind: ConfigNodeKind.String, StringValue: "transparent" })
                {
                    overlay = ConfigNode.Create(new Dictionary<string, ConfigNode> { ["opacity"] = ConfigNode.FromNumber(0) });
                }
                else if (IsTruthy(point))
                {
                    overlay = point.IsObject ? point : ConfigNode.EmptyObject;
                }
                else
                {
                    return null;
                }
            }
            else if (_owner._config.VegaLite[_type]?["point"] is { } configured && IsTruthy(configured))
            {
                overlay = configured.IsObject ? configured : ConfigNode.EmptyObject;
            }
            else
            {
                return null;
            }

            var defaults = new Dictionary<string, ConfigNode>(StringComparer.Ordinal)
            {
                ["opacity"] = ConfigNode.FromNumber(1),
                ["filled"] = ConfigNode.FromBoolean(true),
            };
            if (Def("clip") is { } clip)
            {
                defaults["clip"] = clip;
            }

            return ConfigNode.Merge(ConfigNode.Create(defaults), overlay);
        }

        /// <summary>
        ///     Works out the line overlay of an area: the <c>line</c> of the mark definition (<c>true</c> or an object), else that of <c>config.area</c>.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite's <c>getLineOverlay</c>. The overlay is a <c>line</c> mark with the <c>clip</c>, <c>interpolate</c> and <c>tension</c> of the area, whatever the overlay object adds
        ///     to or replaces. The line is drawn at the edge of the area, so it has no <c>x2</c> and <c>y2</c>.
        /// </remarks>
        /// <returns>The mark definition of the overlay; null when the mark has no line overlay.</returns>
        private ConfigNode? LineOverlayDefinition()
        {
            if (_type != "area")
            {
                return null;
            }

            ConfigNode overlay;
            if (Def("line") is { } line)
            {
                if (!IsTruthy(line))
                {
                    return null;
                }

                overlay = line.IsObject ? line : ConfigNode.EmptyObject;
            }
            else if (_owner._config.VegaLite[_type]?["line"] is { } configured && IsTruthy(configured))
            {
                overlay = configured.IsObject ? configured : ConfigNode.EmptyObject;
            }
            else
            {
                return null;
            }

            var defaults = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);
            foreach (var name in new[] { "clip", "interpolate", "tension" })
            {
                if (Def(name) is { } inherited)
                {
                    defaults[name] = inherited;
                }
            }

            return ConfigNode.Merge(ConfigNode.Create(defaults), overlay);
        }

        /// <summary>
        ///     Builds the overlays of the mark after its own items: the line of an area, then the points.
        /// </summary>
        /// <param name="items">The items of the mark, to which the items of the overlays are appended.</param>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when a limit is exceeded.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private void AddOverlays(List<SceneItem> items)
        {
            if (_lineOverlay is { } line)
            {
                var orient = ScaleResolver.InferOrient("line", line["orient"]?.AsString(), _view["encoding"]);
                items.AddRange(new Run(_owner, "line", null, _view, _table, _scales, _width, _height, _cancellationToken, new OverlaySettings(line, orient)).Build());
            }

            if (_pointOverlay is { } point)
            {
                items.AddRange(new Run(_owner, "point", null, _view, _table, _scales, _width, _height, _cancellationToken, new OverlaySettings(point, null)).Build());
            }
        }
    }
}
