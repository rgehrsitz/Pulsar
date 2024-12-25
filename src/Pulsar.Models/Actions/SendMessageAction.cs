namespace Pulsar.Models.Actions;

/// <summary>
/// Action to send a message to a channel
/// </summary>
public class SendMessageAction
{
    /// <summary>
    /// The channel to send the message to
    /// </summary>
    public string Channel { get; set; } = "";

    /// <summary>
    /// The message to send
    /// </summary>
    public string Message { get; set; } = "";
}
