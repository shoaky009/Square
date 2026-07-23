namespace Square.Compiler.Parser;

/// <summary>
/// Vue <c>v-for</c> 指令的专用 AST 节点。
/// 与 SQX 的 <c>For</c> 指令独立，便于 Vue 路径单独演进。
/// </summary>
internal sealed class SqvForDirective : SqxNode
{
    public string SourceExpression = "";
    public string ItemName = "item";
    public string IndexName;
    public List<SqxNode> Children = new();
}

/// <summary>
/// Vue <c>v-if</c> / <c>v-else-if</c> / <c>v-else</c> 条件链的专用 AST 节点。
/// 每个分支保存原始条件（else 分支条件为 null）与对应子树。
/// </summary>
internal sealed class SqvIfChainDirective : SqxNode
{
    public List<SqvIfBranch> Branches = new();
}

internal sealed class SqvIfBranch
{
    public string Condition;
    public bool IsElse;
    public List<SqxNode> Children = new();
}