using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DigitalDarziApi.Domain.Entities;
using DigitalDarziApi.Application.Interfaces.Repositories;
using DigitalDarziApi.Application.Interfaces.Services;
using DigitalDarziApi.Application.Common.Models;
using DigitalDarziApi.Application.Common.Responses;
using DigitalDarziApi.Application.ViewModels.Common;

namespace DigitalDarziApi.Application.Services
{
    /// <summary>
    /// Generic CRUD service for entities inheriting from BaseDBModel.
    /// Mirrors MobilePosApi.BaseCrudService.
    /// </summary>
    public class BaseCrudService<TEntity, TCreateVm, TUpdateVm, TDetailVm>
        : IBaseCrudService<TCreateVm, TUpdateVm, TDetailVm>
        where TEntity : BaseDBModel
        where TCreateVm : class, IBaseCrudViewModel, new()
        where TUpdateVm : class, IBaseCrudViewModel, IIdentification, new()
        where TDetailVm : class, IBaseCrudViewModel, new()
    {
        protected readonly IBaseRepository<TEntity> _repository;
        protected readonly IMapper _mapper;

        public BaseCrudService(IBaseRepository<TEntity> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public virtual async Task<ApiResponse<TDetailVm>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return new ApiResponse<TDetailVm>("Record not found.", (int)HttpStatusCode.NotFound);

            var vm = _mapper.Map<TDetailVm>(entity);
            return new ApiResponse<TDetailVm>(vm);
        }

        public virtual async Task<ApiResponse<PaginatedResultModel<TDetailVm>>> GetAllAsync(IBaseSearchModel search, CancellationToken ct = default)
        {
            var (entities, total) = await _repository.GetPagedAsync(search);
            var items = _mapper.Map<IEnumerable<TDetailVm>>(entities);
            var result = new PaginatedResultModel<TDetailVm>(items, search.PageNumber, search.PageSize, total);
            return new ApiResponse<PaginatedResultModel<TDetailVm>>(result);
        }

        public virtual async Task<ApiResponse<Guid>> CreateAsync(TCreateVm vm, CancellationToken ct = default)
        {
            var entity = _mapper.Map<TEntity>(vm);
            if (entity is BaseDBModel baseModel)
            {
                if (vm.GetType().GetProperty("ActiveStatus") == null)
                    baseModel.ActiveStatus = Domain.Enums.ActiveStatus.Active;
                if (vm.GetType().GetProperty("IsActive") == null)
                    baseModel.IsActive = true;
            }
            var created = await _repository.AddAsync(entity);
            return new ApiResponse<Guid>(created.Id);
        }

        public virtual async Task<ApiResponse<Guid>> UpdateAsync(TUpdateVm vm, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(vm.Id);
            if (entity == null)
                return new ApiResponse<Guid>("Record not found.", (int)HttpStatusCode.NotFound);

            _mapper.Map(vm, entity);
            await _repository.UpdateAsync(entity);
            return new ApiResponse<Guid>(vm.Id);
        }

        public virtual async Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await _repository.DeleteAsync(id);
            return new ApiResponse<string>(id.ToString(), "Deleted successfully.");
        }
    }
}
