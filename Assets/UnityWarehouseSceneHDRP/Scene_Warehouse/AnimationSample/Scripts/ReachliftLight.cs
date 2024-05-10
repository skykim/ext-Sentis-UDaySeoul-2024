using System.Collections;
using UnityEngine;





namespace UnityWarehouseSceneHDRP
{
    public class ReachliftLight : MonoBehaviour
    {
        [SerializeField] private Renderer _light;
        [SerializeField] private GameObject _lineLight;





        public void Movelight(bool isEnabled)
        {
            _light.material.SetFloat("_Movelight_On", isEnabled ? 1 : 0);
        }



        public void MovelightBlink(bool isEnabled)
        {
            if(isEnabled)
            {
                StartCoroutine(MovelightBlinkCoroutine());
            }
            else
            {
                StopAllCoroutines();
            }
        }
        private IEnumerator MovelightBlinkCoroutine()
        {
            float light = _light.material.GetFloat("_Movelight_On");
            while(true)
            {
                yield return new WaitForSeconds(0.5f);
                light = 1 - light;
                _light.material.SetFloat("_Movelight_On", light);
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
            _light.material.SetFloat("_Movelight_On", 1);
        }
    }
}