using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace JsonRpc
{
	public class Server
	{
		private ConcurrentDictionary<string, Delegate> methods = new ConcurrentDictionary<string, Delegate>();
		private Smd smd = new Smd();

		public Server()
		{
		}
		
		public Server(IService service)
		{
			AddService(service);
		}

		public Server(IService[] services)
		{
			foreach (IService s in services)
			{
				AddService(s);
			}
		}

		public void AddService(IService service)
		{
			Type item = service.GetType();
			MethodInfo[] _methods = item.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			foreach (var meth in _methods)
			{
				if (meth.GetCustomAttributes(typeof(JsonRpc.JsonRpcMethodAttribute), false).Length == 0)
				{
					continue;
				}
				Dictionary<string, Type> paras = new Dictionary<string, Type>();
				ParameterInfo[] paramzs = meth.GetParameters();

				for (int i = 0; i < paramzs.Length; i++)
				{
					paras.Add(paramzs[i].Name, paramzs[i].ParameterType);
				}

				var resType = meth.ReturnType;
				paras.Add("returns", resType); // add the return type to the generic parameters list.

				var atdata = meth.GetCustomAttributes(typeof(JsonRpc.JsonRpcMethodAttribute), false);
				foreach (JsonRpc.JsonRpcMethodAttribute handlerAttribute in atdata)
				{
					var methodName = handlerAttribute.MethodName == string.Empty ? meth.Name : handlerAttribute.MethodName;
					var newDel = Delegate.CreateDelegate(System.Linq.Expressions.Expression.GetDelegateType(paras.Values.ToArray()), service, meth);
					if (this.methods.TryAdd(methodName, newDel))
					{
						smd.AddService(methodName, paras);
					}
				}
			}
		}
		
		public void AddService(IService[] services)
		{
			foreach (IService s in services)
			{
				AddService(s);
			}
		}

		private object CleanUpParameter(object p, SmdAdditionalParameters metaData)
		{
			if (p == null || (p != null && p.GetType() == metaData.ObjectType))
            {
                return p;
            }

			var bob = p as Newtonsoft.Json.Linq.JValue;
			if (bob != null && (bob.Value == null || bob.Value.GetType() == metaData.ObjectType))
			{
				return bob.Value;
			}

			var paramI = p;
			try
			{
				return Newtonsoft.Json.JsonConvert.DeserializeObject(paramI.ToString(), metaData.ObjectType);
			}
			catch (Exception)
			{
				// no need to throw here, they will
				// get an invalid cast exception right after this.
			}

			return paramI;
		}

		public Response Handle(Request req)
		{
			Delegate handle = null;
			SmdService metadata = null;
			bool haveDelegate = this.methods.TryGetValue(req.Method, out handle);
			bool haveMetadata = this.smd.Services.TryGetValue(req.Method, out metadata);

			if (haveDelegate == false || haveMetadata == false || metadata == null || handle == null)
			{
				return new Response()
				{
					Result = null
					, Error = new JsonRpcException(
						-32601
						, "Method not found"
						, "The method does not exist / is not available. Method name: `" + req.Method + "`"
					)
					, Id = req.Id
				};
			}

			if (req.Params == null) // allow params element to be missing without rewriting the params counting code below.
			{
				req.Params = new Newtonsoft.Json.Linq.JArray();
			}

			if (req.Params is ICollection == false)
			{
				return new Response()
				{
					Result = null
					, Error = new JsonRpcException(
						-32602
						, "Invalid params"
						, "The number of parameters could not be counted. Method name:  `" + req.Method + "`"
					)
					, Id = req.Id
				};
			}

			bool isJObject = req.Params is Newtonsoft.Json.Linq.JObject;
			bool isJArray = req.Params is Newtonsoft.Json.Linq.JArray;
			bool isArray = req.Params.GetType().IsArray;
			object[] parameters = null;
			var metaDataParamCount = metadata.parameters.Count(x => x != null);

			var getCount = req.Params as ICollection;
			var loopCt = getCount.Count;
			var paramCount = loopCt;
			if (paramCount == metaDataParamCount - 1 && metadata.parameters[metaDataParamCount-1].ObjectType.Name.Contains(typeof(JsonRpcException).Name))
			{
				paramCount++;
			}
			parameters = new object[paramCount];

			if (isJArray)
			{
				var jarr = ((Newtonsoft.Json.Linq.JArray)req.Params);
				for (int i = 0; i < loopCt; i++)
				{
					parameters[i] = CleanUpParameter(jarr[i], metadata.parameters[i]);
				}
			}
			else if (isJObject)
			{
				var jo = req.Params as Newtonsoft.Json.Linq.JObject;
				var asDict = jo as IDictionary<string, Newtonsoft.Json.Linq.JToken>;
				for (int i = 0; i < loopCt; i++)
				{
					if (asDict.ContainsKey(metadata.parameters[i].Name) == false)
					{
						return new Response()
						{
							Error = new JsonRpcException(
								-32602
								, "Invalid params"
								, string.Format(
									"Named parameter '{0}' was not present."
									, metadata.parameters[i].Name
								) + ". Method name: `" + req.Method + "`"
							)
							, Id = req.Id
						};
					}
					parameters[i] = CleanUpParameter(jo[metadata.parameters[i].Name], metadata.parameters[i]);
				}
			}
			else if (isArray)
			{
				var ps = req.Params as object[];
				for (int i = 0; i < loopCt; i++)
				{
					parameters[i] = CleanUpParameter(ps[i], metadata.parameters[i]);
				}
			}

			if (parameters.Length != metaDataParamCount)
			{
				return new Response()
				{
					Error = new JsonRpcException(
						-32602
						, "Invalid params"
					    , string.Format(
							"Expecting {0} parameters, and received {1}"
							, metadata.parameters.Length
							, parameters.Length
						) + ". Method name: `" + req.Method + "`"
				    )
					, Id = req.Id
				};
			}

			try
			{
				var results = handle.DynamicInvoke(parameters);
				return new Response() { Id = req.Id, Result = results };
			}
			catch (Exception ex)
			{
				if (ex is TargetParameterCountException)
				{
					return new Response() { Id = req.Id, Error = new JsonRpcException(-32602, "Invalid params", ex) };
				}

				// We really dont care about the TargetInvocationException, just pass on the inner exception
				if (ex is JsonRpcException)
				{
					return new Response() { Id = req.Id, Error = ex as JsonRpcException };
				}
				if (ex.InnerException != null && ex.InnerException is JsonRpcException)
				{
					return new Response() { Id = req.Id, Error = ex.InnerException as JsonRpcException };
				}
				else if (ex.InnerException != null)
				{
					return new Response() { Id = req.Id, Error = new JsonRpcException(-32603, "Internal Error", ex.InnerException) };
				}

				return new Response() { Id = req.Id, Error = new JsonRpcException(-32603, "Internal Error", ex) };
			}
		}

		public string Handle(string json)
		{
			try
			{
				Request req = new Request(json);
				Response res = Handle(req);
				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				return result;
			}
			catch (JsonRpcException ex)
			{
				Response res = new Response();
				try
				{
					Request req = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(json);
					res.Id = req.Id;
				}
				catch (Exception) {}
				res.Error = ex;

				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				return result;
			}
			catch (Exception ex)
			{
				Response res = new Response();
				try
				{
					Request req = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(json);
					res.Id = req.Id;
				}
				catch (Exception) {}
				res.Error = new JsonRpcException(0, ex.Message, ex.Data);

				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				return result;
			}
		}
	}
}

