// Portions derived from Vega (BSD-3-Clause), d3-scale-chromatic (ISC; ColorBrewer colors Apache-2.0), matplotlib colormaps (CC0), cividis (Battelle license) and Turbo (Apache-2.0); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     The table of color schemes: data only, in the compact form of Vega's palette tables.
/// </summary>
/// <remarks>
///     <para>
///         Every scheme is one run of six-digit hexadecimal colors (<c>rrggbb</c>, no separator): a categorical scheme
///         lists its colors in category order, a continuous scheme lists the stops of the piecewise linear ramp that
///         spans 0 to 1. The values are facts of published palettes and were transcribed from the palette tables of
///         Vega (<c>vega-scale</c> <c>palettes.js</c>), whose categorical sets in turn come from d3-scale-chromatic;
///         the origin and license of every group is recorded in <c>third-party/README.md</c>:
///     </para>
///     <list type="bullet">
///         <item><description>the ColorBrewer colors (the qualitative sets accent, dark2, paired, pastel1, pastel2, set1, set2, set3 and the sequential and diverging ramps blues to spectral) are licensed under the Apache License 2.0, Copyright 2002 Cynthia Brewer, Mark Harrower and The Pennsylvania State University;</description></item>
///         <item><description>viridis, magma, inferno and plasma are the matplotlib colormaps, released to the public domain (CC0) by Nathaniel J. Smith, Stefan van der Walt and Eric Firing;</description></item>
///         <item><description>cividis is the colormap of Jamie R. Nunez, Christopher R. Anderton and Ryan S. Renslow (Battelle Memorial Institute license), sampled as Vega samples it;</description></item>
///         <item><description>turbo is the colormap of Anton Mikhailov (Google LLC, Apache License 2.0), sampled as Vega samples it;</description></item>
///         <item><description>category10, category20, category20b, category20c, observable10, rainbow and sinebow come from d3-scale-chromatic (ISC), and the remaining schemes (tableau10, tableau20, blueOrange, browns, tealBlues, teals, warmGreys and the gold, light and dark ramps) are published by Vega (BSD-3-Clause), whose documentation credits Maureen Stone of Tableau Software with part of them.</description></item>
///     </list>
///     <para>
///         The table is decoded by <see cref="ColorScheme"/> the first time a scheme is used. The scheme names are
///         spelled as Vega documents them; the lookup ignores the letter case.
///     </para>
/// </remarks>
internal static partial class ColorSchemes
{
    /// <summary>The categorical scheme <c>accent</c> (ColorBrewer, 8 colors).</summary>
    private const string Accent = "7fc97fbeaed4fdc086ffff99386cb0f0027fbf5b17666666";

    /// <summary>The categorical scheme <c>category10</c> (d3, 10 colors).</summary>
    private const string Category10 = "1f77b4ff7f0e2ca02cd627289467bd8c564be377c27f7f7fbcbd2217becf";

    /// <summary>The categorical scheme <c>category20</c> (d3, 20 colors).</summary>
    private const string Category20 = "1f77b4aec7e8ff7f0effbb782ca02c98df8ad62728ff98969467bdc5b0d58c564bc49c94e377c2f7b6d27f7f7fc7c7c7bcbd22dbdb8d17becf9edae5";

    /// <summary>The categorical scheme <c>category20b</c> (d3, 20 colors).</summary>
    private const string Category20B = "393b795254a36b6ecf9c9ede6379398ca252b5cf6bcedb9c8c6d31bd9e39e7ba52e7cb94843c39ad494ad6616be7969c7b4173a55194ce6dbdde9ed6";

    /// <summary>The categorical scheme <c>category20c</c> (d3, 20 colors).</summary>
    private const string Category20C = "3182bd6baed69ecae1c6dbefe6550dfd8d3cfdae6bfdd0a231a35474c476a1d99bc7e9c0756bb19e9ac8bcbddcdadaeb636363969696bdbdbdd9d9d9";

    /// <summary>The categorical scheme <c>dark2</c> (ColorBrewer, 8 colors).</summary>
    private const string Dark2 = "1b9e77d95f027570b3e7298a66a61ee6ab02a6761d666666";

