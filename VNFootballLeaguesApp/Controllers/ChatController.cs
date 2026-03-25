using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly IChatConversationService _chatConversation;

        public ChatController(IGeminiService geminiService, IChatConversationService chatConversation)
        {
            _geminiService = geminiService;
            _chatConversation = chatConversation;
        }

        /// <summary>
        /// Chat có lưu DB: gửi UserId (bắt buộc), SessionId (null = phiên mới), Message.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            if (request.UserId == Guid.Empty)
                return BadRequest(new { error = "UserId is required" });

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required" });

            try
            {
                var result = await _chatConversation.SendMessageAsync(
                    request.UserId,
                    request.SessionId,
                    request.Message,
                    cancellationToken);

                return Ok(new
                {
                    sessionId = result.SessionId,
                    sessionTitle = result.SessionTitle,
                    message = request.Message.Trim(),
                    response = result.Response
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Chat thử nhanh, không lưu database (giữ tương thích).
        /// </summary>
        [HttpPost("preview")]
        public async Task<IActionResult> ChatPreview([FromBody] ChatPreviewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required" });

            var response = await _geminiService.ChatAsync(request.Message);

            return Ok(new
            {
                message = request.Message,
                response
            });
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] Guid userId, CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { error = "userId query is required" });

            var list = await _chatConversation.GetSessionsAsync(userId, cancellationToken);
            return Ok(new { count = list.Count, data = list });
        }

        [HttpGet("sessions/{sessionId:guid}/messages")]
        public async Task<IActionResult> GetMessages(
            [FromRoute] Guid sessionId,
            [FromQuery] Guid userId,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { error = "userId query is required" });

            try
            {
                var list = await _chatConversation.GetMessagesAsync(userId, sessionId, cancellationToken);
                return Ok(new { sessionId, count = list.Count, data = list });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }

    public class ChatRequest
    {
        public Guid UserId { get; set; }

        /// <summary>Null hoặc 00000000-0000-0000-0000-000000000000 = tạo phiên mới.</summary>
        public Guid? SessionId { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ChatPreviewRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
