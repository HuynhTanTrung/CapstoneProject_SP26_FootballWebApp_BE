using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services
{
    public class ChatConversationService : IChatConversationService
    {
        private const string SenderUser = "User";
        private const string SenderAssistant = "Assistant";

        private readonly VNFootballLeaguesDBContext _db;
        private readonly IGeminiService _gemini;

        public ChatConversationService(VNFootballLeaguesDBContext db, IGeminiService gemini)
        {
            _db = db;
            _gemini = gemini;
        }

        public async Task<ChatSendResult> SendMessageAsync(Guid userId, Guid? sessionId, string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message is required.", nameof(message));

            var userExists = await _db.Users.AnyAsync(u => u.UserId == userId, cancellationToken);
            if (!userExists)
                throw new InvalidOperationException("User not found.");

            ChatSession session;

            if (sessionId == null || sessionId == Guid.Empty)
            {
                var title = BuildTitle(message);
                session = new ChatSession
                {
                    SessionId = Guid.NewGuid(),
                    UserId = userId,
                    Title = title,
                    StartTime = DateTime.UtcNow
                };
                _db.ChatSessions.Add(session);
                await _db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId, cancellationToken)
                    ?? throw new InvalidOperationException("Session not found or access denied.");
            }

            var userMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = session.SessionId,
                Sender = SenderUser,
                Text = message.Trim(),
                Timestamp = DateTime.UtcNow
            };
            _db.ChatMessages.Add(userMessage);
            await _db.SaveChangesAsync(cancellationToken);

            var historyRows = await _db.ChatMessages
                .Where(m => m.SessionId == session.SessionId)
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.MessageId)
                .Select(m => new { m.Sender, m.Text })
                .ToListAsync(cancellationToken);

            var turns = historyRows
                .Select(m => (
                    role: string.Equals(m.Sender, SenderUser, StringComparison.OrdinalIgnoreCase) ? "user" : "model",
                    text: m.Text ?? string.Empty))
                .ToList();

            var assistantText = await _gemini.ChatWithHistoryAsync(turns);

            var assistantMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = session.SessionId,
                Sender = SenderAssistant,
                Text = assistantText,
                Timestamp = DateTime.UtcNow
            };
            _db.ChatMessages.Add(assistantMessage);
            await _db.SaveChangesAsync(cancellationToken);

            return new ChatSendResult(session.SessionId, assistantText, session.Title);
        }

        public async Task<IReadOnlyList<ChatSessionSummaryDto>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.ChatSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartTime)
                .Select(s => new ChatSessionSummaryDto(s.SessionId, s.Title, s.StartTime))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
        {
            var owns = await _db.ChatSessions.AnyAsync(s => s.SessionId == sessionId && s.UserId == userId, cancellationToken);
            if (!owns)
                throw new InvalidOperationException("Session not found or access denied.");

            return await _db.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.MessageId)
                .Select(m => new ChatMessageDto(m.MessageId, m.Sender, m.Text, m.Timestamp))
                .ToListAsync(cancellationToken);
        }

        private static string BuildTitle(string message)
        {
            var trimmed = message.Trim();
            const int max = 500;
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }
    }
}
