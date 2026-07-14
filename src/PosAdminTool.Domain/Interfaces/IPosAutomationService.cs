namespace PosAdminTool.Domain.Interfaces;

public interface IPosAutomationService
{
    Task AddItemsToCartAsync(IReadOnlyList<string> scannedCodes, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
