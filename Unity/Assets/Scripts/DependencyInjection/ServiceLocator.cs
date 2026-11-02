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
	}
}