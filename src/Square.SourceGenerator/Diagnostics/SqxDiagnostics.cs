using Microsoft.CodeAnalysis;

namespace Square.SourceGenerator.Diagnostics;

public static class SqxDiagnostics
{
    public const string Category = "Square.SQX";

    public static readonly DiagnosticDescriptor SQX0001_SyntaxError = new(
        "SQX0001", "SQX 语法错误", "{0}", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0002_UndefinedControl = new(
        "SQX0002", "未定义的控件", "控件 '{0}' 未定义", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0003_RequiredPropMissing = new(
        "SQX0003", "必填 Prop 缺失", "组件 '{0}' 的必填 Prop '{1}' 未提供", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0004_BindingMemberNotFound = new(
        "SQX0004", "绑定成员未找到", "成员 '{0}' 未在组件中找到", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0005_EventSignatureMismatch = new(
        "SQX0005", "事件方法签名不匹配", "事件 '{0}' 的方法签名不匹配", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0006_RefNameConflict = new(
        "SQX0006", "ref 名称冲突", "ref 名称 '{0}' 冲突", Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SQX0007_PropTypeMismatch = new(
        "SQX0007", "Prop 类型不匹配", "Prop '{0}' 类型不匹配", Category, DiagnosticSeverity.Error, true);
}