using HeadLendingVisitReportApi.Models.Dtos;
using HeadLendingVisitReportApi.Models.Entities;
using HeadLendingVisitReportApi.Repositories;
using HeadLendingVisitReportApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HeadLendingVisitReportApi.Controllers;

[ApiController]
[Route("api/visit-reports/{visitReportId:int}/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IVisitReportRepository _visitReportRepository;
    private readonly IFileStorageService _fileStorage;

    public AttachmentsController(IAttachmentRepository attachmentRepository,IVisitReportRepository visitReportRepository,IFileStorageService fileStorage)
    {
        _attachmentRepository = attachmentRepository;
        _visitReportRepository = visitReportRepository;
        _fileStorage = fileStorage;
    }
    [HttpGet]
    [ProducesResponseType(typeof(List<AttachmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttachmentDto>>> GetAll(int visitReportId)
    {
        var records = await _attachmentRepository.GetByVisitReportIdAsync(visitReportId);
        return Ok(records.Select(ToDto).ToList());
    }
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(List<AttachmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<List<AttachmentDto>>> Upload(int visitReportId, [FromForm] List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { message = "At least one file must be provided." });

        var report = await _visitReportRepository.GetByIdAsync(visitReportId);
        if (report is null) return NotFound(new { message = $"Visit report {visitReportId} was not found." });

        var saved = new List<AttachmentDto>();

        foreach (var file in files)
        {
            var savedFile = await _fileStorage.SaveFileAsync(visitReportId, file);

            var record = new AttachmentRecord
            {
                VisitReportId = visitReportId,
                FileName = Path.GetFileName(file.FileName),
                StoredFileName = savedFile.StoredFileName,
                FilePath = savedFile.RelativePath,
                ContentType = file.ContentType,
                FileSizeBytes = savedFile.FileSizeBytes
            };

            var newId = await _attachmentRepository.InsertAsync(record);
            if (newId == 0)
            {
                _fileStorage.DeleteFile(savedFile.RelativePath);
                return NotFound(new { message = $"Visit report {visitReportId} was not found." });
            }
            record.Id = newId;
            saved.Add(ToDto(record));
        }
        return CreatedAtAction(nameof(GetAll), new { visitReportId }, saved);
    }
    [HttpGet("{attachmentId:int}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int visitReportId, int attachmentId)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId);
        if (attachment is null || attachment.VisitReportId != visitReportId)
            return NotFound(new { message = "Attachment was not found for this visit report." });

        var stream = _fileStorage.OpenRead(attachment.FilePath);
        return File(stream, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }
    [HttpDelete("{attachmentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int visitReportId, int attachmentId)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId);
        if (attachment is null || attachment.VisitReportId != visitReportId)
            return NotFound(new { message = "Attachment was not found for this visit report." });

        await _attachmentRepository.DeleteAsync(attachmentId);
        _fileStorage.DeleteFile(attachment.FilePath);

        return NoContent();
    }
    private static AttachmentDto ToDto(AttachmentRecord record) => new()
    {
        Id = record.Id,
        VisitReportId = record.VisitReportId,
        FileName = record.FileName,
        ContentType = record.ContentType,
        FileSizeBytes = record.FileSizeBytes,
        UploadedAtUtc = record.UploadedAtUtc
    };
}
