using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Square.SourceGenerator.Generator;
using Xunit;

namespace Square.SourceGenerator.Tests;

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

        Assert.Contains("new Square.Controls.Controls.View()", generated);
        Assert.Contains("new Square.Controls.Controls.Text(\"hello\")", generated);
        Assert.Contains(".Children.Add", generated);
        Assert.DoesNotContain("new view", generated);
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
        Assert.Contains(".AddEventListener(\"input\", e => Password.Value = ((Square.Controls.Controls.Input)e.Target!).Value);", generated);
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

        Assert.Contains(".AddEventListener(\"change\", e => Name.Value = ((Square.Controls.Controls.Input)e.Target!).Value.Trim());", generated);
        Assert.Contains(".AddEventListener(\"input\", e => Age.Value = double.Parse(((Square.Controls.Controls.Input)e.Target!).Value, System.Globalization.CultureInfo.InvariantCulture));", generated);
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
        Assert.Contains(".AddEventListener(\"change\", e => RememberMe.Value = ((Square.Controls.Controls.CheckBox)e.Target!).IsChecked);", generated);
        Assert.Contains(".BindProperty(\"Value\", Plan);", generated);
        Assert.Contains(".AddEventListener(\"change\", e => Plan.Value = ((Square.Controls.Controls.Select)e.Target!).Value);", generated);
    }

    private static GeneratorDriverRunResult RunGenerator(params AdditionalText[] files)
    {
        var compilation = CSharpCompilation.Create(
            "VueGeneratorTests",
            [CSharpSyntaxTree.ParseText("public sealed class Placeholder { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SqxGenerator().AsSourceGenerator()],
            files,
            (CSharpParseOptions?)compilation.SyntaxTrees.First().Options);
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(content, Encoding.UTF8);
    }
}
