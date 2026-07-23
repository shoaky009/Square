namespace Square.Sample.RichText.Components;

public partial class RichTextTitleBar
{
    public Main? Page { get; set; }

    private void OnLoadSample() => Page?.OnLoadSample();
    private void OnClear() => Page?.OnClear();
    private void OnUndo() => Page?.OnUndo();
    private void OnRedo() => Page?.OnRedo();
    private void OnSelectAll() => Page?.OnSelectAll();
    private void OnCopyRich() => Page?.OnCopyRich();
    private void OnPasteRich() => Page?.OnPasteRich();
    private void OnBold() => Page?.OnBold();
    private void OnItalic() => Page?.OnItalic();
    private void OnUnderline() => Page?.OnUnderline();
    private void OnClearFormatting() => Page?.OnClearFormatting();
    private void OnBlue() => Page?.OnBlue();
    private void OnRed() => Page?.OnRed();
    private void OnGreen() => Page?.OnGreen();
}
