using System;

namespace SpLib
{
    /// <summary>
    /// The class contract is motivated by the class Contract of the namespace System.Diagnostics.Contract. Unfortunately CodeContracts were not supported in
    /// Visual Studio since version 2015. Nevertheless I wanted to keep at least a consistent uniform way to do parameter testing.
    /// So: What is left is not anymore comparable with what the original CodeContracts used to be. 
    /// It just offers a way of checking parameters for functions and giving out an appropriate error message in case of a 'contract violoation'
    /// </summary>
    public static class Contract
    {
        public static readonly string ArgumentValidationFailedMsgTemplate = @"Argument validation of function {0} failed";
        /// <summary>
        /// Used for function argument checking.
        /// </summary>
        /// <typeparam name="T">Exception that is thrown if the condition is not met</typeparam>
        /// <param name="condition">Condition that is checked for function arguments</param>
        /// <param name="message">The error message that is associated with the contract violation</param>
        /// <param name="args">Name of the argument that was checked</param>
        public static void Requires<T>(bool condition, string message, string arg) where T : ArgumentException
        {
            if (!condition)
            {
                throw ExceptionHelper.CreateArgumentException<T>(message, arg);
            }
        }

        /// <summary>
        /// Check for argument not null; if agument == null, a argument null exception is thrown
        /// </summary>
        /// <param name="arg">actual argument</param>
        /// <param name="argument">argument name</param>
        public static void RequiresArgumentNotNull(object arg, string argument)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(argument);
            }
        }

        /// <summary>
        /// Used for function argument checking.
        /// Without specifying a exception type, the thrown exception will be of type ViolatedContractException
        /// </summary>
        /// <param name="condition">Condition that is checked for function arguments</param>
        /// <param name="message">The error message that is associated with the contract violation</param>
        /// <param name="args">Possibly additional arguments to be included in the error</param>
        public static void Requires(bool condition, string message, string argument)
        {
            Requires<ViolatedContractException>(condition, message, argument);
        }

        /// <summary>
        /// Used for function argument checking.
        /// Without specifying a exception type, the thrown exception will be of type ViolatedContractException
        /// </summary>
        /// <param name="condition">Condition that is checked for function arguments</param>
        /// <param name="message">The error message that is associated with the contract violation</param>
        /// <param name="args">Name of argment(s) that lead to the contract violation</param>
        public static void Requires(bool condition, string arg)
        {
            if (!condition)
            {
                var stackFrame = new System.Diagnostics.StackFrame(1, true);
                var method = stackFrame.GetMethod();
                var methodName = string.Format("{0}.{1}(..)", method.DeclaringType.Name, method.Name);
                var message = string.Format(ArgumentValidationFailedMsgTemplate, methodName);
                Requires<ViolatedContractException>(condition, message, arg);
            }
        }
    }
}
