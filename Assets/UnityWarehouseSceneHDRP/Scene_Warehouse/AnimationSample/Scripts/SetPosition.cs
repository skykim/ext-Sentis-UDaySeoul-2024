using UnityEngine;





namespace UnityWarehouseSceneHDRP
{
	public class SetPosition: MonoBehaviour
	{
		public void Set(Transform target)
		{
			transform.position = target.position;
		}
	}
}