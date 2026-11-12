// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <summary>
///     Creates the exception of a failed database operation: a syntax error when the diagram is opened, a render failure
///     when a render's configuration runs the statements again.
/// </summary>
/// <param name="code">The diagnostic code (<see cref="MermaidDiagnosticCodes.SyntaxError"/> or <see cref="MermaidDiagnosticCodes.LimitExceeded"/>).</param>
/// <param name="message">The message.</param>
/// <param name="location">The location of the statement.</param>
/// <param name="hint">The suggested fix.</param>
/// <returns>The exception.</returns>
internal delegate Exception GitGraphFailure(string code, string message, MermaidLocation location, string hint);

/// <summary>
///     A commit of a git graph: an entry of Mermaid's commit map.
/// </summary>
/// <param name="Id">The id (sanitized, or generated as <c>{seq}-{7 hex digits}</c>).</param>
/// <param name="Message">The message (sanitized, or composed for a merge or a cherry-pick).</param>
/// <param name="Seq">The sequence number.</param>
/// <param name="Type">Mermaid's commit type: 0 normal, 1 reverse, 2 highlight, 3 merge, 4 cherry-pick.</param>
/// <param name="Tags">The tags in order.</param>
/// <param name="Parents">The ids of the parents.</param>
/// <param name="Branch">The branch the commit was made on.</param>
/// <param name="Location">The location of the statement that made the commit.</param>
internal sealed record GitCommit(string Id, string Message, int Seq, int Type, IReadOnlyList<string> Tags, IReadOnlyList<string> Parents, string Branch, MermaidLocation Location)
{
    /// <summary>The type of a normal commit.</summary>
    public const int Normal = 0;

    /// <summary>The type of a reverse commit.</summary>
    public const int Reverse = 1;

    /// <summary>The type of a highlighted commit.</summary>
    public const int Highlight = 2;

    /// <summary>The type of a merge.</summary>
    public const int Merge = 3;

    /// <summary>The type of a cherry-pick.</summary>
    public const int CherryPick = 4;

    /// <summary>Gets the <c>type:</c> of a merge (its symbol), or null.</summary>
    public int? CustomType { get; init; }

    /// <summary>Gets, for a merge, whether its id was given (its label is drawn only then); null for other commits.</summary>
    public bool? CustomId { get; init; }
}

/// <summary>
///     A branch of a git graph (an entry of Mermaid's <c>branchConfig</c>): its name and order.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Order">The order (0 when the statement gives none; the main branch's from the configuration).</param>
/// <param name="Location">The location of the statement that made the branch (the diagram keyword for the main branch).</param>
internal sealed record GitBranch(string Name, double Order, MermaidLocation Location);

/// <summary>
///     The git graph database: a port of Mermaid's <c>gitGraphAst.ts</c> (<c>commit</c>, <c>branch</c>,
///     <c>checkout</c>, <c>merge</c> and <c>cherryPick</c>, with their errors in Mermaid's order and with Mermaid's
///     messages), run over the statements of a diagram for one main branch name and order.
/// </summary>
/// <remarks>
///     <para>
///         <b>Texts.</b> Ids, messages, branch names, commit and cherry-pick tags and parents are sanitized as Mermaid's
///         <c>sanitizeText</c> sanitizes them under the gitGraph configuration (<see cref="Sanitize"/>); the tags of a
///         merge are kept as written, as in Mermaid.
///     </para>
///     <para>
///         <b>Commits.</b> The commits are kept in the order of Mermaid's commit map: a commit whose id is already in the
///         map replaces the earlier commit in its place (Mermaid only logs a warning). A commit without an id gets
///         <c>{seq}-{7 hex digits}</c>: Mermaid draws the digits from <c>Math.random</c>; here they come from the
///         mulberry32 sequence seeded with <see cref="IdSeed"/>, restarted for every run (the top four bits of each 32-bit
///         output make a digit), so that the ids are the same in every render (and the same as those of the seeded
///         reference renders).
///     </para>
///     <para>
///         <b>Branches.</b> The branches are kept in the order they were made (the main branch first) and sorted for
///         drawing by their order, stably, as JavaScript's sort does (<see cref="BranchOrder"/>).
///     </para>
///     <para>
///         The run is linear in the statements, with hash lookups. An instance is never changed after its run and is safe
///         for concurrent use.
///     </para>
/// </remarks>
internal sealed class GitGraphDatabase
{
    /// <summary>The seed of the id sequence (mulberry32), shared with the reference harness.</summary>
    internal const uint IdSeed = 0x2545F491;

