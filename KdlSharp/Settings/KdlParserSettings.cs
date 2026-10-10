namespace KdlSharp.Settings;

/// <summary>
/// Configuration settings for KDL parsing.
/// </summary>
public sealed class KdlParserSettings
{
    /// <summary>
    /// Gets or sets the target KDL version for parsing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default: <see cref="KdlVersion.V2"/></b> - KDL v2 is the current specification and recommended for all new documents.
    /// </para>
    /// </remarks>
    public KdlVersion TargetVersion { get; set; } = KdlVersion.V2;

    /// <summary>
    /// Gets or sets the maximum nesting depth for nodes.
    /// </summary>
    /// <remarks>
    /// Prevents stack overflow from deeply nested documents. Default is 1000.
    /// </remarks>
    public int MaxNestingDepth { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to allow duplicate property keys.
    /// </summary>
    /// <remarks>
    /// Per KDL spec, duplicates are allowed (rightmost wins). Set to <c>false</c> to reject duplicates.
    /// </remarks>
    public bool AllowDuplicateProperties { get; set; } = true;

    /// <summary>
    /// Gets or sets whether parsed values keep a reference to the source text, so that their exact
    /// source text is available (for example through <see cref="Values.KdlNumber.RawText"/>).
    /// </summary>
    /// <remarks>
    /// Default is <c>false</c>. When enabled, the whole source text stays in memory for as long as any
    /// parsed node or value from it does.
    /// </remarks>
    public bool PreserveSourceText { get; set; }

    /// <summary>
    /// Creates a copy of these settings.
    /// </summary>
    public KdlParserSettings Clone()
    {
        return new KdlParserSettings
        {
            TargetVersion = TargetVersion,
            MaxNestingDepth = MaxNestingDepth,
            AllowDuplicateProperties = AllowDuplicateProperties,
            PreserveSourceText = PreserveSourceText
        };
    }
}

