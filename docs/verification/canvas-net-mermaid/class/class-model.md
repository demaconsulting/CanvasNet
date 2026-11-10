### ClassModel Unit Verification Design

This document describes the unit-level verification strategy for the `ClassModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Class` namespace: the internal sealed `ClassModel` (the database operations, the URL
check, the metadata, the limits, `GetData`, the reports, and `Layout`, in two partial files) and its element types
(`ClassNode`, `ClassMember`, `ClassRelation`, `ClassRelationInput`, `ClassRelationType`, `ClassNote`,
`ClassInterface`, `ClassNamespace`, and `ClassStyleClass`).

#### Verification Approach

The unit is verified by calling its operations directly on a new model, or by opening short texts and inspecting the
model, its layout data, its reports, or its scene, and comparing the result with the values that Mermaid's
`classDb.ts`, `classTypes.ts`, and class renderer give for the same input, stated in the tests as literals: the ids,
types, drawn texts, and DOM ids of classes; the display texts, drawn texts, and classifier styles of members; Mermaid's
error messages; the interface nodes of lollipops; the qualified ids, parents, and labels of namespaces; the styles and
CSS classes; the checked links; the counts; the limits and their locations; the nodes and edges of the layout data;
and the title and view box of the drawing. The model's agreement with Mermaid on whole texts is verified as well by
the upstream vectors of the ClassParser unit, whose facts are the model's database facts and layout data.

No mocking or stubbing is used; the parser, the sanitizer, the configuration resolver, and the shared rendering are
real (the helper `ClassFacts` parses a text into a model and resolves its default configuration). The internal types
are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ClassModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Class`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenario that lays out
- **Isolation**: Each test builds its own model; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any id, text, style, message, location, count, node, edge, or coordinate that differs from the asserted
Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Class-ClassModel-Classes: Classes, Generic Names, And DOM Ids

**Tests**: `ClassModelTests.ClassModel_AddClass_GenericName_SplitsTypeAndNumbersDomIds`,
`ClassModelTests.ClassModel_ParseGenericTypes_Text_FollowsMermaid`

Asserts that adding `Box~T~`, `Box`, and `Pair` keeps two classes: `Box` with the type `T`, the drawn text
`Box&lt;T&gt;`, and the DOM id `classId-Box-0`, and `Pair` with no type, the text `Pair`, and the DOM id
`classId-Pair-1` (the second `Box` adds nothing and does not advance the counter); and that five texts are written as
Mermaid's `parseGenericTypes` writes them (a theory): `List~int~` as `List<int>`, `List~List~int~~` as
`List<List<int>>`, `Map~K, V~` as `Map<K, V>`, and `~T` and `Plain` unchanged.

##### CanvasNetMermaid-Class-ClassModel-Members: Mermaid's Class Members

**Tests**: `ClassModelTests.ClassModel_AddMember_MemberText_SplitsLikeMermaid`,
`ClassModelTests.ClassModel_AddMember_AnnotationAndEmptyText_AreNotMembers`

Asserts the display text, the drawn text, and the classifier style of nine member texts (a theory), among them
`+String name` (display `+String name`, drawn `\+String name`, no style), `-int count$` (underlined), `String label*`
(italic), `#List~int~ items` (display `#List<int> items`, drawn with `&lt;` and `&gt;`), `+create()$ Shape` (display
`+create() : Shape`, underlined), `someAbstract()* int` (italic), and `+map(Function~T,R~ f) Box~R~` (display
`+map(Function<T,R> f) : Box<R>`); and that `<<interface>>` added as a member is the annotation `interface` while a
text of spaces adds no attribute and no method.

##### CanvasNetMermaid-Class-ClassModel-Errors: Mermaid's Database Errors, Located

**Tests**: `ClassModelTests.ClassModel_AddAnnotation_UnknownClass_ThrowsLocatedError`,
`ClassModelTests.ClassModel_AddMember_UnreadableMethod_ThrowsLocatedError`

Asserts that an annotation of the class `Missing`, which does not exist, is `mermaid-syntax-error` at the given
location (line 2, column 15) with Mermaid's message "Cannot read properties of undefined (reading 'annotations')"; and
that the member `(x)`, which has a `)` but does not match Mermaid's method expression, is `mermaid-syntax-error` at the
given location (line 3, column 5) with Mermaid's message "Cannot read properties of undefined (reading
'startsWith')".

##### CanvasNetMermaid-Class-ClassModel-Relations: Relations, Lollipops, And Their Markers

**Tests**: `ClassModelTests.ClassModel_AddRelation_Lollipop_AddsInterfaceNode`,
`ClassModelTests.ClassModel_GetData_Relation_MapsMarkersAndTerminalLabels`

