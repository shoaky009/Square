using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Square.Compiler;
using Square.Runtime.Binding;
using Xunit;

namespace Square.Compiler.Tests;

public class VueGeneratorTests
{
    [Fact]
    public void SqvFileGeneratesComponent()
    {
        const string source = """
            <template>
              <View>
                <Text>{{ Title }}</Text>
              </View>
            </template>
            <script lang="csharp">
              public ObservableValue<string> Title = new("Hello");
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("VueCard.sqv", source));

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedTrees, tree => tree.FilePath.Contains("VueCard", StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, tree => tree.GetText().ToString().Contains("partial class VueCard", StringComparison.Ordinal));
    }

    [Fact]
    public void SqvBindingsAndEventsUseExistingEmitterSemantics()
    {
        const string source = """
            <template>
              <View>
                <Text :text="Title" />
                <Button @click="OnClick">Save</Button>
              </View>
            </template>
            <script lang="csharp">
              public ObservableValue<string> Title = new("Hello");
              private void OnClick() { }
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("Bindings.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains(".BindProperty(\"TextContent\", Title);", generated);
        Assert.Contains("AddEventListener", generated);
        Assert.Contains("\"click\"", generated);
        Assert.Contains("OnClick", generated);
    }

    [Fact]
    public void SqvLowercaseViewTextLowersToBuiltInControls()
    {
        const string source = """
            <template>
              <view style="user-select: text">hello</view>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("LowercaseView.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.View()", generated);
        Assert.Contains("new Square.Controls.Text(\"hello\")", generated);
        Assert.Contains(".Children.Add", generated);
        Assert.DoesNotContain("new view", generated);
    }

    [Fact]
    public void SqvScrollViewerLowersToBuiltInControl()
    {
        const string source = """
            <template>
              <ScrollViewer>
                <Text>Scrollable</Text>
              </ScrollViewer>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("Scroller.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.ScrollViewer()", generated);
        Assert.DoesNotContain("new ScrollViewer", generated);
    }

    [Fact]
    public void SqvPopupLowersToBuiltInControl()
    {
        const string source = """
            <template>
              <Popup>
                <Text>Floating</Text>
              </Popup>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("PopupCard.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.Popup()", generated);
        Assert.DoesNotContain("new Popup", generated);
    }

    [Fact]
    public void SqvDialogLowersToBuiltInControl()
    {
        const string source = """
            <template>
              <Dialog>
                <Button>Close</Button>
              </Dialog>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("DialogCard.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.Dialog()", generated);
        Assert.DoesNotContain("new Dialog", generated);
    }

    [Fact]
    public void SqvMenuTreeLowersToBuiltInControlsAndProperties()
    {
        const string source = """
            <template>
              <MenuBar>
                <MenuItem text="View">
                  <Menu>
                    <MenuItem text="Grid" checkable="true" shortcut="Ctrl+G" stays-open-on-click="true" />
                    <MenuSeparator />
                    <MenuItem text="Dark" group="theme" />
                  </Menu>
                </MenuItem>
              </MenuBar>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("Menus.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.MenuBar()", generated);
        Assert.Contains("new Square.Controls.MenuItem()", generated);
        Assert.Contains("new Square.Controls.Menu()", generated);
        Assert.Contains("new Square.Controls.MenuSeparator()", generated);
        Assert.Contains("SetProperty(\"IsCheckable\", true)", generated);
        Assert.Contains("SetProperty(\"ShortcutText\", \"Ctrl+G\")", generated);
        Assert.Contains("SetProperty(\"StaysOpenOnClick\", true)", generated);
        Assert.Contains("SetProperty(\"GroupName\", \"theme\")", generated);
    }

    [Fact]
    public void SqvVIfLowersToShowDirective()
    {
        const string source = """
            <template>
              <View>
                <Text v-if="Visible">Visible</Text>
              </View>
            </template>
            <script lang="csharp">
              public ObservableValue<bool> Visible = new(true);
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("Conditional.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new ShowNode(Visible", generated);
    }

    [Fact]
    public void SqvTemplateSlotSyntaxLowersToSquareSlots()
    {
        const string source = """
            <template>
              <Card>
                <template v-slot:header>
                  <Text>Header</Text>
                </template>
                <Text>Body</Text>
              </Card>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("SlotUsage.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains(".Slots.Set(\"header\"", generated);
        Assert.Contains(".Slots.Set(\"\"", generated);
        Assert.DoesNotContain("new template", generated);
        Assert.DoesNotContain("new Fragment", generated);
    }

    [Fact]
    public void SqvInputVModelBindsValueAndWritesBackOnInput()
    {
        const string source = """
            <template>
              <Input type="password" v-model="Password" />
            </template>
            <script lang="csharp">
              public ObservableValue<string> Password = new("square123");
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("PasswordModel.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains(".BindProperty(\"Value\", Password);", generated);
        Assert.Contains(".AddEventListener(\"input\", e => Password.Value = ((Square.Controls.Input)e.Target!).Value);", generated);
    }

    [Fact]
    public void SqvVModelModifiersAffectTextInputWriteBack()
    {
        const string source = """
            <template>
              <View>
                <Input v-model.trim.lazy="Name" />
                <Input v-model.number="Age" />
              </View>
            </template>
            <script lang="csharp">
              public ObservableValue<string> Name = new("Ada");
              public ObservableValue<double> Age = new(12);
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("ModelModifiers.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains(".AddEventListener(\"change\", e => Name.Value = ((Square.Controls.Input)e.Target!).Value.Trim());", generated);
        Assert.Contains(".AddEventListener(\"input\", e => Age.Value = double.Parse(((Square.Controls.Input)e.Target!).Value, System.Globalization.CultureInfo.InvariantCulture));", generated);
    }

    [Fact]
    public void SqvControlVModelUsesControlSpecificPropertyAndEvent()
    {
        const string source = """
            <template>
              <View>
                <CheckBox v-model="RememberMe" />
                <Select v-model="Plan" />
              </View>
            </template>
            <script lang="csharp">
              public ObservableValue<bool> RememberMe = new(true);
              public ObservableValue<string> Plan = new("Pro");
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("ControlModel.sqv", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains(".BindProperty(\"IsChecked\", RememberMe);", generated);
        Assert.Contains(".AddEventListener(\"change\", e => RememberMe.Value = ((Square.Controls.CheckBox)e.Target!).IsChecked);", generated);
        Assert.Contains(".BindProperty(\"Value\", Plan);", generated);
        Assert.Contains(".AddEventListener(\"change\", e => Plan.Value = ((Square.Controls.Select)e.Target!).Value);", generated);
    }

    [Fact]
    public void GeneratedCleanupCoexistsWithUserDetachHook()
    {
        const string source = """
            <template><Button ref={SaveButton}>Save</Button></template>
            <script lang="csharp">
              protected override void OnDetachedCore() { }
            </script>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("Cleanup.sqx", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("protected override void OnGeneratedDetachedCore()", generated);
        Assert.Contains("SaveButton = null!;", generated);
        Assert.Contains("protected override void OnDetachedCore()", generated);
    }

    [Theory]
    [InlineData("CodeBehind.sqx")]
    [InlineData("CodeBehind.sqv")]
    public void CodeBehindPartialCompilesWithEventsAndRefs(string path)
    {
        var isVue = path.EndsWith(".sqv", StringComparison.OrdinalIgnoreCase);
        var eventAttribute = isVue
            ? "@click=\"OnClick\""
            : "onClick={OnClick}";
        var refAttribute = isVue ? "ref=\"SaveButton\"" : "ref={SaveButton}";
        var template =
            "<template><Button " + refAttribute + " " + eventAttribute + ">Save</Button></template>" +
            "<script namespace=\"TestApp\"></script>";
        const string codeBehind = """
            namespace TestApp;
            public partial class CodeBehind
            {
                private void OnClick(Square.Events.Event e)
                {
                    SaveButton.TextContent = "Saved";
                }
            }
            """;

        var compilation = CreateCompilation(codeBehind);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SqxGenerator().AsSourceGenerator()],
            [new InMemoryAdditionalText(path, template)],
            (CSharpParseOptions?)compilation.SyntaxTrees.First().Options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        Assert.DoesNotContain(generatorDiagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void TitleBarLowersToBuiltInControl()
    {
        const string source = """
            <template>
              <TitleBar>
                <Text slot="icon" text="I" />
                <Text text="App" />
                <Button slot="control" text="X" />
              </TitleBar>
            </template>
            """;

        var result = RunGenerator(new InMemoryAdditionalText("WindowTitle.sqx", source));
        var generated = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Contains("new Square.Controls.TitleBar()", generated);
        Assert.DoesNotContain("new TitleBar()", generated);
        Assert.Contains(".Slots.Set(\"icon\"", generated);
        Assert.Contains(".Slots.Set(\"\"", generated);
        Assert.Contains(".Slots.Set(\"control\"", generated);
        Assert.True(
            generated.IndexOf(".Children.Add(", StringComparison.Ordinal) <
            generated.LastIndexOf(".BuildElementTree();", StringComparison.Ordinal));
    }

    private static GeneratorDriverRunResult RunGenerator(params AdditionalText[] files)
    {
        var compilation = CreateCompilation("public sealed class Placeholder { }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SqxGenerator().AsSourceGenerator()],
            files,
            (CSharpParseOptions?)compilation.SyntaxTrees.First().Options);
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (references.All(reference => reference.Display != typeof(PropAttribute).Assembly.Location))
            references.Add(MetadataReference.CreateFromFile(typeof(PropAttribute).Assembly.Location));
        return CSharpCompilation.Create(
            "VueGeneratorTests",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(content, Encoding.UTF8);
    }
}