    /// <summary>The categorical scheme <c>observable10</c> (d3, 10 colors).</summary>
    private const string Observable10 = "4269d0efb118ff725c6cc5b03ca951ff8ab7a463f297bbf59c6b4e9498a0";

    /// <summary>The categorical scheme <c>paired</c> (ColorBrewer, 12 colors).</summary>
    private const string Paired = "a6cee31f78b4b2df8a33a02cfb9a99e31a1cfdbf6fff7f00cab2d66a3d9affff99b15928";

    /// <summary>The categorical scheme <c>pastel1</c> (ColorBrewer, 9 colors).</summary>
    private const string Pastel1 = "fbb4aeb3cde3ccebc5decbe4fed9a6ffffcce5d8bdfddaecf2f2f2";

    /// <summary>The categorical scheme <c>pastel2</c> (ColorBrewer, 8 colors).</summary>
    private const string Pastel2 = "b3e2cdfdcdaccbd5e8f4cae4e6f5c9fff2aef1e2cccccccc";

    /// <summary>The categorical scheme <c>set1</c> (ColorBrewer, 9 colors).</summary>
    private const string Set1 = "e41a1c377eb84daf4a984ea3ff7f00ffff33a65628f781bf999999";

    /// <summary>The categorical scheme <c>set2</c> (ColorBrewer, 8 colors).</summary>
    private const string Set2 = "66c2a5fc8d628da0cbe78ac3a6d854ffd92fe5c494b3b3b3";

    /// <summary>The categorical scheme <c>set3</c> (ColorBrewer, 12 colors).</summary>
    private const string Set3 = "8dd3c7ffffb3bebadafb807280b1d3fdb462b3de69fccde5d9d9d9bc80bdccebc5ffed6f";

    /// <summary>The categorical scheme <c>tableau10</c> (Vega, 10 colors).</summary>
    private const string Tableau10Data = "4c78a8f58518e4575672b7b254a24beeca3bb279a2ff9da69d755dbab0ac";

    /// <summary>The categorical scheme <c>tableau20</c> (Vega, 20 colors).</summary>
    private const string Tableau20 = "4c78a89ecae9f58518ffbf7954a24b88d27ab79a20f2cf5b43989483bcb6e45756ff9d9879706ebab0acd67195fcbfd2b279a2d6a5c99e765fd8b5a5";

    /// <summary>The continuous scheme <c>blues</c> (11 stops).</summary>
    private const string Blues = "cfe1f2bed8eca8cee58fc1de74b2d75ba3cf4592c63181bd206fb2125ca40a4a90";

    /// <summary>The continuous scheme <c>greens</c> (11 stops).</summary>
    private const string Greens = "d3eecdc0e6baabdda594d3917bc77d60ba6c46ab5e329a512089430e7735036429";

    /// <summary>The continuous scheme <c>greys</c> (11 stops).</summary>
    private const string Greys = "e2e2e2d4d4d4c4c4c4b1b1b19d9d9d8888887575756262624d4d4d3535351e1e1e";

    /// <summary>The continuous scheme <c>oranges</c> (11 stops).</summary>
    private const string Oranges = "fdd8b3fdc998fdb87bfda55efc9244f87f2cf06b18e4580bd14904b93d029f3303";

    /// <summary>The continuous scheme <c>purples</c> (11 stops).</summary>
    private const string Purples = "e2e1efd4d4e8c4c5e0b4b3d6a3a0cc928ec3827cb97566ae684ea25c3696501f8c";

    /// <summary>The continuous scheme <c>reds</c> (11 stops).</summary>
    private const string Reds = "fdc9b4fcb49afc9e80fc8767fa7051f6573fec3f2fdc2a25c81b1db21218970b13";

    /// <summary>The continuous scheme <c>blueGreen</c> (11 stops).</summary>
    private const string BlueGreen = "d5efedc1e8e0a7ddd18bd2be70c6a958ba9144ad77319c5d2089460e7736036429";

    /// <summary>The continuous scheme <c>bluePurple</c> (11 stops).</summary>
    private const string BluePurple = "ccddecbad0e4a8c2dd9ab0d4919cc98d85be8b6db28a55a6873c99822287730f71";