Asserts that a relation with a lollipop at the first end only adds the interface `interface0` labeled `Ignition` for
the class `Engine` in place of a class, and a relation with lollipops at both ends adds both classes, so the classes
are `Engine`, `Left`, and `Right` and the first relation goes from `interface0` to `Engine`; and that
`Customer "1" *--> Order : places` maps to the edge `id_Customer_Order_1` with the markers `composition` and
`dependency`, a minimum length of 1, the label `places`, the start terminal label `1`, no end terminal label, and a
solid line, in layout data of type `classDiagram` with the direction `TB` and the five class markers `aggregation`,
`extension`, `composition`, `dependency`, and `lollipop`.

##### CanvasNetMermaid-Class-ClassModel-Namespaces: Qualified Namespaces In Both Modes

**Tests**: `ClassModelTests.ClassModel_AddNamespace_DottedAndNested_CreatesQualifiedNamespaces`,
`ClassModelTests.ClassModel_GetData_CompactNamespaces_KeepDeclaredOnly`

Asserts that `Company.Project` and the nested `Module` labeled `Modules` return the qualified ids `Company.Project`
and `Company.Project.Module` and give the namespaces `Company` (implicit, without a parent), `Company.Project`
(declared, in `Company`), and `Company.Project.Module` (declared, in `Company.Project`, labeled `Modules`), with
`Company.Project` the only child of `Company`; and that with `class.hierarchicalNamespaces` false in the frontmatter,
`namespace A.B.C` and `namespace A` give the clusters `A` and `A.B.C`, each labeled with its qualified id and without a
parent (the implicit `A.B` is not drawn), with the class `Deep` in `A.B.C` and the class `Top` in `A`.

##### CanvasNetMermaid-Class-ClassModel-Notes: Notes And Their Links

**Tests**: `ClassModelTests.ClassModel_GetData_NotesNamespacesAndClasses_MapToNodes`

