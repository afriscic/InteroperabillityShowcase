using intClient.Components;
using intClient.Resources.Styles;
using intShared;

namespace intClient;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiReactorApp<HomePage>(app =>
            {
                app.UseTheme<ApplicationTheme>();
            },
            unhandledExceptionAction: e => 
            {
                System.Diagnostics.Debug.WriteLine(e.ExceptionObject);
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        
        builder.Services.AddSingleton(new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(5)
        });
        builder.Services.AddSingleton(new EquipmentInfo()
        {
            ID = Guid.NewGuid(),
            Type = "PharmacySoftware",
            FriendlyName = "PharmacyClient1",
            PacksSupported = false,
            UnitsSupported = false
        });

        return builder.Build();
    }
}
