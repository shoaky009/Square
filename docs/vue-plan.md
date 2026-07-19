# SQV / Vue Template Compatibility Plan

> Status: draft  
> Scope: add `.sqv` as a Vue 3 template syntax frontend for Square while preserving existing `.sqx`.

## Goals

- Keep `.sqx` unchanged as Square's native template language.
- Add `.sqv` as a new file format that targets Vue 3 template syntax compatibility.
- Add a dedicated Vue parser instead of mutating the existing SQX parser into a Vue parser.
- Refactor `Square.SourceGenerator` so it can compile both `.sqx` and `.sqv` through a shared intermediate representation.
- Preserve Square's compile-first model: no Vue runtime, no JavaScript runtime, no runtime template parsing.
- Keep NativeAOT compatibility and strong C# diagnostics as first-class constraints.

## Non-Goals

- Do not remove or deprecate `.sqx` syntax.
- Do not execute JavaScript expressions in templates.
- Do not embed Vue runtime behavior into Square.
- Do not silently ignore unsupported Vue features. Parse them and report explicit diagnostics when generation is not supported.

## Compatibility Definition

`.sqv` aims to support the Vue 3 template syntax surface. Expressions inside Vue syntax are interpreted as C# expression text and compiled by the Source Generator into C# code.

Example:

```vue
<template>
  <View class="page">
    <Text>{{ Title.Value }}</Text>

    <Input
      :value="Name"
      @input="OnNameChanged" />

    <Button ref="SaveButton" @click.stop.prevent="OnSave">
      Save
    </Button>

    <Text v-if="Saved.Value">Saved</Text>

    <Text v-for="item in Items" :key="item.Id">
      {{ item.Name }}
    </Text>
  </View>
</template>

<script lang="csharp">
  public ObservableValue<string> Title = new("Hello Square");
  public ObservableValue<string> Name = new("");
  public ObservableValue<bool> Saved = new(false);
</script>

<style>
  .page { padding: 16px; }
</style>
```

## File Format

`.sqv` keeps the same top-level section model as `.sqx`:

- One required `<template>` section.
- At most one `<script lang="csharp">` section.
- At most one `<style>` section.
- Component metadata remains on the `<script>` tag: `namespace`, `name`, `access`.

This keeps the generator, metadata, style pipeline, and component naming model consistent across `.sqx` and `.sqv`.

## Architecture

Current issue:

- `Square.Markup` contains SQX lexer/parser/AST.
- `Square.SourceGenerator` also contains a copied SQX lexer/parser/AST.
- Documentation says SourceGenerator should use Markup, but the project currently does not reference it.

Target pipeline:

```text
.sqx -> SQX parser -> SQX-to-IR adapter -> Template IR
.sqv -> Vue parser -> Vue-to-IR adapter -> Template IR
Template IR -> semantic validation -> C# emitter -> generated component
```

### Markup Package

`Square.Markup` should own both frontends and the shared IR:

- `Square.Markup.Sqx`: existing SQX frontend.
- `Square.Markup.Vue`: new Vue template frontend.
- `Square.Markup.Compilation`: shared template IR and source span types.

`Square.SourceGenerator` should consume the shared IR and should not own duplicated parsers.

Because `Square.SourceGenerator` targets `netstandard2.0`, `Square.Markup` should become `netstandard2.0` compatible or multi-target `netstandard2.0;net10.0`.

### Shared IR

The IR should represent Square-compilable template structure independently of the source syntax.

Suggested model:

```csharp
public sealed class TemplateCompilationUnit
{
    public string Name { get; init; }
    public string? Namespace { get; init; }
    public string Access { get; init; }
    public TemplateLanguage Language { get; init; }
    public TemplateDocument Template { get; init; }
    public string? ScriptCode { get; init; }
    public string? StyleCode { get; init; }
}

public enum TemplateLanguage
{
    Sqx,
    Sqv
}
```

Node concepts:

- Element.
- Text.
- Interpolation.
- Static attribute.
- Bound attribute.
- Event binding.
- Directive node.
- Conditional chain.
- For block.
- Slot outlet.
- Slot content group.
- Comment, usually non-emitting but span-preserving.
- Source span for diagnostics.

## Vue Syntax Surface

The `.sqv` parser should recognize and preserve the following Vue 3 syntax forms.

### Text And Interpolation

- Plain text.
- HTML entities where applicable.
- `{{ expression }}` interpolation.
- Comments: `<!-- ... -->`.

### Attributes And Bindings

- Static attributes: `name="value"`, `disabled`.
- Dynamic bindings: `:prop="expr"`, `v-bind:prop="expr"`.
- Object binding: `v-bind="expr"`.
- Dynamic arguments: `:[name]="expr"`, `v-bind:[name]="expr"`.
- Binding modifiers: `.camel`, `.prop`, `.attr`.

### Events