Asserts that a note for the existing class `Order` and a note for the missing class `Missing` map to the note nodes
`note0` and `note1` with the styles `text-align: left`, `white-space: nowrap`, `fill: #fff5ad`, and `stroke: #aaaa33`
(the default theme's note colors), marked as trusted, and that only the first note has a link: the dotted edge
`edgeNote0` from `note0` to `Order` with a minimum length of 1.

##### CanvasNetMermaid-Class-ClassModel-Styles: Style Classes And Mermaid's DOM-Id Lookup

**Tests**: `ClassModelTests.ClassModel_DefineClass_AppliesToClassesThatCarryIt`,
`ClassModelTests.ClassModel_SetCssClass_DigitId_MatchesNoClass`

Asserts that `classDef warm fill:#f96,color:#036` gives the styles `fill:#f96` and `color:#036` to the class `Hot`,
which carried `warm` before the definition, and none to the class `Later`, which gets `warm` after it, and keeps the
text style `bgFill:#f96,color:#036`; and that `cssClass` for `1Alpha,Beta` leaves `1Alpha` with the CSS classes
`default` (the `classId-` prefix of a digit id matches no class) and gives `Beta` the CSS classes `default hot`.

##### CanvasNetMermaid-Class-ClassModel-Links: Links Checked As The Strict Security Level Checks Them

**Tests**: `ClassModelTests.ClassModel_FormatUrl_Link_IsCheckedLikeMermaid`

Asserts what six links become (a theory): `google.com` and `/relative/page` are kept, `https://Example.com` is
normalized to `https://example.com/`, `mailto:someone@example.com` is kept, `javascript:alert(1)` is refused as
`about:blank`, and a link of spaces is no link.

##### CanvasNetMermaid-Class-ClassModel-Metadata: Counts, Direction, And Titles

**Tests**: `ClassModelTests.ClassModel_Metadata_CountsClassesRelationsAndNamespaces`,
`ClassModelTests.ClassModel_SubgraphCount_HierarchicalNamespaces_CountsTheKeptNamespaces`

Asserts that a text with an accessible title, a direction `RL`, a namespace holding `Order`, a relation to an implicit
class, a lollipop interface, and a note gives two nodes, two edges, and one subgraph (notes, interfaces, and note links
are not counted), the direction `RL`, the accessible title `Shop`, and no title, with the interface node `interface0`
marked as trusted; and that the namespaces `A.B.C` and `D` count four subgraphs with hierarchical namespaces and two
(the declared ones) without, while the model keeps all four namespaces in both cases (a theory).

##### CanvasNetMermaid-Class-ClassModel-Limits: The Relation, Node, And Joined Label Limits

**Tests**: `ClassModelTests.ClassModel_Limits_TooManyRelationsOrNodes_ThrowLimitExceeded`,
`ClassModelTests.ClassModel_AddNamespace_TooManyNamespaces_ThrowsLimitExceeded`,
`ClassModelTests.ClassModel_AddNamespace_ImplicitAncestors_CountAgainstMaxNodes`,
`ClassModelTests.ClassModel_Limits_NodesAndNamespacesAtTheLimit_AreAccepted`,
`ClassModelTests.ClassModel_AddNamespace_LongQualifiedId_ThrowsLimitExceeded`,
`ClassModelTests.ClassModel_AddNamespace_DottedIdTooDeep_ThrowsLimitExceeded`,
`ClassModelTests.ClassModel_SetClassLabel_LabelWithGenericType_ThrowsLimitExceeded`

Asserts that with `MaxEdges` of 1 and `MaxNodes` of 3 a second relation is `mermaid-limit-exceeded` at the given
location with a message that names `MaxEdges`, and a note after three classes is `mermaid-limit-exceeded` at the given
location with a message that names `MaxNodes`. Asserts that with `MaxNodes` of 3 a class and a namespace with a class
leave no room for a second namespace, which is refused at its name (line 6, column 15) with the message
`more than 3 nodes and subgraphs (MermaidLimits.MaxNodes)`; that with `MaxNodes` of 2 the namespace
`Geo.Plane.Shapes` is refused at its name (line 2, column 15), because its implicit ancestors `Geo` and `Geo.Plane`
count; and that with `MaxNodes` of 3 the namespace `Geo.Plane` with one class, three nodes and namespaces in all, is
accepted with one node and two subgraphs. Asserts that with `MaxLabelLength` of 10 the nested namespace `Bravo` in
`Alpha`, whose qualified id `Alpha.Bravo` is 11 characters long although each name fits, is refused at its name
(line 3, column 15) with the message `a label is 11 characters long` that names `MermaidLimits.MaxLabelLength`, while
the nested namespace `Beta`, giving the 10-character qualified id `Alpha.Beta`, is accepted; that with
`MaxNestingDepth` of 3 the namespace `A.B.C.D`, four levels in one name, is refused at its name (line 2, column 15)
with the message `namespaces are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)`, while `A.B.C` is
accepted with the namespaces `A`, `A.B`, and `A.B.C`; and that
`class A~Type~["Labels"]`, whose drawn name `Labels<Type>` is 12 characters long although the name and the label each
fit, is refused at the class name (line 2, column 7) with the message `a label is 12 characters long`, while
`class A~Type~["Name"]` is accepted with the drawn name `Name<Type>`.

##### CanvasNetMermaid-Class-ClassModel-LayoutData: The Layout Data Of A Render

**Tests**: `ClassModelTests.ClassModel_GetData_Relation_MapsMarkersAndTerminalLabels`,
`ClassModelTests.ClassModel_GetData_NotesNamespacesAndClasses_MapToNodes`,
`ClassModelTests.ClassModel_GetData_NonStringNoteThemeVariables_AreStringified`,
`ClassModelTests.ClassModel_GetData_CompactNamespaces_KeepDeclaredOnly`,
`ClassModelTests.ClassModel_GetData_Spacing_FollowsTheStateSection`

Asserts the relation edge, its markers, terminal labels, and minimum length, and the diagram type, direction, and
markers of the layout data (see the Relations scenario); that a namespace `Shop` holding the class `Order` with an
attribute and a static method, and two notes, give the nodes `Shop`, `Order`, `note0`, and `note1` in that order, the
namespace as a `rect` cluster with a padding of 16 and untrusted styles, the class with the parent `Shop`, the DOM id
`classId-Order-0`, the attribute text `\+int id`, and the method text `\+total() : double` with the underline style,
and the note link (see the Notes scenario); that note theme variables a directive sets to a number or a boolean
(`noteBkgColor` 5 and `noteBorderColor` true, or false and 1.5) give a free note the styles `fill: 5` and
`stroke: true` (or `fill: false` and `stroke: 1.5`), stringified as Mermaid's template strings do (a theory); that
without hierarchical namespaces only the declared namespaces are
clusters (see the Namespaces scenario); and that `state.nodeSpacing` 80 and `state.rankSpacing` 70 give the class
diagram a node spacing of 80 and a rank spacing of 70, as Mermaid's class renderer reads them.

##### CanvasNetMermaid-Class-ClassModel-Unsupported: Looks And Layouts Are Reported In Order

**Tests**: `ClassModelTests.ClassModel_ReportUnsupported_LookAndLayout_AreReportedInOrder`

Asserts that a configuration with the `handDrawn` look and the `elk` layout reports, through a sink that does not
throw, `mermaid-look-unsupported` at line 3 and then `mermaid-layout-unsupported` at line 4, the lines of their keys,
each with a hint.

##### CanvasNetMermaid-Class-ClassModel-Drawing: The Frame Of The Drawing

**Tests**: `ClassModelTests.ClassModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox`

Asserts that a document with the frontmatter title `Shapes` and one class lays out into a scene with one title text,
anchored in the middle at the baseline -25 (above the drawing at the default title margin of 25), inside the view box,
and that the view box is the box of the class `classId-Shape-0` grown by 8 on the left, the right, and the bottom
(to three decimals).
