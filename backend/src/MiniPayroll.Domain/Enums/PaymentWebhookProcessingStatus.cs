namespace MiniPayroll.Domain.Enums;

public enum PaymentWebhookProcessingStatus
{
    Received = 0,
    Processed = 1,
    Failed = 2,
    Ignored = 3
}
