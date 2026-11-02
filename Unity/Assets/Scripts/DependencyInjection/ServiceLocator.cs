using System;
using System.Collections.Generic;


namespace DependencyInjection
{
	public static class ServiceLocator
	{
		private static readonly Dictionary<Type, object> services = new();

		public static void Register<T>(T service) where T : class
		{
			services[typeof(T)] = service;
		}

		public static T Get<T>() where T : class
			=> services[typeof(T)] as T;

		// For services that come and go at runtime, such as networked objects
		public static bool TryGet<T>(out T service) where T : class
		{
			service = services.TryGetValue(typeof(T), out var found) ? found as T : null;

			return service != null;
		}

		public static void Unregister<T>(T service) where T : class
		{
			if (services.TryGetValue(typeof(T), out var registered) && registered == service)
				services.Remove(typeof(T));
		}
	}
}