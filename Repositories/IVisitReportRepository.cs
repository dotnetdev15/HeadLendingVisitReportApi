using HeadLendingVisitReportApi.Models.Dtos;

namespace HeadLendingVisitReportApi.Repositories;

public interface IVisitReportRepository
{
    #region
    Task<int> CreateAsync(VisitReportDto dto);
    Task<bool> UpdateAsync(int id, VisitReportDto dto);
    Task<VisitReportDto> GetByIdAsync(int id);
    Task<PagedResult<VisitReportDto>> GetAllAsync(VisitReportQuery query);
    Task<PagedResult<VisitReportDto>> GetByRoleTeamAsync(VisitReportQuery query);
    Task<PagedResult<AdminVisitSummaryDto>> GetAdminVisitSummaryAsync(AdminVisitReportQuery query);
    Task<List<AdminVisitDetailDto>> GetAdminVisitDetailsAsync(AdminVisitReportQuery query);
    Task<int> DeleteAsync(int id);
    Task<LoginProfileDto> GetLoginProfileByEmpCodeAsync(string empCode);
    Task UpdateImagesAsync(int id, VisitReportDto dto);
    Task<int> AddCommentAsync(int visitReportId, AddVisitReportCommentRequest req);
    Task<List<VisitReportCommentDto>> GetCommentsAsync(int visitReportId);
    #endregion
    Task<PagedResult<CreatorMappingDto>> GetCreatorMappingsAsync(MappingListQuery query);
    Task<bool> UpdateCreatorMappingStatusAsync(int id, bool isActive);
    Task<BulkUploadResultDto> BulkUploadCreatorMappingsAsync(CreatorMappingBulkUploadRequest request);
    Task<PagedResult<ReportingMappingDto>> GetReportingMappingsAsync(MappingListQuery query);
    Task<bool> UpdateReportingMappingStatusAsync(int id, bool isActive);
}