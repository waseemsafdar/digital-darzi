using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/salaries")]
[Authorize(Roles = "Owner,Manager")]
public class SalariesController
    : BaseCrudController<StaffSalary, RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel, StaffSalaryListViewModel, StaffSalarySearchModel>
{
    private readonly IStaffSalaryService<RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel> _salaryService;

    public SalariesController(IStaffSalaryService<RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel> service) : base(service)
    {
        _salaryService = service;
    }

    [HttpPost]
    public override async Task<IActionResult> Create([FromBody] RecordStaffSalaryViewModel vm, CancellationToken ct)
    {
        var result = await _salaryService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : ReturnProcessedResponse(result);
    }
}
