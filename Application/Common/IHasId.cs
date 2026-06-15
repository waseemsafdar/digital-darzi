namespace Application.Common;

/// <summary>
/// All Update ViewModels must implement this so BaseCrudService
/// can extract the Id without a separate route parameter.
/// Mirrors MobilePosApi IIdentification pattern.
/// </summary>
public interface IHasId
{
    Guid Id { get; set; }
}
