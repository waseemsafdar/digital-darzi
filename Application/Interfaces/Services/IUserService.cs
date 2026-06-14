using Application.Common;
using Application.ViewModels.User;

namespace Application.Interfaces.Services;

public interface IUserService
{
    Task<ApiResponse<PagedResult<UserListViewModel>>> GetListAsync(Guid? shopId, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<UserDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<UserDetailViewModel>> CreateAsync(CreateUserViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<UserDetailViewModel>> UpdateAsync(Guid id, UpdateUserViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<object>> AssignShopsAsync(Guid id, List<Guid> shopIds, CancellationToken ct = default);
    Task<ApiResponse<object>> AssignRolesAsync(Guid id, List<Guid> roleIds, CancellationToken ct = default);
}
