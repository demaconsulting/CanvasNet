### CommandRegistry

#### Verification Approach

`CommandRegistry` is verified with unit tests in `CommandRegistryTests.cs`. The tests build registries holding
`TestCommand` instances (a configurable `ICommand` defined in the test project, which records the arguments it
received and can be made to throw), so no mocking framework is needed and no shipped command is involved.
`Context` instances are created from real argument arrays.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because some capture console
output.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Names and aliases resolve ignoring case; duplicates and the reserved name `help` are rejected.
- A command runs with exactly the command arguments; a `ToolException` gets the command name; any other
  exception propagates unchanged.
- An unknown command produces a usage error with a suggestion only when one is within an edit distance of two.
- Help text lists commands, aliases, summaries, options, and the global options.

#### Test Scenarios

**CommandRegistry_CreateDefault_ReturnsRegistry**: the default registry exists, contains the shipped commands in order
(`formats`, `info`, `render` with its alias `convert`, `crop`, `resize`, `rotate`, `flip`, `pad`, `compare`, `montage`,
and
`inspect`), and
every command in it resolves by its own name.

**CommandRegistry_Register_TwoCommands_KeepsRegistrationOrder**: the command list is in registration order.

**CommandRegistry_Register_DuplicateName_ThrowsInvalidOperationException**,
**CommandRegistry_Register_DuplicateAlias_ThrowsInvalidOperationException**,
**CommandRegistry_Register_AliasRepeatsOwnName_ThrowsInvalidOperationException**, and
**CommandRegistry_Register_ReservedHelpName_ThrowsInvalidOperationException**: a duplicate name (in another
case), an alias that collides with a registered name, an alias that repeats the command's own name, and the
reserved name `help` (as a name and as an alias) are rejected.

**CommandRegistry_Register_NullCommand_ThrowsArgumentNullException**: a null command is rejected.

**CommandRegistry_Find_NameOrAlias_ReturnsCommand** and **CommandRegistry_Find_UnknownName_ReturnsNull**: the
name and the alias resolve regardless of case; an unknown name resolves to nothing.

**CommandRegistry_Run_RegisteredCommand_RunsCommandWithArguments**: the command selected by alias receives the
command arguments.

**CommandRegistry_Run_NoCommand_ThrowsUsageError** and **CommandRegistry_Run_NullContext_ThrowsArgumentNullException**:
no command is a usage error with a hint; a null context is rejected.

**CommandRegistry_Run_UnknownCommandNearAName_SuggestsClosestName**,
**CommandRegistry_Run_UnknownCommandNearAnAlias_SuggestsTheAlias**, and
**CommandRegistry_Run_UnknownCommandFarFromAll_HasNoSuggestion**: a typo gets a "Did you mean" hint naming the
closest command or alias; a name far from every command gets only the pointer to the help; the exception names
the typed command.

**CommandRegistry_Run_ToolExceptionWithoutCommand_FillsCommandName** and
**CommandRegistry_Run_ToolExceptionWithCommand_KeepsCommandName**: the canonical command name is recorded on a
`ToolException` that lacks one and an existing name is kept; code and message are intact.

**CommandRegistry_Run_UnexpectedException_PropagatesUnchanged**: the same exception instance escapes.

**CommandRegistry_Run_HelpCommandWithName_PrintsCommandHelp** and
**CommandRegistry_Run_HelpCommandWithoutName_PrintsTopLevelHelp**: the built-in help command prints a command's
help, or the top-level help.

**CommandRegistry_RenderHelp_NoCommand_ListsCommandsAndGlobalOptions**: the top-level help starts with the usage,
lists commands with aliases and summaries, lists every global option (including `--json`), and has no trailing
newline.

**CommandRegistry_RenderHelp_EmptyRegistry_OmitsCommandSection**: with no command the command section is
omitted.

**CommandRegistry_RenderHelp_Command_ShowsUsageSummaryAndOptions**: a command's help (requested by alias) shows
its usage, summary, options with value placeholders, and the global options.

**CommandRegistry_RenderHelp_HelpName_ReturnsTopLevelHelp**: the name `help` gives the top-level help.

**CommandRegistry_RenderHelp_UnknownCommand_ThrowsUsageError**: help for an unknown command is a usage error.