    /// <summary>The continuous scheme <c>greenBlue</c> (11 stops).</summary>
    private const string GreenBlue = "d3eecec5e8c3b1e1bb9bd8bb82cec269c2ca51b2cd3c9fc7288abd1675b10b60a1";

    /// <summary>The continuous scheme <c>orangeRed</c> (11 stops).</summary>
    private const string OrangeRed = "fddcaffdcf9bfdc18afdad77fb9562f67d53ee6545e24932d32d1ebf130da70403";

    /// <summary>The continuous scheme <c>purpleBlue</c> (11 stops).</summary>
    private const string PurpleBlue = "dbdaebc8cee4b1c3de97b7d87bacd15b9fc93a90c01e7fb70b70ab056199045281";

    /// <summary>The continuous scheme <c>purpleBlueGreen</c> (11 stops).</summary>
    private const string PurpleBlueGreen = "dbd8eac8cee4b0c3de93b7d872acd1549fc83892bb1c88a3097f8702736b016353";

    /// <summary>The continuous scheme <c>purpleRed</c> (11 stops).</summary>
    private const string PurpleRed = "dcc9e2d3b3d7ce9eccd186c0da6bb2e14da0e23189d91e6fc61159ab07498f023a";

    /// <summary>The continuous scheme <c>redPurple</c> (11 stops).</summary>
    private const string RedPurple = "fccfccfcbec0faa9b8f98faff571a5ec539ddb3695c41b8aa908808d0179700174";

    /// <summary>The continuous scheme <c>yellowGreen</c> (11 stops).</summary>
    private const string YellowGreen = "e4f4acd1eca0b9e2949ed68880c97c62bb6e47aa5e3297502083440e723b036034";

    /// <summary>The continuous scheme <c>yellowOrangeBrown</c> (11 stops).</summary>
    private const string YellowOrangeBrown = "feeaa1fedd84fecc63feb746fca031f68921eb7215db5e0bc54c05ab3d038f3204";

    /// <summary>The continuous scheme <c>yellowOrangeRed</c> (11 stops).</summary>
    private const string YellowOrangeRed = "fee087fed16ffebd59fea849fd903efc7335f9522bee3423de1b20ca0b22af0225";

    /// <summary>The continuous scheme <c>blueOrange</c> (11 stops).</summary>
    private const string BlueOrange = "134b852f78b35da2cb9dcae1d2e5eff2f0ebfce0bafbbf74e8932fc5690d994a07";

    /// <summary>The continuous scheme <c>brownBlueGreen</c> (11 stops).</summary>
    private const string BrownBlueGreen = "704108a0651ac79548e3c78af3e6c6eef1eac9e9e48ed1c74da79e187a72025147";

    /// <summary>The continuous scheme <c>purpleGreen</c> (11 stops).</summary>
    private const string PurpleGreen = "5b1667834792a67fb6c9aed3e6d6e8eff0efd9efd5aedda971bb75368e490e5e29";

    /// <summary>The continuous scheme <c>purpleOrange</c> (11 stops).</summary>
    private const string PurpleOrange = "4114696647968f83b7b9b4d6dadbebf3eeeafce0bafbbf74e8932fc5690d994a07";

    /// <summary>The continuous scheme <c>redBlue</c> (11 stops).</summary>
    private const string RedBlue = "8c0d25bf363adf745ef4ae91fbdbc9f2efeed2e5ef9dcae15da2cb2f78b3134b85";

    /// <summary>The continuous scheme <c>redGrey</c> (11 stops).</summary>
    private const string RedGrey = "8c0d25bf363adf745ef4ae91fcdccbfaf4f1e2e2e2c0c0c0969696646464343434";

    /// <summary>The continuous scheme <c>yellowGreenBlue</c> (11 stops).</summary>
    private const string YellowGreenBlue = "eff9bddbf1b4bde5b594d5b969c5be45b4c22c9ec02182b82163aa23479c1c3185";

