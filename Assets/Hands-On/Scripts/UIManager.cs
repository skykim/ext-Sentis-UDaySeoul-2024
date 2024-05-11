using System.Collections;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject _shortcutInfo;
    public GameObject _sentisAnimation;

    // Update is called once per frame
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
        yield return new WaitForSeconds(3.0f); 
        _sentisAnimation.SetActive(false); 
    }
}
