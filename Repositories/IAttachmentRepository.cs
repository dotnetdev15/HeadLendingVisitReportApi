using HeadLendingVisitReportApi.Models.Dtos;
using HeadLendingVisitReportApi.Models.Entities;

namespace HeadLendingVisitReportApi.Repositories;

public interface IAttachmentRepository
{
    Task<int> InsertAsync(AttachmentRecord record);
    Task<List<AttachmentRecord>> GetByVisitReportIdAsync(int visitReportId);
    Task<AttachmentRecord> GetByIdAsync(int id);
    Task<int> DeleteAsync(int id);
}
