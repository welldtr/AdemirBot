using System.Runtime.Serialization;

namespace DiscordBot.Services
{
    [Serializable]
    internal class BufferProcessingException : Exception
    {
        public BufferProcessingException()
        {
        }

        public BufferProcessingException(string? message) : base(message)
        {
        }

        public BufferProcessingException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BufferProcessingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}