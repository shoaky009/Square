namespace Square.Sample.CodePadApp.Components;

public partial class CodePadTitleBar
{
    public Main? Page { get; set; }

    private void OnLoadSample() => Page?.OnLoadSample();
    private void OnClear() => Page?.OnClear();
    private void OnUndo() => Page?.OnUndo();
    private void OnRedo() => Page?.OnRedo();
    private void OnSelectAll() => Page?.OnSelectAll();
    private void OnFindNext() => Page?.OnFindNext();
    private void OnFindPrevious() => Page?.OnFindPrevious();
    private void OnReplaceNext() => Page?.OnReplaceNext();
    private void OnReplaceAll() => Page?.OnReplaceAll();
    private void OnToggleComment() => Page?.OnToggleComment();
    private void OnLangCSharp() => Page?.OnLangCSharp();
    private void OnLangJson() => Page?.OnLangJson();
    private void OnLangJs() => Page?.OnLangJs();
    private void OnLangPython() => Page?.OnLangPython();
    private void OnLangMarkdown() => Page?.OnLangMarkdown();
    private void OnThemeLight() => Page?.OnThemeLight();
    private void OnThemeDark() => Page?.OnThemeDark();
    private void OnToggleLineNumbers() => Page?.OnToggleLineNumbers();
    private void OnToggleFolding() => Page?.OnToggleFolding();
    private void OnCollapseAllFolds() => Page?.OnCollapseAllFolds();
    private void OnExpandAllFolds() => Page?.OnExpandAllFolds();
    private void OnSelectFoldAtCaret() => Page?.OnSelectFoldAtCaret();
    private void OnToggleWordWrap() => Page?.OnToggleWordWrap();
    private void OnToggleScrollBars() => Page?.OnToggleScrollBars();
    private void OnToggleOverviewRuler() => Page?.OnToggleOverviewRuler();
    private void OnToggleFindPanel() => Page?.OnToggleFindPanel();
    private void OnToggleReadOnly() => Page?.OnToggleReadOnly();
    private void OnToggleGlyphMargin() => Page?.OnToggleGlyphMargin();
}
