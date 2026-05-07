using Microsoft.Extensions.DependencyInjection;

namespace Mekatrol.QRCode.Common;

/// <summary>
/// Provides dependency injection registration for QR Code services.
/// </summary>
public static class QRCodeServiceCollectionExtensions
{
    /// <summary>
    /// Adds the QR Code generator service using transient scope.
    /// </summary>
    /// <param name="services">The service collection to add the generator to.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddQRCodeGenerator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IQRCodeGenerator, QRCodeGenerator>();
        services.AddTransient<QRCodeGenerator>();
        return services;
    }
}
