// class-vector-cases.mjs - the class diagram texts of gen-diagram-vectors.mjs --type class. Each case is { name, spec,
// text } (and an optional site configuration). The expectations are not written here: the generator records what the
// pinned mermaid makes of each text.
//
// Attribution: most texts are the inputs of mermaid's own class parser tests at the pinned version (mermaid
// 11.17.2, packages/mermaid/src/diagrams/class/: classDiagram.spec.ts, classDiagram-styles.spec.js, classTypes.spec.ts
// and parser/class.spec.js; MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of LICENSE-mermaid.txt). Their
// spec field names the upstream file and test. Only the texts are taken; the upstream assertions are replaced by the
// facts mermaid records. The member strings of classTypes.spec.ts, which upstream gives to ClassMember directly, are
// written as class body lines. The cases whose spec field is "original" cover features the upstream specs do not
// (lollipops, two-way relations, quirks and errors), and the "fixture" cases are the class fixtures of the tests (their
// frontmatter is removed by mermaid's preprocessing; its configuration is not applied, which changes no fact here).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s class parser specs (classDiagram.spec.ts, classDiagram-styles.spec.js, classTypes.spec.ts, parser/class.spec.js; MIT), named in each case\'s spec field, plus original texts and the class fixtures.';

const lines = (...rows) => rows.join('\n');
const spec = (test) => `classDiagram.spec.ts: ${test}`;
const styles = (test) => `classDiagram-styles.spec.js: ${test}`;
const types = (test) => `classTypes.spec.ts: ${test} (as class body lines)`;
const parserSpec = (test) => `parser/class.spec.js: ${test}`;
const body = (...members) => lines('classDiagram', 'class Class1 {', ...members, '}');

// The keywords of the note tests of classDiagram.spec.ts (a selection: the upstream test runs each of 51 keywords)
const keywordCase = (form, keyword, i) => {
  const texts = {
    'note-keyword-inside': `classDiagram
                     note "This is a keyword: ${keyword}. It truly is."
                  `,
    'note-keyword-start': `classDiagram
                      note "${keyword}"`,
    'note-for-keyword-inside': `classDiagram
                   class Something {
                     int id
                     string name
                   }
                   note for Something "This is a keyword: ${keyword}. It truly is."
                   `,
    'note-for-keyword-start': `classDiagram
                    class Something {
                      int id
                      string name
                    }
                    note for Something "${keyword}"
                    `,
    'note-keyword-error': `classDiagram
                   note ${keyword}`
  };
  const titles = {
    'note-keyword-inside': `should handle a note with ${keyword} in it`,
    'note-keyword-start': `should handle note with ${keyword} at beginning of string`,
    'note-for-keyword-inside': `should handle a "note for" with a ${keyword} in it`,
    'note-for-keyword-start': `should handle a "note for" with a ${keyword} at beginning of string`,
    'note-keyword-error': `should elicit error for ${keyword} after NOTE token`
  };
  return { name: `${form}-${i}`, spec: spec(titles[form]), text: texts[form] };
};

// The class fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'class');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
  name: `fixture-${f.replace(/\.mmd$/, '')}`,
  spec: `fixture class/${f}`,
  text: readFileSync(join(fixtureFolder, f), 'utf8')
}));

const compact = { class: { hierarchicalNamespaces: false } };

