using Square.PackageConsumer;
using Square.Graphics;
using Square.Graphics.Codecs;
using Square.Images;
using Square.Platform;

var component = new Main();
component.BuildElementTree();

if (component.Children.Count != 1)
    throw new InvalidOperationException("The packaged source generator did not build the SQX component.");

if (!component.CodeBehindLoaded)
    throw new InvalidOperationException("The SQX code-behind partial class was not compiled.");

var platform = PlatformRegistry.Get();
#if PLATFORM_WIN32
if (platform.Name != "Win32")
    throw new InvalidOperationException("The Win32 platform package was not automatically registered.");
#elif PLATFORM_X11
if (platform.Name != "X11")
    throw new InvalidOperationException("The X11 platform package was not automatically registered.");
#endif

using var source = new Bitmap(2, 1);
new byte[] { 30, 20, 10, 40, 60, 50, 40, 255 }.CopyTo(source.Pixels, 0);
using var encoded = new MemoryStream();
BitmapPngEncoder.Save(source, encoded);
using var decodedDocument = ImageDecoder.Decode(encoded.ToArray());
if (!source.Pixels.AsSpan().SequenceEqual(decodedDocument.PrimaryBitmap.Pixels))
    throw new InvalidOperationException("The packaged Square.Images PNG decoder returned incorrect pixels.");

var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");
using var decodedGif = ImageDecoder.Decode(gif);
if (decodedGif.Format != ImageFormat.Gif || decodedGif.Items.Count != 1 ||
    decodedGif.PrimaryBitmap.Width != 1 || decodedGif.PrimaryBitmap.Height != 1 || decodedGif.PrimaryBitmap.Pixels[3] != 255)
    throw new InvalidOperationException("The packaged Square.Images GIF decoder returned an invalid document.");

var webp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
using var decodedWebp = ImageDecoder.Decode(webp);
if (decodedWebp.PrimaryBitmap.Width != 8 || decodedWebp.PrimaryBitmap.Height != 8 ||
    !decodedWebp.PrimaryBitmap.Pixels.AsSpan(0, 4).SequenceEqual(new byte[] { 0, 0, 254, 255 }))
    throw new InvalidOperationException("The packaged Square.Images WebP decoder returned incorrect pixels.");
