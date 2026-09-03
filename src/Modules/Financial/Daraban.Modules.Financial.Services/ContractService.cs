using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services;

public class ContractService : IContractService
{
    private readonly IContractRepository _contractRepository;

    public ContractService(IContractRepository contractRepository)
    {
        _contractRepository = contractRepository;
    }

    public async Task<Result<ContractPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        ContractStatus? status,
        Guid? supplierId,
        Guid? contractTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _contractRepository.GetPagedAsync(
            entityNodeId, search, status, supplierId, contractTypeId, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<ContractPagedResult>.Success(new ContractPagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<ContractDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await _contractRepository.GetByIdWithDetailsAsync(id, ct);
        if (contract is null)
            return Result.Failure<ContractDto>(new Error("CONTRACT.NOT_FOUND", "Contract not found.", ErrorType.NotFound));

        return Result<ContractDto>.Success(MapToDto(contract));
    }

    public async Task<Result<ContractDto>> CreateAsync(CreateContractRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityNodeId,
            Name = request.Name,
            Reference = request.Reference,
            ContractTypeId = request.ContractTypeId,
            SupplierId = request.SupplierId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DurationMonths = request.DurationMonths,
            Value = request.Value,
            MonthlyCost = request.MonthlyCost,
            AnnualCost = request.AnnualCost,
            Currency = request.Currency,
            BillingFrequency = request.BillingFrequency,
            AutoRenew = request.AutoRenew,
            NoticePeriodDays = request.NoticePeriodDays,
            SignedDate = request.SignedDate,
            SignedById = request.SignedById,
            DocumentLocation = request.DocumentLocation,
            Terms = request.Terms,
            Comment = request.Comment,
            IsCritical = request.IsCritical,
            Status = ContractStatus.Draft,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _contractRepository.AddAsync(contract, ct);
        await _contractRepository.SaveChangesAsync(ct);

        return Result<ContractDto>.Success(MapToDto(contract));
    }

    public async Task<Result<ContractDto>> UpdateAsync(Guid id, UpdateContractRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var contract = await _contractRepository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result.Failure<ContractDto>(new Error("CONTRACT.NOT_FOUND", "Contract not found.", ErrorType.NotFound));

        contract.Name = request.Name;
        contract.Reference = request.Reference;
        contract.ContractTypeId = request.ContractTypeId;
        contract.SupplierId = request.SupplierId;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.DurationMonths = request.DurationMonths;
        contract.Value = request.Value;
        contract.MonthlyCost = request.MonthlyCost;
        contract.AnnualCost = request.AnnualCost;
        contract.Currency = request.Currency;
        contract.BillingFrequency = request.BillingFrequency;
        contract.AutoRenew = request.AutoRenew;
        contract.NoticePeriodDays = request.NoticePeriodDays;
        contract.DocumentLocation = request.DocumentLocation;
        contract.Terms = request.Terms;
        contract.Comment = request.Comment;
        contract.IsCritical = request.IsCritical;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        contract.UpdatedById = actorUserId;

        await _contractRepository.UpdateAsync(contract, ct);
        await _contractRepository.SaveChangesAsync(ct);

        return Result<ContractDto>.Success(MapToDto(contract));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var contract = await _contractRepository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result.Failure(new Error("CONTRACT.NOT_FOUND", "Contract not found.", ErrorType.NotFound));

        // Soft delete
        contract.IsDeleted = true;
        contract.DeletedAt = DateTimeOffset.UtcNow;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        contract.UpdatedById = actorUserId;

        await _contractRepository.UpdateAsync(contract, ct);
        await _contractRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<ContractDto>> ChangeStatusAsync(Guid id, ContractStatus newStatus, Guid actorUserId, CancellationToken ct = default)
    {
        var contract = await _contractRepository.GetByIdAsync(id, ct);
        if (contract is null)
            return Result.Failure<ContractDto>(new Error("CONTRACT.NOT_FOUND", "Contract not found.", ErrorType.NotFound));

        // Validate status transition
        var isValidTransition = IsValidStatusTransition(contract.Status, newStatus);
        if (!isValidTransition)
            return Result.Failure<ContractDto>(new Error("CONTRACT.INVALID_TRANSITION", $"Cannot transition from {contract.Status} to {newStatus}.", ErrorType.BusinessRule));

        contract.Status = newStatus;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        contract.UpdatedById = actorUserId;

        await _contractRepository.UpdateAsync(contract, ct);
        await _contractRepository.SaveChangesAsync(ct);

        return Result<ContractDto>.Success(MapToDto(contract));
    }

    private static bool IsValidStatusTransition(ContractStatus current, ContractStatus next)
    {
        return current switch
        {
            ContractStatus.Draft => next is ContractStatus.Active,
            ContractStatus.Active => next is ContractStatus.Suspended or ContractStatus.Expired or ContractStatus.Cancelled,
            ContractStatus.Suspended => next is ContractStatus.Active or ContractStatus.Cancelled,
            ContractStatus.Expired => next is ContractStatus.Active,
            ContractStatus.Cancelled => false,
            ContractStatus.Terminated => false,
            _ => false
        };
    }

    private static ContractDto MapToDto(Contract contract) => new(
        contract.Id,
        contract.EntityId,
        contract.Name,
        contract.Reference,
        contract.ContractTypeId,
        contract.ContractType?.Name,
        contract.SupplierId,
        contract.Supplier?.Name,
        contract.StartDate,
        contract.EndDate,
        contract.DurationMonths,
        contract.Value,
        contract.MonthlyCost,
        contract.AnnualCost,
        contract.Currency,
        contract.BillingFrequency,
        contract.Status,
        contract.AutoRenew,
        contract.NoticePeriodDays,
        contract.SignedDate,
        contract.SignedById,
        contract.DocumentLocation,
        contract.Terms,
        contract.Comment,
        contract.IsCritical,
        contract.CreatedAt,
        contract.UpdatedAt);

    private static ContractListDto MapToListDto(Contract contract) => new(
        contract.Id,
        contract.Name,
        contract.Reference,
        contract.Supplier?.Name,
        contract.ContractType?.Name,
        contract.StartDate,
        contract.EndDate,
        contract.Value,
        contract.Status,
        contract.IsCritical);
}
