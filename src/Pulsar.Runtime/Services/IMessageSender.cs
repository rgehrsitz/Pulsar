using System.Threading.Tasks;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Interface for sending messages to external systems
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Sends a message to a specified channel
    /// </summary>
    /// <param name="channel">The channel to send the message to (e.g., email, sms, webhook)</param>
    /// <param name="message">The message content to send</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendMessageAsync(string channel, string message);
}
