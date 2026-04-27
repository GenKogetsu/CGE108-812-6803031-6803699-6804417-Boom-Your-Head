using UnityEngine;

public class ButtonShow : MonoBehaviour
{
    public List<GameObject> Buttons;
    public Dictionary<int, GameObject> ButtonsDict;

    public int currentButtonIndex = 0;

    private void Awake()
    {
        ButtonsDict = new Dictionary<int, GameObject>();
        foreach (var button in Buttons)
        {
            int index = Buttons.IndexOf(button);
            button.SetActive(false);
            ButtonsDict.Add(index, button);
        }
        ButtonsDict[currentButtonIndex].SetActive(true);
    }

    public void PlusIndex()
    {
        ButtonsDict[currentButtonIndex].SetActive(false);
        if (currentButtonIndex < Buttons.Count-1) currentButtonIndex++;
        ButtonsDict[currentButtonIndex].SetActive(true);
    }

    public void MinusIndex()
    {
        ButtonsDict[currentButtonIndex].SetActive(false);
        if (currentButtonIndex > 0) currentButtonIndex--;
        ButtonsDict[currentButtonIndex].SetActive(true);
    }
}
