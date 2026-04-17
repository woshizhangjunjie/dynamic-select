using Avalonia.Controls.Notifications;

namespace DailyWords.Mac;

public sealed record NotificationRequest(string Title, string Message, NotificationType Type);