    /// <summary>The number of hex digits of a generated id (Mermaid's <c>random({ length: 7 })</c>).</summary>
    private const int IdDigits = 7;

    /// <summary>The hint of a text that is too long.</summary>
    internal const string LabelLimitHint = "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted";

    /// <summary>The commits in the order of the commit map.</summary>
    private readonly List<GitCommit> _commits = [];

    /// <summary>The index of each commit id in <see cref="_commits"/>.</summary>
    private readonly Dictionary<string, int> _commitIndex = new(StringComparer.Ordinal);

    /// <summary>The branches in the order they were made.</summary>
    private readonly List<GitBranch> _branches = [];

    /// <summary>The head commit id of each branch (null for a branch without commits).</summary>
    private readonly Dictionary<string, string?> _heads = new(StringComparer.Ordinal);

    /// <summary>The limits of the run.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The factory of the run's exceptions.</summary>
    private readonly GitGraphFailure _failure;

    /// <summary>The state of the id sequence.</summary>
    private uint _random = IdSeed;

    /// <summary>The number of branches and commits made, the main branch included (for <see cref="MermaidLimits.MaxNodes"/>).</summary>
    private int _nodes;

    /// <summary>
    ///     Initializes an empty database: the main branch, checked out, without commits.
    /// </summary>
    /// <param name="mainBranchName">The main branch name.</param>
    /// <param name="mainBranchOrder">The main branch order.</param>
    /// <param name="keyword">The location of the diagram keyword (the main branch's).</param>
    /// <param name="limits">The limits.</param>
    /// <param name="failure">The factory of the exceptions.</param>
    private GitGraphDatabase(string mainBranchName, double mainBranchOrder, MermaidLocation keyword, MermaidLimits limits, GitGraphFailure failure)
    {
        MainBranchName = mainBranchName;
        MainBranchOrder = mainBranchOrder;
        CurrentBranch = mainBranchName;
        _limits = limits;
        _failure = failure;
        _branches.Add(new GitBranch(mainBranchName, mainBranchOrder, keyword));
        _heads[mainBranchName] = null;
        _nodes = 1;
    }

    /// <summary>Gets the main branch name of the run.</summary>
    public string MainBranchName { get; }

    /// <summary>Gets the main branch order of the run.</summary>
    public double MainBranchOrder { get; }

    /// <summary>Gets the commits in the order of Mermaid's commit map.</summary>
    public IReadOnlyList<GitCommit> Commits => _commits;

    /// <summary>Gets the branches in the order they were made, the main branch first.</summary>
    public IReadOnlyList<GitBranch> Branches => _branches;

    /// <summary>Gets the checked-out branch.</summary>
    public string CurrentBranch { get; private set; }

    /// <summary>Gets the head commit, or null.</summary>
    public GitCommit? Head { get; private set; }

    /// <summary>
    ///     Gets the branches in the order Mermaid draws them (<c>getBranchesAsObjArray</c>): sorted by their order,
    ///     stably.
    /// </summary>
    public IReadOnlyList<GitBranch> BranchOrder => [.. _branches.OrderBy(branch => branch.Order)];

    /// <summary>
    ///     Gets the head commit id of a branch.
    /// </summary>
    /// <param name="branch">The branch name.</param>
    /// <returns>The id, or null for a branch without commits.</returns>
    public string? HeadOf(string branch) => _heads.GetValueOrDefault(branch);

    /// <summary>
    ///     Gets a commit by id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The commit, or null.</returns>
    public GitCommit? Find(string id) => _commitIndex.TryGetValue(id, out var index) ? _commits[index] : null;

