using System.Net;
using Application.Common;
using Application.Common.Models;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Common;
using AutoMapper;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Generic base CRUD service — uses AutoMapper for entity↔ViewModel conversions.
/// Matches MobilePosApi.BaseCrudService pattern.
/// </summary>
public class BaseCrudService<TEntity, TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TEntity : BaseDBModel
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    protected readonly IBaseRepository<TEntity> _repo;
    protected readonly IMapper _mapper;

    public BaseCrudService(IBaseRepository<TEntity> repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public virtual async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(id, ct);
            if (entity == null)
                return new ApiResponse<TDetail>("Record not found.", (int)HttpStatusCode.NotFound);
            return new ApiResponse<TDetail>(_mapper.Map<TDetail>(entity));
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<TDetail>("An error occurred while retrieving the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public virtual async Task<ApiResponse<PaginatedResultModel<TDetail>>> GetAllAsync(IBaseSearchModel search, CancellationToken ct = default)
    {
        try
        {
            var paged = await _repo.GetPagedAsync(search, ct);
            var items = _mapper.Map<IEnumerable<TDetail>>(paged.Items);
            var result = new PaginatedResultModel<TDetail>(items, search.PageNumber, search.PageSize, paged.TotalCount);
            return new ApiResponse<PaginatedResultModel<TDetail>>(result);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<PaginatedResultModel<TDetail>>("An error occurred while retrieving records.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public virtual async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = _mapper.Map<TEntity>(vm);
            
            if (vm.GetType().GetProperty("ActiveStatus") == null && entity is BaseDBModel baseModel)
            {
                baseModel.ActiveStatus = Domain.Enums.ActiveStatus.Active;
            }
                
            var created = await _repo.AddAsync(entity, ct);
            return new ApiResponse<Guid>(created.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while creating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public virtual async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(vm.Id, ct);
            if (entity == null)
                return new ApiResponse<Guid>("Record not found.", (int)HttpStatusCode.NotFound);
            _mapper.Map(vm, entity);
            await _repo.UpdateAsync(entity, ct);
            return new ApiResponse<Guid>(vm.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while updating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public virtual async Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _repo.DeleteAsync(id, ct);
            return new ApiResponse<string>(id.ToString(), "Deleted successfully.");
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<string>("An error occurred while deleting the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }
}