    /// <summary>The continuous scheme <c>redYellowBlue</c> (11 stops).</summary>
    private const string RedYellowBlue = "a50026d4322cf16e43fcac64fedd90faf8c1dcf1ecabd6e875abd04a74b4313695";

    /// <summary>The continuous scheme <c>redYellowGreen</c> (11 stops).</summary>
    private const string RedYellowGreen = "a50026d4322cf16e43fcac63fedd8df9f7aed7ee8ea4d86e64bc6122964f006837";

    /// <summary>The continuous scheme <c>pinkYellowGreen</c> (11 stops).</summary>
    private const string PinkYellowGreen = "8e0152c0267edd72adf0b3d6faddedf5f3efe1f2cab6de8780bb474f9125276419";

    /// <summary>The continuous scheme <c>spectral</c> (11 stops).</summary>
    private const string Spectral = "9e0142d13c4bf0704afcac63fedd8dfbf8b0e0f3a1a9dda269bda94288b55e4fa2";

    /// <summary>The continuous scheme <c>viridis</c> (matplotlib, 31 stops).</summary>
    private const string Viridis = "440154470e61481a6c482575472f7d443a834144873d4e8a39568c35608d31688e2d708e2a788e27818e23888e21918d1f988b1fa08822a8842ab07f35b77943bf7154c56866cc5d7ad1518fd744a5db36bcdf27d2e21be9e51afde725";

    /// <summary>The continuous scheme <c>magma</c> (matplotlib, 31 stops).</summary>
    private const string Magma = "0000040404130b0924150e3720114b2c11603b0f704a107957157e651a80721f817f24828c29819a2e80a8327db6377ac43c75d1426fde4968e95462f1605df76f5cfa7f5efc8f65fe9f6dfeaf78febf84fece91fddea0fcedaffcfdbf";

    /// <summary>The continuous scheme <c>inferno</c> (matplotlib, 31 stops).</summary>
    private const string Inferno = "0000040403130c0826170c3b240c4f330a5f420a68500d6c5d126e6b176e781c6d86216b932667a12b62ae305cbb3755c73e4cd24644dd513ae65c30ed6925f3771af8850ffb9506fca50afcb519fac62df6d645f2e661f3f484fcffa4";

    /// <summary>The continuous scheme <c>plasma</c> (matplotlib, 31 stops).</summary>
    private const string Plasma = "0d088723069033059742039d5002a25d01a66a00a87801a88405a7900da49c179ea72198b12a90ba3488c33d80cb4779d35171da5a69e16462e76e5bed7953f2834cf68f44fa9a3dfca636fdb32ffec029fcce25f9dc24f5ea27f0f921";

    /// <summary>The continuous scheme <c>cividis</c> (Nunez et al., 64 stops).</summary>
    private const string Cividis = "00205100235800265d002961012b65042e670831690d346b11366c16396d1c3c6e213f6e26426e2c456e31476e374a6e3c4d6e42506e47536d4c566d51586e555b6e5a5e6e5e616e62646f66676f6a6a706e6d717270717573727976737c79747f7c75827f758682768985778c8877908b78938e789691789a94789e9778a19b78a59e77a9a177aea575b2a874b6ab73bbaf71c0b26fc5b66dc9b96acebd68d3c065d8c462ddc85fe2cb5ce7cf58ebd355f0d652f3da4ff7de4cfae249fce647";

    /// <summary>The continuous scheme <c>rainbow</c> (d3, 31 stops).</summary>
    private const string Rainbow = "6e40aa883eb1a43db3bf3cafd83fa4ee4395fe4b83ff576eff6659ff7847ff8c38f3a130e2b72fcfcc36bee044aff05b8ff4576ff65b52f6673af27828ea8d1ddfa319d0b81cbecb23abd82f96e03d82e14c6edb5a5dd0664dbf6e40aa";

    /// <summary>The continuous scheme <c>sinebow</c> (d3, 31 stops).</summary>
    private const string Sinebow = "ff4040fc582af47218e78d0bd5a703bfbf00a7d5038de70b72f41858fc2a40ff402afc5818f4720be78d03d5a700bfbf03a7d50b8de71872f42a58fc4040ff582afc7218f48d0be7a703d5bf00bfd503a7e70b8df41872fc2a58ff4040";

