using System.Collections.Generic;

namespace SystemManagement
{
	using Interfaces;

	public class SystemManager
	{
		private readonly List<IUpdateSystem> updateSystems = new();

		private readonly List<IFixedUpdateSystem> fixedUpdateSystems = new();

		private readonly List<ILateUpdateSystem> lateUpdateSystems = new();

		// IUpdatable
		public void Register(object system)
		{
			if (system is IUpdateSystem update)
				updateSystems.Add(update);

			if (system is IFixedUpdateSystem fixedUpdate)
				fixedUpdateSystems.Add(fixedUpdate);

			if (system is ILateUpdateSystem lateUpdate)
				lateUpdateSystems.Add(lateUpdate);
		}

		public void Update()
		{
			//check allocations on for each
			foreach (var system in updateSystems)
				system.RunUpdate();
		}

		public void FixedUpdate()
		{
			foreach (var system in fixedUpdateSystems)
				system.RunFixedUpdate();
		}

		public void LateUpdate()
		{
			foreach (var system in lateUpdateSystems)
				system.RunLateUpdate();
		}
	}
}