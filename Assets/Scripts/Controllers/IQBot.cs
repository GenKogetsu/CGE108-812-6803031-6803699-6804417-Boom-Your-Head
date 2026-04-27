using UnityEngine;

public class IQBot : MonoBehaviour
{
    public string LoadSceneName;
    public float delayTime = 2f;    

    bool isClicked;

    public void OnClick()
    {
        if (isClicked) return;
        StartCoroutine(LoadSceneAfter(LoadSceneName, delayTime));
    }

    public IEnumerator LoadSceneAfter(string sceneName , float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneEffectController.Instance.LoadSceneAndPlayEffect(sceneName);
    }
}
