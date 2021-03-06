using System;
using System.Runtime.Serialization;

namespace SpLib
{
    /// <summary>
    /// ViolatedContractException is the exception that is thrown by the function Contract.Requires in case nothing else 
    /// is specified
    /// </summary>
    public class ViolatedContractException : ArgumentException
    {
        /// <summary>
        /// Constructor: message and argument have to be swapped in order to conform with the System exceptions
        /// </summary>
        /// <param name="message"></param>
        /// <param name="argument"></param>
        public ViolatedContractException(string message, string argument)
            : base(argument, message) { }
        protected ViolatedContractException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}
