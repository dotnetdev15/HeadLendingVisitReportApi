using HeadLendingVisitReportApi.Models.Dtos;
using HeadLendingVisitReportApi.Models.Entities;
using HeadLendingVisitReportApi.Repositories;
using HeadLendingVisitReportApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HeadLendingVisitReportApi.Controllers;

[ApiController]
[Route("api/visit-reports")]
public class VisitReportsController : ControllerBase
{
    private readonly IVisitReportRepository _repository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<VisitReportsController> _logger;

    public VisitReportsController(IVisitReportRepository repository, IAttachmentRepository attachmentRepository, IFileStorageService fileStorage, ILogger<VisitReportsController> logger)
    {
        _repository = repository;
        _attachmentRepository = attachmentRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }
    #region
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VisitReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VisitReportDto>>> GetAll([FromQuery] VisitReportQuery query)
    {
        var result = await _repository.GetAllAsync(query ?? new VisitReportQuery());
        return Ok(result);
    }
    [HttpGet("role-team")]
    [ProducesResponseType(typeof(PagedResult<VisitReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<VisitReportDto>>> GetByRoleTeam([FromQuery] VisitReportQuery query)
    {
        query ??= new VisitReportQuery();
        try
        {
            var result = await _repository.GetByRoleTeamAsync(query);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    /// <summary>
    /// Admin Visit Report — search summary list (Name, EmpCode, VisitCount).
    /// Filters: FromDate, ToDate, EmpType, Creator, BranchCode, EmployeeCode.
    /// </summary>
    [HttpGet("admin-visit-summary")]
    [ProducesResponseType(typeof(PagedResult<AdminVisitSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminVisitSummaryDto>>> GetAdminVisitSummary([FromQuery] AdminVisitReportQuery query)
    {
        query ??= new AdminVisitReportQuery();
        if (!query.FromDate.HasValue || !query.ToDate.HasValue)
            return BadRequest(new { message = "FromDate and ToDate are required." });
        if (query.FromDate.Value.Date > query.ToDate.Value.Date)
            return BadRequest(new { message = "FromDate cannot be later than ToDate." });
        if ((query.ToDate.Value.Date - query.FromDate.Value.Date).TotalDays > 31)
            return BadRequest(new { message = "Date range cannot be more than 31 days." });
        if (string.IsNullOrWhiteSpace(query.EmpType))
            return BadRequest(new { message = "EmpType is required (HEAD/VP/ZM/RM)." });

        var result = await _repository.GetAdminVisitSummaryAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Admin Visit Report — popup details when Visit Count is clicked.
    /// Returns Branch Code / Branch Name / Creator for the selected EmpCode.
    /// </summary>
    [HttpGet("admin-visit-details")]
    [ProducesResponseType(typeof(List<AdminVisitDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AdminVisitDetailDto>>> GetAdminVisitDetails([FromQuery] AdminVisitReportQuery query)
    {
        query ??= new AdminVisitReportQuery();
        if (string.IsNullOrWhiteSpace(query.EmployeeCode))
            return BadRequest(new { message = "EmployeeCode is required." });
        if (!query.FromDate.HasValue || !query.ToDate.HasValue)
            return BadRequest(new { message = "FromDate and ToDate are required." });

        try
        {
            var items = await _repository.GetAdminVisitDetailsAsync(query);
            return Ok(new { items, totalCount = items.Count });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VisitReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisitReportDto>> GetById(int id)
    {
        var report = await _repository.GetByIdAsync(id);
        if (report is null) return NotFound(new { message = $"Visit report {id} was not found." });
        return Ok(report);
    }
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VisitReportDto), StatusCodes.Status201Created)]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<VisitReportDto>> Create([FromForm] VisitReportDto request)
    {
        if (request == null)
            return BadRequest(new { message = "Invalid Request." });

        int newId = await _repository.CreateAsync(request);

        if (request.Files != null && request.Files.Count > 0)
        {
            for (int i = 0; i < request.Files.Count; i++)
            {
                var file = request.Files[i];

                if (file == null || file.Length == 0)
                    continue;

                var savedFile = await _fileStorage.SaveFileAsync(newId, file);

                string category = (request.Categories != null && request.Categories.Count > i) ? request.Categories[i]?.Trim().ToLower() : "others";

                switch (category)
                {
                    case "business":
                        request.BusinessTypeImg = savedFile.RelativePath;
                        break;
                    case "cash":
                        request.CashTypeImg = savedFile.RelativePath;
                        break;
                    case "od":
                        request.OdTypeImg = savedFile.RelativePath;
                        break;
                    case "npa":
                        request.NpaTypeImg = savedFile.RelativePath;
                        break;
                    case "collection":
                        request.CollectionTypeImg = savedFile.RelativePath;
                        break;
                    case "audit":
                        request.AuditTypeImg = savedFile.RelativePath;
                        break;
                    default:
                        request.OthersTypeImg = savedFile.RelativePath;
                        category = "others";
                        break;
                }

                await _attachmentRepository.InsertAsync(new AttachmentRecord
                {
                    VisitReportId = newId,
                    FileName = Path.GetFileName(file.FileName),
                    StoredFileName = savedFile.StoredFileName,
                    FilePath = savedFile.RelativePath,
                    ContentType = file.ContentType,
                    FileSizeBytes = savedFile.FileSizeBytes,
                    DocumentCategory = category
                });
            }

            await _repository.UpdateImagesAsync(newId, request);
        }
        var created = await _repository.GetByIdAsync(newId);

        return CreatedAtAction(nameof(GetById), new { id = newId }, created);
    }
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VisitReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisitReportDto>> Update(int id, [FromBody] VisitReportDto dto)
    {
        if (dto is null) return BadRequest(new { message = "Request body is required." });

        var updated = await _repository.UpdateAsync(id, dto);
        if (!updated) return NotFound(new { message = $"Visit report {id} was not found." });

        var report = await _repository.GetByIdAsync(id);
        return Ok(report);
    }
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var attachments = await _attachmentRepository.GetByVisitReportIdAsync(id);

        var rowsDeleted = await _repository.DeleteAsync(id);
        if (rowsDeleted == 0) return NotFound(new { message = $"Visit report {id} was not found." });

        foreach (var attachment in attachments)
        {
            try
            {
                _fileStorage.DeleteFile(attachment.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete attachment file {FilePath} for visit report {Id}.", attachment.FilePath, id);
            }
        }
        return NoContent();
    }
    [HttpGet("login-profile")]
    [ProducesResponseType(typeof(LoginProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginProfileDto>> GetLoginProfile([FromQuery] string empCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(empCode))
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "EmpCode is required."
                });
            }

            var profile = await _repository.GetLoginProfileByEmpCodeAsync(empCode.Trim());

            if (profile == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = $"No active visit-report profile found for EmpCode '{empCode}'."
                });
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Success = false,
                Message = "An error occurred while fetching the login profile.",
                Error = ex.Message
            });
        }
    }
    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult> AddComment(int id, [FromBody] AddVisitReportCommentRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CommentText))
            return BadRequest(new { message = "CommentText is required." });

        var newId = await _repository.AddCommentAsync(id, request);
        return Ok(new { id = newId, visitReportId = id });
    }
    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<List<VisitReportCommentDto>>> GetComments(int id)
    {
        return Ok(await _repository.GetCommentsAsync(id));
    }
    #endregion

    [HttpGet("creator")]
    public async Task<ActionResult<PagedResult<CreatorMappingDto>>> GetCreatorMappings([FromQuery] MappingListQuery query)
    {
        var result = await _repository.GetCreatorMappingsAsync(query ?? new MappingListQuery());
        return Ok(result);
    }
    [HttpPut("creator/{id:int}/status")]
    public async Task<IActionResult> UpdateCreatorStatus(int id, [FromBody] UpdateMappingStatusRequest request)
    {
        if (request == null) return BadRequest(new { message = "Request body is required." });
        var ok = await _repository.UpdateCreatorMappingStatusAsync(id, request.IsActive);
        if (!ok) return NotFound(new { message = $"Creator mapping {id} not found." });
        return Ok(new { success = true });
    }
    [HttpPost("creator/bulk-upload")]
    public async Task<ActionResult<BulkUploadResultDto>> BulkUploadCreatorMappings([FromBody] CreatorMappingBulkUploadRequest request)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { message = "No rows to upload." });
        var result = await _repository.BulkUploadCreatorMappingsAsync(request);
        return Ok(result);
    }
    [HttpGet("reporting")]
    public async Task<ActionResult<PagedResult<ReportingMappingDto>>> GetReportingMappings([FromQuery] MappingListQuery query)
    {
        var result = await _repository.GetReportingMappingsAsync(query ?? new MappingListQuery());
        return Ok(result);
    }
    [HttpPut("reporting/{id:int}/status")]
    public async Task<IActionResult> UpdateReportingStatus(int id, [FromBody] UpdateMappingStatusRequest request)
    {
        if (request == null) return BadRequest(new { message = "Request body is required." });
        var ok = await _repository.UpdateReportingMappingStatusAsync(id, request.IsActive);
        if (!ok) return NotFound(new { message = $"Reporting mapping {id} not found." });
        return Ok(new { success = true });
    }
}