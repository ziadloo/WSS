using System;

namespace JsonRpc
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public sealed class JsonRpcMethodAttribute : Attribute
	{
		readonly string methodName;
		
		public string MethodName
		{
			get { return methodName; }
		}

		public JsonRpcMethodAttribute(string methodName = "")
		{
			this.methodName = methodName;
		}
	}
}
