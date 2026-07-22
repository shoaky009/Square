namespace Square.Images.Jpeg;

internal sealed class JpegScanComponent
{
    public JpegComponent Component = null!;
    public int DcTableId;
    public int AcTableId;
    public int PredictedDc;
}

internal sealed class JpegState
{
    public bool HasFrame;
    public JpegComponent[] Components = [];
    public JpegScanComponent[]? ScanComponents;
    public JpegHuffmanTable.Table?[] DcTables = new JpegHuffmanTable.Table?[4];
    public JpegHuffmanTable.Table?[] AcTables = new JpegHuffmanTable.Table?[4];
    public int[]?[] QuantizationTables = new int[4][];
    public int RestartInterval;
    public int Width;
    public int Height;
    public int MaxHorizontal;
    public int MaxVertical;
}