    /// <summary>
    ///     Runs statements against a new database.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="mainBranchName">The main branch name (<c>gitGraph.mainBranchName</c>).</param>
    /// <param name="mainBranchOrder">The main branch order (<c>gitGraph.mainBranchOrder</c>).</param>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="failure">The factory of the exceptions.</param>
    /// <returns>The database.</returns>
    /// <exception cref="Exception">The exception of <paramref name="failure"/>, at the first statement Mermaid rejects or that exceeds a limit.</exception>
    public static GitGraphDatabase Run(
        IReadOnlyList<GitStatement> statements,
        string mainBranchName,
        double mainBranchOrder,
        MermaidLocation keyword,
        MermaidLimits limits,
        GitGraphFailure failure)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(limits);
        var database = new GitGraphDatabase(mainBranchName, mainBranchOrder, keyword, limits, failure);
        foreach (var statement in statements)
        {
            switch (statement.Kind)
            {
                case GitStatementKind.Commit:
                    database.Commit(statement);
                    break;
                case GitStatementKind.Branch:
                    database.Branch(statement);
                    break;
                case GitStatementKind.Merge:
                    database.Merge(statement);
                    break;
                case GitStatementKind.Checkout:
                    database.Checkout(statement.Name!, statement.Location);
                    break;
                default:
                    database.CherryPick(statement);
                    break;
            }
        }

