using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ChestController : MonoBehaviour
{
    [Header("Animacion")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openAnimationStateName = "ChestOpen";
    [SerializeField] private float tiempoMaximoEsperaAnimacion = 3f;

    [Header("Recompensa")]
    [SerializeField] private GameObject chestItem;
    [SerializeField] private float chestDelay = 0.5f;
    [SerializeField] private float tiempoAntesDesaparecer = 0.35f;
    [SerializeField] private float duracionFadeOut = 0.5f;

    [Header("Portal")]
    [SerializeField] private PortalController portalController;

    [Header("Interaccion")]
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private string nombreEscenaFinal = "Nivel3";

    private bool itemPicked;
    private bool playerInRange;
    private SpriteRenderer[] chestSprites;
    private Collider2D[] chestColliders;

    private void Start()
    {
        if (chestAnimator == null)
            chestAnimator = GetComponent<Animator>();

        if (chestAnimator == null)
            chestAnimator = GetComponentInChildren<Animator>();

        chestSprites = GetComponentsInChildren<SpriteRenderer>(true);
        chestColliders = GetComponentsInChildren<Collider2D>(true);

        if (portalController == null)
            portalController = FindFirstObjectByType<PortalController>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (itemPicked || !playerInRange || !InteractPressedThisFrame())
            return;

        int estadoAperturaHash = ReproducirAnimApertura();
        itemPicked = true;
        playerInRange = false;
        DesactivarColisionesCofre();
        StartCoroutine(GetChestItem());
        StartCoroutine(FadeOutYCerrarCofre());
        StartCoroutine(MostrarPortalDespuesDeAnimacion(estadoAperturaHash));
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (EsJugador(col.gameObject))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (EsJugador(col.gameObject))
            playerInRange = false;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (EsJugador(col.gameObject))
            playerInRange = true;
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (EsJugador(col.gameObject))
            playerInRange = false;
    }

    private int ReproducirAnimApertura()
    {
        if (chestAnimator == null)
            return -1;

        string[] estadosPosibles = { openAnimationStateName, "ChestOpen", "chest_open", "openchest" };
        for (int i = 0; i < estadosPosibles.Length; i++)
        {
            int stateHash = Animator.StringToHash(estadosPosibles[i]);
            if (chestAnimator.HasState(0, stateHash))
            {
                chestAnimator.Play(stateHash);
                return stateHash;
            }
        }

        // Fallback al parametro del controller si no encuentra estado por nombre.
        chestAnimator.SetBool("isOpen", true);
        return -1;
    }

    private IEnumerator GetChestItem()
    {
        yield return new WaitForSeconds(chestDelay);

        if (chestItem != null)
            Instantiate(chestItem, transform.position, Quaternion.identity);
    }

    private IEnumerator FadeOutYCerrarCofre()
    {
        yield return new WaitForSeconds(tiempoAntesDesaparecer);

        if (chestSprites == null || chestSprites.Length == 0)
        {
            gameObject.SetActive(false);
            yield break;
        }

        float t = 0f;
        while (t < duracionFadeOut)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / Mathf.Max(0.0001f, duracionFadeOut));
            for (int i = 0; i < chestSprites.Length; i++)
            {
                Color c = chestSprites[i].color;
                c.a = alpha;
                chestSprites[i].color = c;
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator MostrarPortalDespuesDeAnimacion(int estadoAperturaHash)
    {
        if (estadoAperturaHash != -1 && chestAnimator != null)
        {
            float tiempoEspera = 0f;
            while (tiempoEspera < Mathf.Max(0.1f, tiempoMaximoEsperaAnimacion))
            {
                AnimatorStateInfo stateInfo = chestAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.shortNameHash == estadoAperturaHash && stateInfo.normalizedTime >= 1f)
                    break;

                tiempoEspera += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(chestDelay);
        }

        if (EsEscenaFinal())
        {
            FinalizarJuego();
            yield break;
        }

        if (portalController != null)
            portalController.MostrarPortal();
    }

    private bool EsEscenaFinal()
    {
        string actual = SceneManager.GetActiveScene().name;
        return !string.IsNullOrWhiteSpace(nombreEscenaFinal) &&
               string.Equals(actual, nombreEscenaFinal, System.StringComparison.OrdinalIgnoreCase);
    }

    private void FinalizarJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DesactivarColisionesCofre()
    {
        if (chestColliders == null)
            return;

        for (int i = 0; i < chestColliders.Length; i++)
            chestColliders[i].enabled = false;
    }

    private bool EsJugador(GameObject obj)
    {
        return obj.CompareTag("Player") || obj.GetComponentInParent<PlayerController>() != null;
    }

    private bool InteractPressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame;
    }
}
