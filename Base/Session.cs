using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace Base
{
	public class Session : ConcurrentDictionary<string, object>
	{
		public Session()
		{
		}

		public Session(ConcurrentDictionary<string, object> src)
			: base(src)
		{
		}

		public virtual T Get<T>(string key)
		{
			object obj;
			if (this.TryGetValue (key, out obj)) {
				if (obj is JToken) {
					return (this [key] as JToken).ToObject<T> ();
				} else {
					return (T)obj;
				}
			} else {
				return default(T);
			}
		}

		public virtual void Set(string key, object value)
		{
			this.AddOrUpdate(key, value, (k, v) => {
				return value;
			});
		}
	}
}

