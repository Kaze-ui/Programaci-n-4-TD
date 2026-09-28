using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        EnsureMenuBackground();
    }

    private void EnsureMenuBackground()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null && canvas.transform.Find("MenuBackground_Image") == null)
        {
            GameObject bgObj = new GameObject("MenuBackground_Image");
            bgObj.transform.SetParent(canvas.transform, false);
            bgObj.transform.SetAsFirstSibling();

            UnityEngine.UI.Image img = bgObj.AddComponent<UnityEngine.UI.Image>();
            Sprite bgSprite = Resources.Load<Sprite>("Sprites/MenuBackground");
            if (bgSprite != null)
            {
                img.sprite = bgSprite;
            }
            img.color = new Color(0.9f, 0.9f, 1f, 1f);

            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
    public void PlayGame()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("InGame");
        }
    }

    public void QuitGame()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.QuitGame();
        }
        else
        {
            Debug.Log("Cerrando el juego...");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}