    /// <summary>The continuous scheme <c>turbo</c> (Google, 64 stops).</summary>
    private const string Turbo = "23171b32204a3e2a71453493493eae4b49c54a53d7485ee44569ee4074f53c7ff8378af93295f72e9ff42ba9ef28b3e926bce125c5d925cdcf27d5c629dcbc2de3b232e9a738ee9d3ff39347f68950f9805afc7765fd6e70fe667cfd5e88fc5795fb51a1f84badf545b9f140c5ec3cd0e637dae034e4d931ecd12ef4c92bfac029ffb626ffad24ffa223ff9821ff8d1fff821dff771cfd6c1af76118f05616e84b14df4111d5380fcb2f0dc0260ab61f07ac1805a313029b0f00950c00910b00";

    /// <summary>The continuous scheme <c>browns</c> (Vega, 11 stops).</summary>
    private const string Browns = "eedbbdecca96e9b97ae4a865dc9856d18954c7784cc0673fb85536ad44339f3632";

    /// <summary>The continuous scheme <c>tealBlues</c> (Vega, 9 stops).</summary>
    private const string TealBlues = "bce4d89dd3d181c3cb65b3c245a2b9368fae347da0306a932c5985";

    /// <summary>The continuous scheme <c>teals</c> (Vega, 11 stops).</summary>
    private const string Teals = "bbdfdfa2d4d58ac9c975bcbb61b0af4da5a43799982b8b8c1e7f7f127273006667";

    /// <summary>The continuous scheme <c>warmGreys</c> (Vega, 11 stops).</summary>
    private const string WarmGreys = "dcd4d0cec5c1c0b8b4b3aaa7a59c9998908c8b827f7e7673726866665c5a59504e";

    /// <summary>The continuous scheme <c>goldGreen</c> (Vega, 11 stops).</summary>
    private const string GoldGreen = "f4d166d5ca60b6c35c98bb597cb25760a6564b9c533f8f4f33834a257740146c36";

    /// <summary>The continuous scheme <c>goldOrange</c> (Vega, 11 stops).</summary>
    private const string GoldOrange = "f4d166f8be5cf8aa4cf5983bf3852aef701be2621fd65322c54923b142239e3a26";

    /// <summary>The continuous scheme <c>goldRed</c> (Vega, 11 stops).</summary>
    private const string GoldRed = "f4d166f6be59f9aa51fc964ef6834bee734ae56249db5247cf4244c43141b71d3e";

    /// <summary>The continuous scheme <c>lightGreyRed</c> (Vega, 10 stops).</summary>
    private const string LightGreyRed = "efe9e6e1dad7d5cbc8c8bdb9bbaea9cd967ddc7b43e15f19df4011dc000b";

    /// <summary>The continuous scheme <c>lightGreyTeal</c> (Vega, 10 stops).</summary>
    private const string LightGreyTeal = "e4eaead6dcddc8ced2b7c2c7a6b4bc64b0bf22a6c32295c11f85be1876bc";

    /// <summary>The continuous scheme <c>lightMulti</c> (Vega, 9 stops).</summary>
    private const string LightMulti = "e0f1f2c4e9d0b0de9fd0e181f6e072f6c053f3993ef77440ef4a3c";

    /// <summary>The continuous scheme <c>lightOrange</c> (Vega, 10 stops).</summary>
    private const string LightOrange = "f2e7daf7d5baf9c499fab184fa9c73f68967ef7860e8645bde515bd43d5b";

    /// <summary>The continuous scheme <c>lightTealBlue</c> (Vega, 10 stops).</summary>
    private const string LightTealBlue = "e3e9e0c0dccf9aceca7abfc859afc0389fb9328dad2f7ca0276b95255988";

    /// <summary>The continuous scheme <c>darkBlue</c> (Vega, 10 stops).</summary>
    private const string DarkBlue = "3232322d46681a5c930074af008cbf05a7ce25c0dd38daed50f3faffffff";

