namespace Square.Images.Jpeg;

internal sealed class JpegComponent
{
    public int Id;
    public int HorizontalSampling;
    public int VerticalSampling;
    public int QuantizationTableId;
    public int[][] Coefficients = [];
    public int[][] Samples = [];
    public int BlocksX;
    public int BlocksY;
}