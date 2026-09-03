using Microsoft.Extensions.Logging;
using Tedd.MOS65xx.Maui.Controls;

namespace Tedd.MOS65xx.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                // The emulator picture is the one control with no cross-platform equivalent: it needs a native
                // surface that can take a new 384 x 272 image 50 times a second without scaling it smoothly.
#if WINDOWS
                handlers.AddHandler<ScreenView, ScreenViewHandler>();
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