- Event shorthand: `@click="handler"`.
- Full event form: `v-on:click="handler"`.
- Object event binding: `v-on="expr"`.
- Dynamic event names: `@[event]="handler"`, `v-on:[event]="handler"`.
- Event modifiers: `.stop`, `.prevent`, `.capture`, `.self`, `.once`, `.passive`, `.exact`, key and mouse button modifiers.

### Control Flow

- `v-if="condition"`.
- `v-else-if="condition"`.
- `v-else`.
- `v-for="item in items"`.
- `v-for="(item, index) in items"`.
- `v-for="(value, key, index) in object"`.
- `:key="expr"`.

### Slots

- `<slot>` outlet.
- `<slot name="header">` outlet.
- `v-slot`.
- `v-slot:name`.
- `#name` shorthand.
- Dynamic slot name: `#[name]`.
- Scoped slot props, parsed and represented even if generation is initially limited.

### Special Directives And Built-Ins

- `v-model` and modifiers `.trim`, `.number`, `.lazy`.
- `v-text`.
- `v-html`.
- `v-pre`.
- `v-once`.
- `v-memo`.
- `v-cloak`.
- `<template>` as a structural container.
- `<component :is="...">`.
- `<Teleport>`.
- `<Transition>`.
- `<TransitionGroup>`.
- `<KeepAlive>`.
- `<Suspense>`.

## Generation Semantics

Supported in the first generation pass:

- `{{ expr }}` -> Square text interpolation / text binding.
- `:prop="expr"` and `v-bind:prop="expr"` -> `BindProperty` or direct property set when expression is local loop state.
- `@click="OnClick"` and `v-on:click="OnClick"` -> `AddEventListener("click", OnClick)`.
- `.stop` and `.prevent` event modifiers -> generated event wrapper that calls `StopPropagation()` and `PreventDefault()` before the handler.
- Static `class` and `style` -> existing class/style emission.
- String-valued dynamic `class` -> bound class support, if runtime supports it; otherwise diagnostic.
- `v-text="expr"` -> bind or set `TextContent`.
- `v-if` / `v-else-if` / `v-else` -> conditional chain.
- `v-for` over collections -> `ForNode.Create`.
- `ref="Name"` -> generated ref field.
- `<slot>` / basic named slots -> existing Square slot model.

Parse but report explicit diagnostics initially:

- `v-html`, because Square is not an HTML DOM.
- `v-bind="object"` until an object-to-property binding protocol exists.
- `v-on="object"` until an object-to-event binding protocol exists.
- Dynamic arguments such as `:[name]` and `@[event]`.
- `<component :is="...">` until an AOT-safe dynamic component factory model exists.
- `<Teleport>` until Square has a portal/layer target model.
- `<Transition>` and `<TransitionGroup>` until animation integration exists.
- `<KeepAlive>` until component instance caching semantics exist.
- `<Suspense>` until async component semantics exist.
- Scoped slot props until slot-prop runtime support exists.

## Conditional Chains

Vue conditional chains must preserve mutual exclusion:

```vue
<Text v-if="State.Value == 0">A</Text>
<Text v-else-if="State.Value == 1">B</Text>
<Text v-else>C</Text>
```

IR:

```text
ConditionalChain
  Branch condition: State.Value == 0
    Text
  Branch condition: State.Value == 1
    Text
  Else
    Text
```

Generation should use `SwitchNode`/`Match` if the current runtime model can express this correctly. If not, add a dedicated conditional-chain runtime node instead of lowering each branch into independent `ShowNode` instances.

## Loops

Vue loop syntax:

```vue
<Text v-for="item in Items" :key="item.Id">
  {{ item.Name }}
</Text>

<Text v-for="(item, index) in Items">
  {{ index }} {{ item.Name }}
</Text>
```

IR:

```text
ForBlock
  Source: Items
  ItemName: item
  IndexName: index?
  KeyExpression: item.Id?
  Children: original element without v-for
```

Generation:

```csharp
_for0 = ForNode.Create(Items, item =>
{
    ...
});
```

If index and key are supported by runtime overloads, generate those overloads. If not, parser should still preserve them and SourceGenerator should report actionable diagnostics.

## v-model

`v-model` should be parsed in the first Vue parser milestone. Generation can be implemented in a follow-up milestone.

Suggested mappings:

| Vue syntax | Square meaning |
|---|---|
| `<Input v-model="Name" />` | bind `Value` and update on input |
| `<CheckBox v-model="Checked" />` | bind `IsChecked` and update on change |
| `<Select v-model="Selected" />` | bind `Value` and update on change |

Modifiers:

- `.trim` trims string before write-back.
- `.number` parses numeric values before write-back.
- `.lazy` uses commit/change events instead of immediate input events.

## Slots

Basic Vue slots should map to Square slots.

```vue
<Card>
  <template #header>
    <Text>Header</Text>
  </template>

  <Text>Default content</Text>
</Card>
```

Normalized groups:

```text
Card
  Slot "header": Text
  Slot "": Text
```

