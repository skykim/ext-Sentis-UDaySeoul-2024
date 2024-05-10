using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;





namespace UnityWarehouseSceneHDRP
{
	public class ForkliftLight : MonoBehaviour
	{
		[SerializeField] private Renderer _light;
		[SerializeField] private Renderer _light2;
		[SerializeField] private GameObject _lineLight;
		[SerializeField] List<Transform> _warningLights; 
		[SerializeField] private float _rotateSpeed;
		[SerializeField] Light _patolight;
		[SerializeField] Light _patolight2;

		public void WinkerRight(bool isEnabled)
		{
			StopAllCoroutines();

			if(isEnabled)
			{
				StartCoroutine(WinkerRightCoroutine());
			}
			else
			{
				_light.material.SetFloat("_Winker_Right_On", 0);
			}
		}
		private IEnumerator WinkerRightCoroutine()
		{
			float light = _light.material.GetFloat("_Winker_Right_On");
			while(true)
			{
				yield return new WaitForSeconds(0.5f);
				light = 1 - light;
				_light.material.SetFloat("_Winker_Right_On", light);
			}
		}



		public void WinkerLeft(bool isEnabled)
		{
			StopAllCoroutines();

			if(isEnabled)
			{
				StartCoroutine(WinkerLeftCoroutine());
			}
			else
			{
				_light.material.SetFloat("_Winker_Left_On", 0);
			}
		}
		private IEnumerator WinkerLeftCoroutine()
		{
			float light = _light.material.GetFloat("_Winker_Left_On");
			while(true)
			{
				yield return new WaitForSeconds(0.5f);
				light = 1 - light;
				_light.material.SetFloat("_Winker_Left_On", light);
			}
		}



		public void LineLight(bool isEnabled)
		{
			_light.material.SetFloat("_Linelight_On", isEnabled ? 1 : 0);
			_lineLight.SetActive(isEnabled);
		}



		public void Brakelight(bool isEnabled)
		{
			_light.material.SetFloat("_Brakelight_On", isEnabled ? 1 : 0);
		}


		private void Awake()
		{
			_light2.material.SetFloat("_Patolamp_On", 1);
		}

		private void Update()
		{
			RoatateWarming();
		}

		private void RoatateWarming()
		{
			for(int i=0; i < _warningLights.Count; i++)
			{
				if(_warningLights[i] != null )
				{
					_warningLights[i].Rotate(0, _rotateSpeed, 0);
				}
			}
		}
	}
}