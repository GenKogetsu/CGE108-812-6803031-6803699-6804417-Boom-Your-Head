using UnityEngine;
using UnityEngine.UI;

public class OptionContoller : MonoBehaviour
{
    public Animator OptionAnimator;
    public void ShowOption(Button optionName)
    {
        ResetOptions();
        if (optionName.name == "Display")
        {
            OptionAnimator.SetBool("ShowDisplay", true);
        }

        if (optionName.name == "Audio")
        {
            OptionAnimator.SetBool("ShowAudio", true);
        }

        if (optionName.name == "Language")
        {
            OptionAnimator.SetBool("ShowLanguage", true);
        }
        if (optionName.name == "KeyBinding")
        {
            OptionAnimator.SetBool("ShowKeyBinding", true);
        }
        if (optionName.name == "Accessibility")
        {
            OptionAnimator.SetBool("ShowAccessibility", true);
        }
    }

    private void ResetOptions()
    {
        OptionAnimator.SetBool("ShowDisplay", false);
        OptionAnimator.SetBool("ShowAudio", false);
        OptionAnimator.SetBool("ShowLanguage", false);
        OptionAnimator.SetBool("ShowKeyBinding", false);
        OptionAnimator.SetBool("ShowAccessibility", false);
    }
}