        return database;
    }

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does under the gitGraph configuration (which has no security
    ///     level, so <c>sanitizeMore</c> escapes): an empty text is kept; else each <c>&lt;br&gt;</c> tag is kept as
    ///     <c>&lt;br/&gt;</c> while <c>&lt;</c>, <c>&gt;</c> and <c>=</c> are escaped as <c>&amp;lt;</c>, <c>&amp;gt;</c> and
    ///     <c>&amp;equals;</c>, and the result is cleaned as DOMPurify cleans it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        // breakToPlaceholder, the escapes, then placeholderToBreak (which also turns a written #br# into a break)
        var builder = new StringBuilder(text.Length + 16);
        var i = 0;
        while (i < text.Length)
        {
            var length = LabelBuilder.MatchBreakTag(text, i);
            if (length > 0)
            {
                builder.Append("#br#");
                i += length;
                continue;
            }

            builder.Append(text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '=' => "&equals;",
                var c => c.ToString(),
            });
            i++;
        }

        return HtmlSanitizer.Sanitize(builder.Replace("#br#", "<br/>").ToString());
    }

    /// <summary>
    ///     Gets the message of a text that exceeds <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length of the text.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The message.</returns>
    internal static string LabelLimitMessage(int length, MermaidLimits limits) =>
        string.Create(CultureInfo.InvariantCulture, $"a text is {length} characters long, more than the limit of {limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)");

    /// <summary>
    ///     Draws the next generated id digits (Mermaid's <c>random({ length: 7 })</c>) from the mulberry32 sequence.
    /// </summary>
    /// <returns>The seven hex digits.</returns>
    private string RandomHex()
    {
        Span<char> digits = stackalloc char[IdDigits];
        for (var i = 0; i < IdDigits; i++)
        {
            // mulberry32; Math.floor(Math.random() * 16) is the top four bits of the 32-bit output
            unchecked
            {
                _random += 0x6D2B79F5;
                var t = (_random ^ (_random >> 15)) * (1 | _random);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                var output = t ^ (t >> 14);
                digits[i] = "0123456789abcdef"[(int)(output >> 28)];
            }
        }

        return new string(digits);
    }

    /// <summary>
    ///     Mermaid's <c>commit</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Commit(GitStatement statement)
    {
        var location = statement.Location;
        var id = Checked(SanitizeOrNull(statement.Id), location);
        var message = Checked(Sanitize(statement.Message ?? string.Empty), location)!;
        var tags = statement.Tags.Select(tag => Checked(Sanitize(tag), location)!).ToList();
        CountNode(location, "commits");
        var seq = Seq++;
        var commit = new GitCommit(
            string.IsNullOrEmpty(id) ? NewId(seq, location) : id,
            message,
            seq,
            statement.Type ?? GitCommit.Normal,
            tags,
            Head is null ? [] : [Head.Id],
            CurrentBranch,
            location);
        Add(commit);
    }

    /// <summary>
    ///     Mermaid's <c>branch</c>: the branch made at the head, then checked out.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Branch(GitStatement statement)
    {
        var location = statement.Location;
        var name = Checked(Sanitize(statement.Name!), location)!;
        if (_heads.ContainsKey(name))
        {
            throw Fail(
                $"Trying to create an existing branch. (Help: Either use a new name if you want create a new branch or try using \"checkout {name}\")",
                location,
                $"give the new branch another name, or write checkout {name} to continue on the existing branch");
        }

        CountNode(location, "branches and commits");
        _heads[name] = Head?.Id;
        _branches.Add(new GitBranch(name, statement.Order ?? 0, location));
        Checkout(name, location);
    }

    /// <summary>
    ///     Mermaid's <c>checkout</c>.
    /// </summary>
    /// <param name="branch">The branch name as written.</param>
    /// <param name="location">The location of the statement.</param>
    private void Checkout(string branch, MermaidLocation location)
    {
        var name = Checked(Sanitize(branch), location)!;
        if (!_heads.TryGetValue(name, out var head))
        {
            throw Fail(
                $"Trying to checkout branch which is not yet created. (Help try using \"branch {name}\")",
                location,
                $"create the branch first with branch {name}");
        }

        CurrentBranch = name;
        Head = string.IsNullOrEmpty(head) ? null : Find(head);
    }

    /// <summary>
    ///     Mermaid's <c>merge</c>, with its checks in Mermaid's order.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Merge(GitStatement statement)
    {
        var location = statement.Location;
        var other = Checked(Sanitize(statement.Name!), location)!;
        var customId = string.IsNullOrEmpty(statement.Id) ? statement.Id : Checked(Sanitize(statement.Id), location);
        var currentHead = HeadOf(CurrentBranch);
        var otherHead = HeadOf(other);
        var current = string.IsNullOrEmpty(currentHead) ? null : Find(currentHead);
        var otherCommit = string.IsNullOrEmpty(otherHead) ? null : Find(otherHead);
        if (current is not null && otherCommit is not null && current.Branch == other)
        {
            throw Fail($"Cannot merge branch '{other}' into itself.", location, $"the head of the current branch was made on {other}; check out another branch to merge {other} into it");
        }

        if (CurrentBranch == other)
        {
            throw Fail("Incorrect usage of \"merge\". Cannot merge a branch to itself", location, "check out the branch to merge into, then merge the other branch");
        }

        if (current is null)
        {
            throw Fail($"Incorrect usage of \"merge\". Current branch ({CurrentBranch})has no commits", location, $"commit on {CurrentBranch} before merging into it");
        }

        if (!_heads.ContainsKey(other))
        {
            throw Fail($"Incorrect usage of \"merge\". Branch to be merged ({other}) does not exist", location, $"create the branch with branch {other} and commit on it before merging it");
        }

        if (otherCommit is null)
        {
            throw Fail($"Incorrect usage of \"merge\". Branch to be merged ({other}) has no commits", location, $"commit on {other} before merging it");
        }

        if (ReferenceEquals(current, otherCommit))
        {
            throw Fail("Incorrect usage of \"merge\". Both branches have same head", location, $"commit on {other} before merging it: both branches point to the same commit");
        }

        if (!string.IsNullOrEmpty(customId) && _commitIndex.ContainsKey(customId))
        {
            throw Fail(
                $"Incorrect usage of \"merge\". Commit with id:{customId} already exists, use different custom id",
                location,
                "give the merge an id that no commit has");
        }

        CountNode(location, "commits");
        var seq = Seq++;
        var commit = new GitCommit(
            string.IsNullOrEmpty(customId) ? NewId(seq, location) : customId,
            Checked($"merged branch {other} into {CurrentBranch}", location)!,
            seq,
            GitCommit.Merge,
            statement.Tags,
            Head is null ? [] : [Head.Id, otherHead!],
            CurrentBranch,
            location)
        {
            CustomType = statement.Type,
            CustomId = !string.IsNullOrEmpty(customId),
        };
        Add(commit);
    }

    /// <summary>
    ///     Mermaid's <c>cherryPick</c>, with its checks in Mermaid's order.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void CherryPick(GitStatement statement)
    {
        var location = statement.Location;
        var sourceId = Checked(SanitizeOrNull(statement.Id), location);
        var tags = statement.Tags.Count == 0 ? null : statement.Tags.Select(tag => Checked(Sanitize(tag), location)!).ToList();
        var parent = Checked(SanitizeOrNull(statement.Parent), location);
        var source = string.IsNullOrEmpty(sourceId) ? null : Find(sourceId);
        if (source is null)
        {
            throw Fail("Incorrect usage of \"cherryPick\". Source commit id should exist and provided", location, "give the id of an existing commit, such as cherry-pick id: \"A\"");
        }

        if (!string.IsNullOrEmpty(parent) && !source.Parents.Contains(parent, StringComparer.Ordinal))
        {
            throw Fail(
                "Invalid operation: The specified parent commit is not an immediate parent of the cherry-picked commit.",
                location,
                $"give one of the parents of {source.Id} as parent:");
        }

        if (source.Type == GitCommit.Merge && string.IsNullOrEmpty(parent))
        {
            throw Fail(
                "Incorrect usage of cherry-pick: If the source commit is a merge commit, an immediate parent commit must be specified.",
                location,
                $"give the parent of the merge to pick, such as parent: \"{source.Parents[0]}\"");
        }

        if (source.Branch == CurrentBranch)
        {
            throw Fail("Incorrect usage of \"cherryPick\". Source commit is already on current branch", location, "check out another branch before picking the commit");
        }

        if (string.IsNullOrEmpty(HeadOf(CurrentBranch)))
        {
            throw Fail($"Incorrect usage of \"cherry-pick\". Current branch ({CurrentBranch})has no commits", location, $"commit on {CurrentBranch} before picking a commit into it");
        }

        CountNode(location, "commits");
        var seq = Seq++;
        var id = NewId(seq, location);
        var defaultTag = source.Type == GitCommit.Merge ? $"cherry-pick:{source.Id}|parent:{parent}" : $"cherry-pick:{source.Id}";
        var commit = new GitCommit(
            id,
            Checked($"cherry-picked {source.Message} into {CurrentBranch}", location)!,
            seq,
            GitCommit.CherryPick,
            tags is null ? [Checked(defaultTag, location)!] : [.. tags.Where(tag => tag.Length > 0)],
            Head is null ? [] : [Head.Id, source.Id],
            CurrentBranch,
            location);
        Add(commit);
    }

    /// <summary>Gets or sets the next sequence number.</summary>
    private int Seq { get; set; }

    /// <summary>
    ///     Makes a generated id: the sequence number, a dash and seven digits of the sequence.
    /// </summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The id.</returns>
    private string NewId(int seq, MermaidLocation location) =>
        Checked(seq.ToString(CultureInfo.InvariantCulture) + "-" + RandomHex(), location)!;

    /// <summary>
    ///     Adds a commit to the map (in the place of a commit with the same id), makes it the head and the head of the
    ///     current branch.
    /// </summary>
    /// <param name="commit">The commit.</param>
    private void Add(GitCommit commit)
    {
        Head = commit;
        if (_commitIndex.TryGetValue(commit.Id, out var index))
        {
            _commits[index] = commit;
        }
        else
        {
            _commitIndex[commit.Id] = _commits.Count;
            _commits.Add(commit);
        }

        _heads[CurrentBranch] = commit.Id;
    }

    /// <summary>
    ///     Sanitizes a value that may be absent.
    /// </summary>
    /// <param name="text">The value, or null.</param>
    /// <returns>The sanitized value, or null.</returns>
    private static string? SanitizeOrNull(string? text) => text is null ? null : Sanitize(text);

    /// <summary>
    ///     Counts a branch or commit against <see cref="MermaidLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    /// <param name="what">What the diagram has too many of, for the message.</param>
    private void CountNode(MermaidLocation location, string what)
    {
        if (++_nodes > _limits.MaxNodes)
        {
            throw _failure(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} {what} (MermaidLimits.MaxNodes, the main branch, branches and commits)"),
                location,
                "split the git graph, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The text.</returns>
    private string? Checked(string? text, MermaidLocation location)
    {
        if (text is not null && text.Length > _limits.MaxLabelLength)
        {
            throw _failure(MermaidDiagnosticCodes.LimitExceeded, LabelLimitMessage(text.Length, _limits), location, LabelLimitHint);
        }

        return text;
    }

    /// <summary>
    ///     Creates the exception of an operation Mermaid's database rejects.
    /// </summary>
    /// <param name="message">Mermaid's message.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The exception.</returns>
    private Exception Fail(string message, MermaidLocation location, string hint) =>
        _failure(MermaidDiagnosticCodes.SyntaxError, message, location, hint);
}
