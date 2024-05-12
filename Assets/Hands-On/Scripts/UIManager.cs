using System;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public GameObject _shortcutInfo;
    public GameObject _debuggerMode;
    public GameObject _sentisAnimation;

    private void Start()
    {
        //AnimatedSprite(_sentisAnimation);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _shortcutInfo.SetActive(!_shortcutInfo.activeSelf);
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AnimatedSprite(_sentisAnimation);
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            _debuggerMode.SetActive(!_debuggerMode.activeSelf);
        }
    }

    
    
    public void AnimatedSprite(GameObject gameObject)
    {
        if(gameObject.activeSelf != true)
        {
            StartCoroutine(ActiveSprite(gameObject));
        }
    }
    IEnumerator ActiveSprite(GameObject gameObject)
    {
        _sentisAnimation.SetActive(true); 
        yield return new WaitForSeconds(2.0f); 
        _sentisAnimation.SetActive(false); 
    }
}