export const cases = [
  // ------------------------------------------------------------------------------------------ namespaces and labels
  {
    name: 'ns-classes-within-namespaces', spec: spec('should handle classes within namespaces'), text: `classDiagram
        namespace Company.Project {
          class User {
            +login(username: String, password: String)
            +logout()
          }
        }
        namespace Company.Project.Module {
          class Admin {
            +addUser(user: User)
            +removeUser(user: User)
          }
        }`
  },
  {
    name: 'ns-generic-class', spec: spec('should handle generic classes within namespaces'), text: `classDiagram
    namespace Company.Project.Module {
        class GenericClass~T~ {
            +addItem(item: T)
            +getItem() T
        }
    }`
  },
  {
    name: 'ns-nested-relationships', spec: spec('should handle nested namespaces and relationships'), text: `      classDiagram
        namespace Company.Project.Module.SubModule {
          class Report {
            +generatePDF(data: List)
            +generateCSV(data: List)
          }
        }
        namespace Company.Project.Module {
          class Admin {
            +generateReport()
          }
        }
        Admin --> Report : generates`
  },
  {
    name: 'ns-syntactically-nested', spec: spec('should handle syntactically nested namespace blocks'), text: `classDiagram
        namespace Outer {
          namespace Inner {
            class Foo {
              +bar()
            }
          }
          class Baz {
            +qux()
          }
        }`
  },
  {
    name: 'ns-compact-explicit-only', spec: spec('emits only explicit namespaces from getData() in compact mode'), site: compact,
    text: `classDiagram
          namespace Company.Engineering.Backend {
            class Developer {
              +writeCode()
            }
          }
          namespace Company {
            class CEO {
              +makeDecisions()
            }
          }`
  },
  {
    name: 'ns-compact-nearest-explicit', spec: spec('moves classes inside implicit namespaces to the nearest explicit ancestor in compact mode'),
    site: compact, text: `classDiagram
          namespace Company.Engineering.Backend {
            class Developer
          }
          namespace Company {
            class CEO
          }`
  },
  {
    name: 'ns-hierarchical-default', spec: spec('defaults to hierarchical rendering when hierarchicalNamespaces is unset'), text: `classDiagram
        namespace A.B {
          class Foo
        }`
  },
  {
    name: 'ns-custom-label', spec: spec('should support custom labels on namespaces'), text: `classDiagram
        namespace Auth["Authentication Service"] {
          class UserService {
            +login()
          }
        }`
  },
  {
    name: 'acc-title-descr', spec: spec('should handle accTitle and accDescr'), text: `classDiagram
            accTitle: My Title
            accDescr: My Description`
  },
  {
    name: 'acc-title-multiline-descr', spec: spec('should handle accTitle and multiline accDescr'), text: `classDiagram
            accTitle: My Title
            accDescr {
              This is my multi
              line description
            }`
  },
  { name: 'header-leading-newline', spec: spec('should handle a leading newline (skipped upstream)'), text: '\nclassDiagram\n' + 'class Car' },
  {
    name: 'ns-fully-qualified-relations', spec: spec('should handle fully qualified class names in relationships'), text: `classDiagram
        namespace Company.Project.Module {
          class User
          class Admin
        }
          Admin --> User : manages`
  },
  { name: 'name-backticks', spec: spec('should handle backticked class names'), text: 'classDiagram\n' + 'class `Car`' },
  { name: 'name-dash', spec: spec('should handle class names with dash'), text: 'classDiagram\n' + 'class Ca-r' },
  { name: 'name-underscore-backticks', spec: spec('should handle class names with underscore'), text: 'classDiagram\n' + 'class `A_Car`' },
  {
    name: 'body-separators', spec: spec('should handle parsing of separators'), text:
      'classDiagram\n' +
      'class Foo1 {\n' +
      '  You can use\n' +
      '  several lines\n' +
      '..\n' +
      'as you want\n' +
      'and group\n' +
      '==\n' +
      'things together.\n' +
      '__\n' +
      'You can have as many groups\n' +
      'as you want\n' +
      '--\n' +
      'End of class\n' +
      '}\n' +
      '\n' +
      'class User {\n' +
      '.. Simple Getter ..\n' +
      '+ getName()\n' +
      '+ getAddress()\n' +
      '.. Some setter ..\n' +
      '+ setName()\n' +
      '__ private data __\n' +
      'int age\n' +
      '-- encrypted --\n' +
      'String password\n' +
      '}'
  },
  { name: 'label-text', spec: spec('should parse a class with a text label'), text: 'classDiagram\n' + 'class C1["Class 1 with text label"]' },
  {
    name: 'label-two', spec: spec('should parse two classes with text labels'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"]\n' + 'class C2["Class 2 with chars @?"]\n'
  },
  {
    name: 'label-member', spec: spec('should parse a class with a text label and member'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"]\n' + 'C1: member1'
  },
  {
    name: 'label-member-annotation', spec: spec('should parse a class with a text label, member and annotation'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"]\n' + '<<interface>> C1\n' + 'C1 : int member1'
  },
  {
    name: 'label-css-shorthand', spec: spec('should parse a class with text label and css class shorthand'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"]:::styleClass'
  },
  {
    name: 'label-css-class', spec: spec('should parse a class with text label and css class'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"]\n' + 'C1 : int member1\n' + 'cssClass "C1" styleClass'
  },
  {
    name: 'label-two-css-classes', spec: spec('should parse two classes with text labels and css classes'), text:
      'classDiagram\n' +
      'class C1["Class 1 with text label"]\n' +
      'C1 : int member1\n' +
      'class C2["Long long long long long long long long long long label"]\n' +
      'cssClass "C1,C2" styleClass'
  },
  {
    name: 'label-two-css-shorthands', spec: spec('should parse two classes with text labels and css class shorthands'), text:
      'classDiagram\n' +
      'class C1["Class 1 with text label"]:::styleClass1\n' +
      'class C2["Class 2 !@#$%^&*() label"]:::styleClass2'
  },
  {
    name: 'generic-css-class', spec: spec('should apply cssClasses to generic classes'),
    text: 'classDiagram\n' + 'class C1~T~\n' + 'cssClass "C1~T~" styleClass'
  },
  { name: 'generic-css-shorthand', spec: spec('should apply cssClasses to generic classes via shorthand'), text: 'classDiagram\n' + 'class C1~T~:::styleClass' },
  {
    name: 'generic-click-call', spec: spec('should bind click event on generic class'),
    text: 'classDiagram\n' + 'class C1~T~\n' + 'click C1~T~ call someFunction()'
  },
  {
    name: 'generic-click-tooltip', spec: spec('should bind tooltip on generic class'),
    text: 'classDiagram\n' + 'class C1~T~\n' + 'click C1~T~ call cb() "a tip"'
  },
  {
    name: 'generic-link', spec: spec('should bind link on generic class'),
    text: 'classDiagram\n' + 'class C1~T~\n' + 'link C1~T~ "https://example.com"'
  },
  {
    name: 'label-same-texts', spec: spec('should parse multiple classes with same text labels'), text: `classDiagram
class C1["Class with text label"]
class C2["Class with text label"]
class C3["Class with text label"]`
  },
  {
    name: 'label-different-texts', spec: spec('should parse classes with different text labels'), text: `classDiagram
class C1["OneWord"]
class C2["With, Comma"]
class C3["With (Brackets)"]
class C4["With [Brackets]"]
class C5["With {Brackets}"]
class C6[" "]
class C7["With 1 number"]
class C8["With . period..."]
class C9["With - dash"]
class C10["With _ underscore"]
class C11["With ' single quote"]
class C12["With ~!@#$%^&*()_+=-/?"]
class C13["With Città foreign language"]
`
  },

  // ------------------------------------------------------------------------------------------------------- notes
  { name: 'note-for', spec: spec('should handle "note for"'), text: 'classDiagram\n' + 'Class11 <|.. Class12\n' + 'note for Class11 "test"\n' },
  { name: 'note-free', spec: spec('should handle "note"'), text: 'classDiagram\n' + 'note "test"\n' },
  ...['{}', '\n', '-->'].map((k, i) => keywordCase('note-keyword-inside', k, i)),
  ...['o'].map((k, i) => keywordCase('note-keyword-start', k, i)),
  ...['function()'].map((k, i) => keywordCase('note-for-keyword-inside', k, i)),
  ...['()'].map((k, i) => keywordCase('note-for-keyword-start', k, i)),
  ...['direction', '{}', '<<', '\n'].map((k, i) => keywordCase('note-keyword-error', k, i)),

  // --------------------------------------------------------------------------------------------------- direction
  {
    name: 'direction-tb-cardinalities', spec: spec('should parse diagram with direction'), text: `classDiagram
          direction TB
          class Student {
            -idCard : IdCard
          }
          class IdCard{
            -id : int
            -name : string
          }
          class Bike{
            -id : int
            -name : string
          }
          Student "1" --o "1" IdCard : carries
          Student "1" --o "1" Bike : rides`
  },
  {
    name: 'direction-rl', spec: spec('should revert direction to default once direction is removed'), text: `classDiagram
          direction RL
          class A`
  },

  // ----------------------------------------------------------------------------------------------- class bodies
  { name: 'body-member', spec: spec('should handle member definitions'), text: 'classDiagram\n' + 'class Car{\n' + '+int wheels\n' + '}' },
  { name: 'body-method', spec: spec('should handle method definitions'), text: 'classDiagram\n' + 'class Car{\n' + '+size()\n' + '}' },
  {
    name: 'body-and-statement', spec: spec('should handle a mix of members defined in and outside of brackets'),
    text: 'classDiagram\n' + 'class Car{\n' + '+int wheels\n' + '}\n' + 'Car : +ArrayList size()\n'
  },
  {
    name: 'body-member-and-method', spec: spec('should handle member and method definitions'),
    text: 'classDiagram\n' + 'class Dummy_Class {\n' + 'String data\n' + 'void methods()\n' + '}'
  },
  {
    name: 'body-return-types', spec: spec('should handle return types on methods'), text:
      'classDiagram\n' +
      'class Flight {\n' +
      'int flightNumber\n' +
      'datetime departureTime\n' +
      'getDepartureTime() datetime\n' +
      '}'
  },
  {
    name: 'body-order', spec: spec('should add bracket members in right order'), text:
      'classDiagram\n' + 'class Class1 {\n' + 'int testMember\n' + 'test()\n' + 'string fooMember\n' + 'foo()\n' + '}'
  },
  {
    name: 'body-label-members', spec: spec('should parse a class with a text label and members'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"] {\n' + '+member1\n' + '}'
  },
  {
    name: 'body-label-members-annotation', spec: spec('should parse a class with a text label, members and annotation'),
    text: 'classDiagram\n' + 'class C1["Class 1 with text label"] {\n' + '<<interface>>\n' + '+member1\n' + '}'
  },
  { name: 'body-empty', spec: spec('should handle an empty class body with {}'), text: 'classDiagram\nclass EmptyClass {}' },

  // ---------------------------------------------------------------------------------------------------- comments
  {
    name: 'comment-start', spec: spec('should handle comments at the start'), text: `%% Comment
        classDiagram
        class Class1 {
          int : test
          string : foo
          test()
          foo()
        }`
  },
  {
    name: 'comment-end', spec: spec('should handle comments at the end'), text: `classDiagram
class Class1 {
int : test
string : foo
test()
foo()

}
%% Comment
`
  },
  {
    name: 'comment-end-no-newline', spec: spec('should handle comments at the end no trailing newline'), text: `classDiagram
class Class1 {
int : test
string : foo
test()
foo()
}
%% Comment`
  },
  {
    name: 'comment-line-feeds', spec: spec('should handle a comment with multiple line feeds'), text: `classDiagram


%% Comment

class Class1 {
int : test
string : foo
test()
foo()
}`
  },
  {
    name: 'comment-with-code', spec: spec('should handle a comment with mermaid class diagram code in them'), text: `classDiagram
%% Comment Class1 <|-- Class02
class Class1 {
int : test
string : foo
test()
foo()
}`
  },
  {
    name: 'comment-in-body', spec: spec('should handle a comment inside brackets'), text:
      'classDiagram\n' +
      'class Class1 {\n' +
      '%% Comment Class1 <|-- Class02\n' +
      'int : test\n' +
      'string : foo\n' +
      'test()\n' +
      'foo()\n' +
      '}'
  },

  // ----------------------------------------------------------------------------------- click, link and callback
  { name: 'click-href', spec: spec('should handle href link'), text: 'classDiagram\n' + 'class Class1 \n' + 'click Class1 href "google.com" ' },
  {
    name: 'click-href-tooltip', spec: spec('should handle href link with tooltip'),
    text: 'classDiagram\n' + 'class Class1 \n' + 'click Class1 href "google.com" "A Tooltip" '
  },
  {
    name: 'click-href-tooltip-target', spec: spec('should handle href link with tooltip and target'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'click Class1 href "google.com" "A tooltip" _self'
  },
  { name: 'click-call', spec: spec('should handle function call'), text: 'classDiagram\n' + 'class Class1 \n' + 'click Class1 call functionCall() ' },
  {
    name: 'click-call-tooltip', spec: spec('should handle function call with tooltip'),
    text: 'classDiagram\n' + 'class Class1 \n' + 'click Class1 call functionCall() "A Tooltip" '
  },
  {
    name: 'click-call-arguments', spec: spec('should handle function call with an arbitrary number of args'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'click Class1 call functionCall(test, test1, test2)'
  },
  {
    name: 'click-call-arguments-tooltip', spec: spec('should handle function call with an arbitrary number of args and tooltip'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'click Class1 call functionCall("test0", test1, test2) "A Tooltip"'
  },
  {
    name: 'link-css', spec: spec('should associate link and css appropriately'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'link Class1 "google.com"'
  },
  {
    name: 'link-tooltip', spec: spec('should associate link with tooltip'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'link Class1 "google.com" "A tooltip"'
  },
  {
    name: 'click-href-target', spec: spec('should associate click and href link with target appropriately'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'click Class1 href "google.com" _self'
  },
  {
    name: 'link-tooltip-target', spec: spec('should associate link appropriately'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'link Class1 "google.com" "A tooltip" _self'
  },
  {
    name: 'callback', spec: spec('should associate callback appropriately'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()\n' + 'callback Class1 "functionCall"'
  },

  // ------------------------------------------------------------------------------------------------- annotations
  { name: 'annotation-statement', spec: spec('should handle class annotations'), text: 'classDiagram\n' + 'class Class1\n' + '<<interface>> Class1' },
  {
    name: 'annotation-statement-members', spec: spec('should handle class annotations with members and methods'),
    text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : int test\n' + 'Class1 : test()\n' + '<<interface>> Class1'
  },
  { name: 'annotation-body', spec: spec('should handle class annotations in brackets'), text: 'classDiagram\n' + 'class Class1 {\n' + '<<interface>>\n' + '}' },
  {
    name: 'annotation-body-members', spec: spec('should handle class annotations in brackets with members and methods'),
    text: 'classDiagram\n' + 'class Class1 {\n' + '<<interface>>\n' + 'int : test\n' + 'test()\n' + '}'
  },
  {
    name: 'annotation-docs-separate', spec: spec('should handle the docs example: separate line annotation with members'),
    text: 'classDiagram\n' + 'class Shape\n' + '<<interface>> Shape\n' + 'Shape : noOfVertices\n' + 'Shape : draw()'
  },
  {
    name: 'annotation-docs-enumeration', spec: spec('should handle the docs example: nested annotation with enumeration'), text:
      'classDiagram\n' +
      'class Shape{\n' +
      '    <<interface>>\n' +
      '    noOfVertices\n' +
      '    draw()\n' +
      '}\n' +
      'class Color{\n' +
      '    <<enumeration>>\n' +
      '    RED\n' +
      '    BLUE\n' +
      '    GREEN\n' +
      '    WHITE\n' +
      '    BLACK\n' +
      '}'
  },
  { name: 'annotation-inline', spec: spec('should handle inline annotation syntax: class Shape <<interface>>'), text: 'classDiagram\n' + 'class Shape <<interface>>' },
  {
    name: 'annotation-inline-body', spec: spec('should handle inline annotation with members: class Shape <<interface>> { ... }'),
    text: 'classDiagram\n' + 'class Shape <<interface>> {\n' + '    noOfVertices\n' + '    draw()\n' + '}'
  },
  {
    name: 'annotation-inline-empty-body', spec: spec('should handle inline annotation with empty body: class Shape <<interface>> {}'),
    text: 'classDiagram\n' + 'class Shape <<interface>> {\n' + '}'
  },

  // ----------------------------------------------------------------------------------------- members and methods
  { name: 'member-simple', spec: spec('should handle simple member declaration'), text: 'classDiagram\n' + 'class Car\n' + 'Car : wheels' },
  { name: 'member-direct', spec: spec('should handle direct member declaration'), text: 'classDiagram\n' + 'Car : wheels' },
  { name: 'member-direct-type', spec: spec('should handle direct member declaration with type'), text: 'classDiagram\n' + 'Car : int wheels' },
  {
    name: 'member-visibility', spec: spec('should handle visibility (members)'), text:
      'classDiagram\n' +
      'class actual\n' +
      'actual : -int privateMember\n' +
      'actual : +int publicMember\n' +
      'actual : #int protectedMember\n' +
      'actual : ~int privatePackage'
  },
  { name: 'member-generic', spec: spec('should handle generic types'), text: 'classDiagram\n' + 'class Car\n' + 'Car : -List~Wheel~ wheels' },
  { name: 'method-simple', spec: spec('should handle method definition'), text: 'classDiagram\n' + 'class Car\n' + 'Car : GetSize()' },
  { name: 'method-return', spec: spec('should handle simple return types'), text: 'classDiagram\n' + 'class Object\n' + 'Object : getObject() Object' },
  { name: 'method-return-array', spec: spec('should handle return types as array'), text: 'classDiagram\n' + 'class Object\n' + 'Object : getObjects() Object[]' },
  {
    name: 'method-visibility', spec: spec('should handle visibility (methods)'), text:
      'classDiagram\n' + 'class actual\n' + 'actual : -privateMethod()\n' + 'actual : +publicMethod()\n' + 'actual : #protectedMethod()\n'
  },
  { name: 'method-abstract', spec: spec('should handle abstract methods'), text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()*' },
  { name: 'method-static', spec: spec('should handle static methods'), text: 'classDiagram\n' + 'class Class1\n' + 'Class1 : someMethod()$' },
  { name: 'method-generic-arguments', spec: spec('should handle generic types in arguments'), text: 'classDiagram\n' + 'class Car\n' + 'Car : +setWheels(List~Wheel~ wheels)' },
  { name: 'method-generic-return', spec: spec('should handle generic return types'), text: 'classDiagram\n' + 'class Car\n' + 'Car : +getWheels() List~Wheel~' },
  {
    name: 'method-generic-body', spec: spec('should handle generic types in members in class with brackets'), text:
      'classDiagram\n' + 'class Car {\n' + 'List~Wheel~ wheels\n' + 'setWheels(List~Wheel~ wheels)\n' + '+getWheels() List~Wheel~\n' + '}'
  },
  {
    name: 'types-method-parameters', spec: types('when method has no parameters / single parameter value'), text: body(
      'getTime()', '+getTime()', '-getTime()', '#getTime()', '~getTime()', 'getTime()$', 'getTime()*',
      'getTime(int)', '+getTime(int)', '-getTime(int)', '#getTime(int)', '~getTime(int)', 'getTime(int)$', 'getTime(int)*')
  },
  {
    name: 'types-method-parameter-names', spec: types('when method has single parameter type and name (type first / name first)'), text: body(
      'getTime(int count)', '+getTime(int count)', '-getTime(int count)', '#getTime(int count)', '~getTime(int count)',
      'getTime(int count)$', 'getTime(int count)*',
      'getTime(count int)', '+getTime(count int)', '-getTime(count int)', '#getTime(count int)', '~getTime(count int)',
      'getTime(count int)$', 'getTime(count int)*')
  },
  {
    name: 'types-method-multiple-return', spec: types('when method has multiple parameters / return type'), text: body(
      'getTime(string text, int count)', '+getTime(string text, int count)', '-getTime(string text, int count)',
      '#getTime(string text, int count)', '~getTime(string text, int count)', 'getTime(string text, int count)$',
      'getTime(string text, int count)*',
      'getTime() DateTime', '+getTime() DateTime', '-getTime() DateTime', '#getTime() DateTime', '~getTime() DateTime',
      'getTime() DateTime$', 'getTime()  DateTime*')
  },
  {
    name: 'types-method-generic-parameters', spec: types('when method parameter is generic / contains two generic / is a nested generic'), text: body(
      'getTimes(List~T~)', '+getTimes(List~T~)', '-getTimes(List~T~)', '#getTimes(List~T~)', '~getTimes(List~T~)',
      'getTimes(List~T~)$', 'getTimes(List~T~)*',
      'getTimes(List~T~, List~OT~)', '+getTimes(List~T~, List~OT~)', 'getTimes(List~T~, List~OT~)$',
      'getTimes(List~T~, List~OT~)*',
      'getTimetableList(List~List~T~~)', '~getTimetableList(List~List~T~~)', 'getTimetableList(List~List~T~~)$',
      'getTimetableList(List~List~T~~)*')
  },
  {
    name: 'types-method-composite-generic', spec: types('when method parameter is a composite generic'), text: body(
      'getTimes(List~K, V~)', '+getTimes(List~K, V~)', '-getTimes(List~K, V~)', '#getTimes(List~K, V~)',
      '~getTimes(List~K, V~)', 'getTimes(List~K, V~)$', 'getTimes(List~K, V~)*')
  },
  {
    name: 'types-method-generic-return', spec: types('when method return type is generic / is a nested generic'), text: body(
      'getTimes() List~T~', '+getTimes() List~T~', '-getTimes() List~T~', '#getTimes() List~T~', '~getTimes() List~T~',
      'getTimes() List~T~$', 'getTimes() List~T~*',
      'getTimetableList() List~List~T~~', '+getTimetableList() List~List~T~~', '-getTimetableList() List~List~T~~',
      'getTimetableList() List~List~T~~$', 'getTimetableList() List~List~T~~*')
  },
  {
    name: 'types-uncategorized', spec: types('member name should handle double colons / generic type'),
    text: body('std::map ~int,string~ pMap;', 'getTime~T~(this T, int seconds)$ DateTime')
  },
  {
    name: 'types-attributes', spec: types('given text representing an attribute'),
    text: body('name String', '+name String', '#name String', '-name String', '~name String', 'name String$', 'name String*')
  },

  // ----------------------------------------------------------------------------------------------------- generics
  { name: 'generic-class', spec: spec('should handle generic class'), text: 'classDiagram\n' + 'class Car~T~' },
  {
    name: 'generic-relationships', spec: spec('should handle generic class with relationships'), text:
      'classDiagram\n' + 'class Car~T~\n' + 'Driver -- Car : drives >\n' + 'Car *-- Wheel : have 4 >\n' + 'Car -- Person : < owns'
  },
  {
    name: 'generic-literal-name', spec: spec('should handle generic class with a literal name'), text:
      'classDiagram\n' + 'class `Car`~T~\n' + 'Driver -- `Car` : drives >\n' + '`Car` *-- Wheel : have 4 >\n' + '`Car` -- Person : < owns'
  },
  {
    name: 'generic-body', spec: spec('should handle generic class with brackets'), text:
      'classDiagram\n' +
      'class Dummy_Class~T~ {\n' +
      'String data\n' +
      'void methods()\n' +
      '}\n' +
      '\n' +
      'class Flight {\n' +
      'Integer flightNumber\n' +
      'Date departureTime\n' +
      '}'
  },
  {
    name: 'generic-body-literal-name', spec: spec('should handle generic class with brackets and a literal name'), text:
      'classDiagram\n' +
      'class `Dummy_Class`~T~ {\n' +
      'String data\n' +
      '  void methods()\n' +
      '}\n' +
      '\n' +
      'class Flight {\n' +
      '   flightNumber : Integer\n' +
      '   departureTime : Date\n' +
      '}'
  },
  {
    name: 'ns-several', spec: spec('should handle "namespace"'), text: `classDiagram
namespace Namespace1 { class Class1 }
namespace Namespace2 { class Class1
}
namespace Namespace3 {
class Class1 {
int : test
string : foo
test()
foo()
}
}
namespace Namespace4 {
class Class1 {
int : test
string : foo
test()
foo()
}
class Class2 {
int : test
string : foo
test()
foo()
}
}
`
  },
  {
    name: 'ns-generic-types', spec: spec('should handle namespace with generic types'), text: `classDiagram

namespace space {
    class Square~Shape~{
        int id
        List~int~ position
        setPoints(List~int~ points)
        getPoints() List~int~
    }
}`
  },

  // ------------------------------------------------------------------------------------------------ relationships
  {
    name: 'relation-basic', spec: spec('should handle all basic relationships'), text:
      'classDiagram\n' + 'Class1 <|-- Class02\n' + 'Class03 *-- Class04\n' + 'Class05 o-- Class06\n' + 'Class07 .. Class08\n' + 'Class09 -- Class1'
  },
  {
    name: 'relation-backticks', spec: spec('should handle backticked class name'), text:
      'classDiagram\n' + '`Class1` <|-- Class02\n' + 'Class03 *-- Class04\n' + 'Class05 o-- Class06\n' + 'Class07 .. Class08\n' + 'Class09 -- Class1'
  },
  { name: 'relation-generics', spec: spec('should handle generics'), text: 'classDiagram\n' + 'Class1~T~ <|-- Class02' },
  {
    name: 'relation-labels', spec: spec('should handle relationships with labels'), text:
      'classDiagram\n' + 'class Car\n' + 'Driver -- Car : drives >\n' + 'Car *-- Wheel : have 4 >\n' + 'Car -- Person : < owns'
  },
  {
    name: 'relation-types-directions', spec: spec('should handle relation definition of different types and directions'), text:
      'classDiagram\n' + 'Class11 <|.. Class12\n' + 'Class13 --> Class14\n' + 'Class15 ..> Class16\n' + 'Class17 ..|> Class18\n' + 'Class19 <--* Class20'
  },
  {
    name: 'relation-cardinality', spec: spec('should handle cardinality and labels'), text:
      'classDiagram\n' + 'Class1 "1" *-- "many" Class02 : contains\n' + 'Class03 o-- Class04 : aggregation\n' + 'Class05 --> "1" Class06'
  },
  {
    name: 'relation-dashed', spec: spec('should handle dashed relation definition of different types and directions'), text:
      'classDiagram\n' + 'Class11 <|.. Class12\n' + 'Class13 <.. Class14\n' + 'Class15 ..|> Class16\n' + 'Class17 ..> Class18\n' + 'Class19 .. Class20'
  },
  { name: 'relation-aggregation-dotted', spec: spec('should handle relation definitions AGGREGATION and dotted line'), text: 'classDiagram\n' + 'Class1 o.. Class02' },
  { name: 'relation-composition-both', spec: spec('should handle relation definitions COMPOSITION on both sides'), text: 'classDiagram\n' + 'Class1 *--* Class02' },
  { name: 'relation-no-types', spec: spec('should handle relation definitions with no types'), text: 'classDiagram\n' + 'Class1 -- Class02' },
  { name: 'relation-right-only', spec: spec('should handle relation definitions with type only on right side'), text: 'classDiagram\n' + 'Class1 --|> Class02' },
  {
    name: 'relation-multiple', spec: spec('should handle multiple classes and relation definitions'), text:
      'classDiagram\n' + 'Class1 <|-- Class02\n' + 'Class03 *-- Class04\n' + 'Class05 o-- Class06\n' + 'Class07 .. Class08\n' + 'Class09 -- Class10'
  },
  {
    name: 'body-order-colons', spec: spec('should add bracket members in right order (int : test)'), text:
      'classDiagram\n' + 'class Class1 {\n' + 'int : test\n' + 'string : foo\n' + 'test()\n' + 'foo()\n' + '}'
  },
  {
    name: 'ns-classes', spec: spec('should add classes namespaces'), text: `classDiagram
namespace Namespace1 {
class Class1 {
int : test
string : foo
test()
foo()
}
class Class2
}`
  },
  {
    name: 'ns-relations-between', spec: spec('should add relations between classes of different namespaces'), text: `classDiagram
      A1 --> B1
      namespace A {
        class A1 {
          +foo : string
        }
        class A2 {
          +bar : int
        }
      }
      namespace B {
        class B1 {
          +foo : bool
        }
        class B2 {
          +bar : float
        }
      }
      A2 --> B2`
  },
  {
    name: 'label-relation', spec: spec('should parse a class with a text label (with a relation)'), text: `classDiagram
  class C1["Class 1 with text label"]
  C1 -->  C2
      `
  },
  {
    name: 'label-members-annotation-relation', spec: spec('should parse a class with a text label, members and annotation (with a relation)'), text: `classDiagram
  class C1["Class 1 with text label"] {
    <<interface>>
    +member1
  }
  C1 -->  C2
      `
  },
  {
    name: 'label-two-shorthands-body', spec: spec('should parse two classes with text labels and css class shorthands (with a body)'), text: `classDiagram
class C1["Class 1 with text label"]:::styleClass1 {
  +member1
}
class C2["Class 2 !@#$%^&*() label"]:::styleClass2
C1 --> C2
  `
  },
  {
    name: 'label-css-class-relation', spec: spec('should parse a class with text label and css class (with a relation)'), text: `classDiagram
class C1["Class 1 with text label"] {
  +member1
}
C1 --> C2
cssClass "C1" styleClass
  `
  },

  // ------------------------------------------------------------------------------------------------------ styles
  { name: 'css-direct', spec: styles('should be possible to apply a css class to a class directly'), text: 'classDiagram\n' + 'class Class01:::exClass' },
  {
    name: 'css-direct-body', spec: styles('should be possible to apply a css class to a class directly with struct'), text:
      'classDiagram\n' + 'class Class1:::exClass {\n' + 'int : test\n' + 'string : foo\n' + 'test()\n' + 'foo()\n' + '}'
  },
  {
    name: 'css-relations', spec: styles('should be possible to apply a css class to a class with relations'),
    text: 'classDiagram\n' + 'Class01 <|-- Class02\ncssClass "Class01" exClass'
  },
  { name: 'css-statement', spec: styles('should be possible to apply a cssClass to a class'), text: 'classDiagram\n' + 'class Class01\n cssClass "Class01" exClass' },
  {
    name: 'css-list', spec: styles('should be possible to apply a cssClass to a comma separated list of classes'),
    text: 'classDiagram\n' + 'class Class01\n class Class02\n cssClass "Class01,Class02" exClass'
  },
  {
    name: 'style-node', spec: styles('should be possible to apply a style to an individual node'),
    text: 'classDiagram\n' + 'class Class01\n class Class02\n style Class01 fill:#f9f,stroke:#333,stroke-width:4px'
  },
  {
    name: 'classdef-assign', spec: styles('should be possible to define and assign a class inside the diagram'),
    text: 'classDiagram\n' + 'class Class01\n cssClass "Class01" pink\n classDef pink fill:#f9f'
  },
  {
    name: 'classdef-shorthand', spec: styles('should be possible to define and assign a class using shorthand inside the diagram'),
    text: 'classDiagram\n' + 'class Class01:::pink\n classDef pink fill:#f9f'
  },
  {
    name: 'classdef-styles', spec: styles('should properly assign styles from a class defined inside the diagram'),
    text: 'classDiagram\n' + 'class Class01:::pink\n classDef pink fill:#f9f,stroke:#333,stroke-width:6px'
  },
  {
    name: 'classdef-multiple', spec: styles('should properly assign multiple classes and styles from classes defined inside the diagram'),
    text: 'classDiagram\n' + 'class Class01:::pink\n cssClass "Class01" bold\n classDef pink fill:#f9f\n classDef bold stroke:#333,stroke-width:6px'
  },

  // ------------------------------------------------------------------------------------------- parser/class.spec.js
  { name: 'prototype-class', spec: parserSpec('should work with a __proto__ property'), text: 'classDiagram\nclass __proto__' },
  { name: 'prototype-namespace', spec: parserSpec('should work with a constructor property'), text: 'classDiagram\nnamespace constructor {\n\tclass A\n}' },
  { name: 'backtick-namespace', spec: parserSpec('should handle backtick-quoted namespace names'), text: 'classDiagram\nnamespace `A::B` {\n\tclass `IPC::Sender`\n}' },

  // ------------------------------------------------------------------------------------- original: not in the specs
  { name: 'lollipop-forms', spec: 'original', text: lines('classDiagram', 'Ignition ()-- Engine', 'Engine --() Runnable', 'Left ()--() Right', 'Cache ()..() Store') },
  {
    name: 'relation-two-way', spec: 'original',
    text: lines('classDiagram', 'Alpha <|--|> Beta', 'Gamma <--> Delta', 'Eta <..> Theta', 'Iota o--o Kappa', 'Lambda *--o Mu', 'Nu <|..|> Xi')
  },
  { name: 'header-v2', spec: 'original', text: lines('classDiagram-v2', 'Alpha --> Beta') },
  { name: 'ids-starting-with-o', spec: 'original', text: lines('classDiagram', 'class object', 'order --> object', 'owner o-- other', 'item --o order') },
  { name: 'relation-label-colon', spec: 'original', text: lines('classDiagram', 'Alpha --> Beta : ratio a:b', 'Beta --> Gamma : semi; colon') },
  { name: 'statement-semicolons', spec: 'original', text: 'classDiagram\nAlpha --> Beta;\nclass Gamma;' },
  { name: 'note-in-namespace', spec: 'original', text: lines('classDiagram', 'namespace Area {', '  class Inside', '  note for Inside "kept"', '}', 'note for Missing "orphan"') },
  { name: 'note-target-as-written', spec: 'original', text: lines('classDiagram', 'class Box~T~', 'class `My Class`', 'note for Box~T~ "unlinked"', 'note for Box "linked"', 'note for `My Class` "linked"') },
  { name: 'relation-ends-class-ids', spec: 'original', text: lines('classDiagram', 'Box~T~ --> Other', 'Box --> Other2', '`My Class` --> B') },
  { name: 'css-digit-ids', spec: 'original', text: lines('classDiagram', 'class 1Alpha', 'class Beta', 'cssClass "1Alpha,Beta" hot', 'link 1Alpha "https://example.com"') },
  { name: 'classdef-color-text-styles', spec: 'original', text: lines('classDiagram', 'class Alpha:::warm', 'classDef warm fill:#f96,color:#036,stroke:#333') },
  { name: 'classdef-before-use', spec: 'original', text: lines('classDiagram', 'classDef warm fill:#f96', 'class Alpha:::warm', 'class Beta', 'cssClass "Beta" warm') },
  { name: 'classdef-default', spec: 'original', text: lines('classDiagram', 'class Alpha', 'class Beta', 'classDef default fill:#eee,stroke:#999') },
  { name: 'style-before-class', spec: 'original', text: lines('classDiagram', 'style Alpha fill:#f96', 'class Alpha') },
  { name: 'direction-repeated', spec: 'original', text: lines('classDiagram', 'direction LR', 'class Alpha', 'direction BT %% the last one wins') },
  { name: 'member-annotation-statement', spec: 'original', text: lines('classDiagram', 'Alpha : <<service>>', 'Alpha : <<entity>>', 'Alpha : +run()') },
  { name: 'generic-nested-class-name', spec: 'original', text: lines('classDiagram', 'class Box~List~int~~') },
  { name: 'padding-site', spec: 'original', site: { class: { padding: 5 } }, text: lines('classDiagram', 'namespace Area {', '  class Inside', '}', 'note "padded"') },
  { name: 'error-class-without-name', spec: 'original', text: lines('classDiagram', 'class') },
  { name: 'error-relation-without-target', spec: 'original', text: lines('classDiagram', 'Alpha -->') },
  { name: 'error-unclosed-body', spec: 'original', text: lines('classDiagram', 'class Alpha {', '  +int id') },
  { name: 'error-annotation-unknown-class', spec: 'original', text: lines('classDiagram', '<<interface>> Missing') },
  { name: 'title-statement-ignored', spec: 'original', text: lines('classDiagram', 'title A title', 'class Alpha') },

  // ------------------------------------------------------------------------------------------ the class fixtures
  ...fixtures
];
