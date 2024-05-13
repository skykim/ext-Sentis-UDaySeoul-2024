using System.Collections;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject _shortcutPanel;
    public GameObject _debuggerPanel;
    public GameObject _sentisAnimation;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _shortcutPanel.SetActive(!_shortcutPanel.activeSelf);
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            _debuggerPanel.SetActive(!_debuggerPanel.activeSelf);
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            AnimatedSprite(_sentisAnimation);
        }
    }
    
    public void AnimatedSprite(GameObject splashObject)
    {
        if(gameObject.activeSelf != true)
        {
            StartCoroutine(ActiveSprite(splashObject));
        }
    }
    IEnumerator ActiveSprite(GameObject splashObject)
    {
        splashObject.SetActive(true); 
        yield return new WaitForSeconds(2.0f); 
        splashObject.SetActive(false); 
    }
}