Scoped slot props should be parsed into IR but may initially report a diagnostic until the runtime supports slot prop passing.

## Diagnostics

Suggested `.sqv` diagnostics:

| ID | Meaning |
|---|---|
| `SQV0001` | Vue template syntax error |
| `SQV0002` | Unsupported Vue directive generation |
| `SQV0003` | Invalid `v-for` expression |
| `SQV0004` | `v-else` / `v-else-if` without a preceding `v-if` chain |
| `SQV0005` | Duplicate binding for the same property/event |
| `SQV0006` | Dynamic argument generation is not supported yet |
| `SQV0007` | Unsupported Vue built-in component generation |
| `SQV0008` | Scoped slot props are not supported yet |
| `SQV0009` | Template expression must be a C# expression |

General section and component diagnostics can continue to use existing SQX diagnostics where appropriate, or can later be moved to language-neutral IDs.

## Implementation Milestones

### Milestone A: Shared IR And Parser Ownership

- Make `Square.Markup` consumable by `Square.SourceGenerator`.
- Add shared template IR and source span model.
- Add SQX-to-IR adapter.
- Change SourceGenerator emitter and validators to consume IR.
- Keep `.sqx` tests passing.
- Stop adding new behavior to the duplicated SourceGenerator parser.

### Milestone B: `.sqv` Section Parser And Vue AST

- Add `.sqv` file discovery in SourceGenerator.
- Add Vue section parser for `<template>`, `<script>`, and `<style>`.
- Add Vue template lexer/parser.
- Preserve source spans for diagnostics.
- Parse full Vue 3 template syntax surface into Vue AST.
- Add parser tests for all recognized syntax categories.

### Milestone C: Basic Vue-To-IR Lowering

- Lower interpolation, static attributes, `v-bind`, `:prop`, `v-on`, `@event`, static class, and static style.
- Generate working components from simple `.sqv` files.
- Add SourceGenerator tests for property binding, text interpolation, and event handling.

### Milestone D: Control Flow

- Lower `v-if`, `v-else-if`, and `v-else` into conditional-chain IR.
- Lower `v-for` into for-block IR.
- Support item variables, index variables, and key expressions where runtime supports them.
- Add diagnostics where runtime support is missing.

### Milestone E: Slots And Modifiers

- Lower `<slot>`, `v-slot`, and `#name` into Square slot IR.
- Support `.stop` and `.prevent` event modifiers.
- Parse all event modifiers and diagnose unsupported ones.
- Add tests for slot grouping and event wrapper generation.

### Milestone F: v-model And Runtime Gaps

- Add `v-model` generation for `Input`, `CheckBox`, and `Select`.
- Add runtime APIs needed for index/key loop generation if missing.
- Evaluate dynamic component and transition support after core `.sqv` is stable.

### Milestone G: Docs, Samples, Cleanup

- Add `docs/Sqv-Spec.md`.
- Update `README.md`, `docs/Architecture.md`, `docs/Generator.md`, and getting started docs.
- Add `.sqv` sample components.
- Remove or quarantine the old duplicated SourceGenerator parser after IR migration is complete.

## Test Plan

Markup tests:

- Parse `{{ Name }}`.
- Parse `:text="Title"`.
- Parse `v-bind:text="Title"`.
- Parse `@click="OnClick"`.
- Parse `v-on:click="OnClick"`.
- Parse event modifiers.
- Parse `v-if` / `v-else-if` / `v-else` chains.
- Parse `v-for="item in Items"`.
- Parse `v-for="(item, index) in Items"`.
- Parse `:key="item.Id"`.
- Parse `v-slot` and `#header`.
- Parse dynamic arguments.
- Parse Vue built-ins without crashing.
- Preserve line and column spans.

SourceGenerator tests:

- `.sqv` file generates a component.
- Interpolation emits text binding.
- `:prop` emits property binding.
- `@event` emits event binding.
- `.stop.prevent` emits an event wrapper.
- `v-if` emits mutually exclusive conditional behavior.
- `v-for` emits loop behavior.
- Required Prop validation works with Vue binding syntax.
- `.sqx` and `.sqv` components can reference each other by component name.
- Existing `.sqx` tests still pass unchanged.

Commands:

```powershell
dotnet test tests/Square.Markup.Tests/Square.Markup.Tests.csproj
dotnet test tests/Square.SourceGenerator.Tests/Square.SourceGenerator.Tests.csproj
dotnet test
```

## Open Design Questions

- Should `.sqv` expressions require explicit `.Value` for `ObservableValue<T>`, or should the generator preserve the current SQX shorthand behavior where possible?
- Should unsupported Vue built-ins be hard errors or warnings during the parser-complete milestone?
- Should `Square.Markup` target only `netstandard2.0`, or multi-target `netstandard2.0;net10.0`?
- Should conditional chains reuse `SwitchNode`, or should a dedicated `ConditionalNode` be added?
- Should `v-model` be part of the first generation milestone or a separate runtime milestone?
