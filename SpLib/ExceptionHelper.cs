using System;

namespace SpLib
{
    /// <summary>
    /// The class ExceptionHelper provides
    /// - Factory methods for specified exceptions with an appropriate error message
    /// </summary>
    public static class ExceptionHelper
    {
        /// <summary>
        /// Create an exception of a specific type with the specified message
        /// </summary>
        /// <typeparam name="T">Type of exception to be thrown</typeparam>
        /// <param name="message">Exception message or message format string</param>
        /// <param name="messageArgs">Arguments of message</param>
        /// <returns>Created exception with formatted message</returns>
        public static Exception CreateException<T>(string message, params object[] messageArgs) where T : Exception
        {
            var ctor = typeof(T).GetConstructor(new[] { typeof(string) });
            var exception = (Exception)ctor.Invoke(new object[] { string.Format(message, messageArgs) });
            return exception;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">error message</param>
        /// <param name="argumentName">name of argument that violated the check</param>
        /// <returns></returns>
        public static Exception CreateArgumentException<T>(string message, string argumentName) where T : ArgumentException
        {
            var ctor = typeof(T).GetConstructor(new[] { typeof(string), typeof(string) });
            var exception = (Exception)ctor.Invoke(new object[] { argumentName, message });
            return exception;
        }

    }
}
