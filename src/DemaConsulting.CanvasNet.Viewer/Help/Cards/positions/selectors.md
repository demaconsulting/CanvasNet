---
title: Position selectors
summary: The grammar of --positions - kinds, id globs, quoted ids, typed predicates - and shell quoting.
see-also:
  - positions/mermaid
  - positions/vega-lite
  - limits
examples:
  - name: union
    kind: command
    language: shell
    content: "canvasnet render flowchart.mmd --positions node:send --positions 'edge:@from=stock' -o flow.png --json"
    description: The Send node and every edge that leaves the stock check, in one list.
---

Each `--positions` takes one selector; repeat the option for more. An item that
any selector matches is listed once.

## Grammar

```text
selector    := "all" | kind [ ":" target ]
target      := json-string           an exact id; * and ? are literal
             | predicates            when the target has '='
             | id-pattern            * is any run of characters, ? is one
predicates  := name=value { "+" name=value }
name        := channel | field | @id | @view | @from | @to | @index
value       := json-string | bare    bare: true, false, null, a number, else text
```

## Matching

- `all` matches every item; a kind alone (`node`, `edge`, `mark`, `axis`, ...)
  every item of that kind. The keywords (`all`, the kind, the `@` names) ignore
  case; ids, channel and field names, and values do not.
- An id pattern globs the whole id: `node:s*`, `'commit:*fix*'`, `node:se?d`.
  A quoted id is exact: `node:"my*odd id"`.
- Predicates must all hold. A channel name (`x`, `y`, `color`, ... any
  Vega-Lite encoding channel, or a name in an item's `values`, such as a pie
  slice's `value`) compares with `values`. Any other name is a data field and
  compares with `data`, the fields of the source row:
  `mark:month=2025-11-01+region=Americas`. A field named like a channel can
  only be matched through that channel. A name is a word of letters, digits,
  and `_` that starts with a letter.
- Prefer data fields for marks: `data` holds what the data says, `values`
  what the encoding made of it (a month time unit puts `x` in the year 2012;
  see 'canvasnet help positions vega-lite').
- A bare value reads as `--param` reads one, so `mark:x=3` is the number 3
  (equal to 3.0) and `mark:x="3"` the text. A date in `values` is UTC ISO
  text such as `"2025-01-01T00:00:00.000Z"`; a date in `data` is the text the
  data gave. A missing name never matches; `x=null` matches a name that holds
  null. An array or object is refused.
- `@id` is the exact id, `@view` the view path, `@from` and `@to` the ends of
  an edge or message, and `@index` its number among parallel ones:
  `edge:@from=A+@to=B+@index=0`, `axis:@id=x+@view="hconcat[1]"`.
- `+` and `=` inside a quoted value are text: `mark:text="a+b"`.

## Limits

- At most {{positionsMaxSelectors}} selectors, each at most
  {{positionsMaxSelectorLength}} characters with at most
  {{positionsMaxPredicates}} predicates; a malformed selector is usage-error.

## Quoting

Quote a selector that has `*`, `?`, `"`, a space, or `&`:

- bash and zsh: single quotes, `'node:"my*odd id"'`.
- PowerShell 7.3 or later: single quotes, as in bash. Windows PowerShell 5.1
  drops the inner double quotes, so use 7.3 or later.
- cmd: no single quotes; double quotes with `\"` inside,
  `"node:\"my*odd id\""`.