    /// <summary>The continuous scheme <c>darkGold</c> (Vega, 10 stops).</summary>
    private const string DarkGold = "3c3c3c584b37725e348c7631ae8b2bcfa424ecc31ef9de30fff184ffffff";

    /// <summary>The continuous scheme <c>darkGreen</c> (Vega, 10 stops).</summary>
    private const string DarkGreen = "3a3a3a215748006f4d048942489e4276b340a6c63dd2d836ffeb2cffffaa";

    /// <summary>The continuous scheme <c>darkMulti</c> (Vega, 7 stops).</summary>
    private const string DarkMulti = "3737371f5287197d8c29a86995ce3fffe800ffffff";

    /// <summary>The continuous scheme <c>darkRed</c> (Vega, 9 stops).</summary>
    private const string DarkRed = "3434347036339e3c38cc4037e75d1eec8620eeab29f0ce32ffeb2c";

    /// <summary>
    ///     Builds the catalog: the categorical schemes in alphabetical order, then the continuous ones in the order of
    ///     Vega's palette table.
    /// </summary>
    /// <returns>The 68 schemes.</returns>
    private static ColorScheme[] BuildCatalog() =>
    [
        new("accent", false, Accent),
        new("category10", false, Category10),
        new("category20", false, Category20),
        new("category20b", false, Category20B),
        new("category20c", false, Category20C),
        new("dark2", false, Dark2),
        new("observable10", false, Observable10),
        new("paired", false, Paired),
        new("pastel1", false, Pastel1),
        new("pastel2", false, Pastel2),
        new("set1", false, Set1),
        new("set2", false, Set2),
        new("set3", false, Set3),
        new("tableau10", false, Tableau10Data),
        new("tableau20", false, Tableau20),
        new("blues", true, Blues),
        new("greens", true, Greens),
        new("greys", true, Greys),
        new("oranges", true, Oranges),
        new("purples", true, Purples),
        new("reds", true, Reds),
        new("blueGreen", true, BlueGreen),
        new("bluePurple", true, BluePurple),
        new("greenBlue", true, GreenBlue),
        new("orangeRed", true, OrangeRed),
        new("purpleBlue", true, PurpleBlue),
        new("purpleBlueGreen", true, PurpleBlueGreen),
        new("purpleRed", true, PurpleRed),
        new("redPurple", true, RedPurple),
        new("yellowGreen", true, YellowGreen),
        new("yellowOrangeBrown", true, YellowOrangeBrown),
        new("yellowOrangeRed", true, YellowOrangeRed),
        new("blueOrange", true, BlueOrange),
        new("brownBlueGreen", true, BrownBlueGreen),
        new("purpleGreen", true, PurpleGreen),
        new("purpleOrange", true, PurpleOrange),
        new("redBlue", true, RedBlue),
        new("redGrey", true, RedGrey),
        new("yellowGreenBlue", true, YellowGreenBlue),
        new("redYellowBlue", true, RedYellowBlue),
        new("redYellowGreen", true, RedYellowGreen),
        new("pinkYellowGreen", true, PinkYellowGreen),
        new("spectral", true, Spectral),
        new("viridis", true, Viridis),
        new("magma", true, Magma),
        new("inferno", true, Inferno),
        new("plasma", true, Plasma),
        new("cividis", true, Cividis),
        new("rainbow", true, Rainbow),
        new("sinebow", true, Sinebow),
        new("turbo", true, Turbo),
        new("browns", true, Browns),
        new("tealBlues", true, TealBlues),
        new("teals", true, Teals),
        new("warmGreys", true, WarmGreys),
        new("goldGreen", true, GoldGreen),
        new("goldOrange", true, GoldOrange),
        new("goldRed", true, GoldRed),
        new("lightGreyRed", true, LightGreyRed),
        new("lightGreyTeal", true, LightGreyTeal),
        new("lightMulti", true, LightMulti),
        new("lightOrange", true, LightOrange),
        new("lightTealBlue", true, LightTealBlue),
        new("darkBlue", true, DarkBlue),
        new("darkGold", true, DarkGold),
        new("darkGreen", true, DarkGreen),
        new("darkMulti", true, DarkMulti),
        new("darkRed", true, DarkRed),
    ];
}
