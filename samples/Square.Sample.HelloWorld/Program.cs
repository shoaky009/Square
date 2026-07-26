using Square.Controls;
using Square.Hosting;
using Square.Images;

ImageSourceRegistration.RegisterDefaults();

var window = new AppWindow("Hello World", 480, 320);
window.Load(new Text("Hello World"));

new DesktopApplication(window).Run();
