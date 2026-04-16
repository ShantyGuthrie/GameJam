using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class StoryManager : MonoBehaviour
{
    [TextArea(10, 20)]
    public string[] textos;

    public TextMeshProUGUI textoUI;

    private int index = 0;

    void Start()
    {
        if (textos == null || textos.Length == 0 || textoUI == null)
            return;

        textoUI.text = textos[index];
    }

    public void SiguienteTexto()
    {
        if (textos == null || textos.Length == 0 || textoUI == null)
        {
            SceneManager.LoadScene("level1");
            return;
        }

        index++;

        if (index < textos.Length)
        {
            textoUI.text = textos[index];
        }
        else
        {
            SceneManager.LoadScene("level1");
        }
    }
}