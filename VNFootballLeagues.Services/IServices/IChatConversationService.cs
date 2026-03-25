namespace VNFootballLeagues.Services.IServices
{
    public sealed record ChatSendResult(Guid SessionId, string Response, string SessionTitle);

    public interface IChatConversationService
    {
        Task<ChatSendResult> SendMessageAsync(Guid userId, Guid? sessionId, string message, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatSessionSummaryDto>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    }

    public sealed record ChatSessionSummaryDto(Guid SessionId, string Title, DateTime StartTime);

    public sealed record ChatMessageDto(Guid MessageId, string Sender, string Text, DateTime Timestamp);
}
